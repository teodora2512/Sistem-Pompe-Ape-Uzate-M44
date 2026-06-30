using System;
using System.ComponentModel;
using DataModel;
using Communicator;

namespace Simulator
{
    class PompeViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string prop)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        // Worker rulează logica la fiecare 100ms pe un thread separat
        private BackgroundWorker worker = new BackgroundWorker();
        // Timer pentru tranzitii cu intarziere (teste pompe 3s)
        private System.Timers.Timer timer = new System.Timers.Timer();
        // Timer separat pentru alarma sonora (5s)
        private System.Timers.Timer alarmTimer = new System.Timers.Timer();
        // Trimite starea curenta catre Monitor prin TCP
        private Sender _monitorSender;

        // ===== CAPACITATI FIXE POMPE (cat scot pompele pe ciclu de 100ms) =====
        // Acestea sunt constante - nu depind de potentiometru, reprezinta
        // capacitatea fizica a fiecarei pompe de a evacua apa
        private const double Pump1Capacity = 0.6;
        private const double Pump2Capacity = 0.6;

        //----Variabile functionalitatea 2 (ANALOGICA)----
        private bool _isAnalogModeActive = false;
        public bool IsAnalogModeActive
        {
            get => _isAnalogModeActive;
            set
            {
                _isAnalogModeActive = value;
                OnPropertyChanged(nameof(IsAnalogModeActive));
                OnPropertyChanged(nameof(AnalogLinesVisibility));
                if (!value)
                {
                    _savedLevelB2 = 100.0;
                    _savedLevelB5 = 180.0;
                    OnPropertyChanged(nameof(LevelB2));
                    OnPropertyChanged(nameof(LevelB5));
                    OnPropertyChanged(nameof(LevelB2Top));
                    OnPropertyChanged(nameof(LevelB5Top));
                }
            }
        }

        // ===== POTENTIOMETRU 1 (Y - Zulauf/Inflow) =====
        // Conform cerintei: "Rata de incarcare se seteaza cu ajutorul potentiometrului 1"
        // Reprezinta rata de admisie a apei in bazin (0-10V), constanta in toate starile
        private double _potentiometerVoltage1 = 1.0;

        public double Potentiometer1Angle =>
            -135 + (PotentiometerVoltage1 / 10.0) * 270;

        public double PotentiometerVoltage1
        {
            get => _potentiometerVoltage1;
            set
            {
                _potentiometerVoltage1 = Math.Max(0.0, Math.Min(10.0, value));
                OnPropertyChanged(nameof(PotentiometerVoltage1));
                OnPropertyChanged(nameof(Potentiometer1Angle));
                OnPropertyChanged(nameof(InflowRate));
            }
        }

        // Rata de admisie calculata o singura data din potentiometru
        // 0V = 0 px/ciclu, 10V = 1.5 px/ciclu (peste capacitatea unei singure pompe)
        public double InflowRate => (PotentiometerVoltage1 / 10.0) * 1.5;

        // ===== POTENTIOMETRU 2 (U2 - Soll-Pegel/Setpoint) =====
        // Seteaza pragul de pornire pentru P1 (S1+S3) sau P2 (S1+S4)
        private double _potentiometerVoltage2 = 0.0;

        public double Potentiometer2Angle =>
            -135 + (PotentiometerVoltage2 / 10.0) * 270;

        public double PotentiometerVoltage2
        {
            get => _potentiometerVoltage2;
            set
            {
                _potentiometerVoltage2 = Math.Max(0.0, Math.Min(10.0, value));
                OnPropertyChanged(nameof(PotentiometerVoltage2));
                OnPropertyChanged(nameof(Potentiometer2Angle));
                OnPropertyChanged(nameof(Pot2TargetPixelLevel));
                OnPropertyChanged(nameof(Pot2TargetTop));

                CheckAndSaveAnalogThresholds();
            }
        }

        private double _savedLevelB2 = 100.0; // prag pornire P1 (implicit 100)
        private double _savedLevelB5 = 180.0; // prag pornire P2 (implicit 180)

        // ===== IESIRI ANALOGICE (0-10V) - conform cerintei functionalitatea 2 =====
        // Iesirea analogica 1: nivelul curent al apei, normalizat in tensiune 0-10V
        // "nivelul curent al apei este pus la dispozitia automatului programabil
        //  pe iesirea analogica 1 a simulatorului printr-o marime normalizata in tensiune 0-10V"
        public double AnalogOutput1_WaterLevelVoltage => (WaterLevel / 260.0) * 10.0;

        // Iesirea analogica 2: pragul setat curent (B2 sau B5), normalizat in tensiune 0-10V
        // "iesirea analogica 2 a simulatorului va oferi automatului valoarea setata
        //  ca marime analogica normalizata in tensiune 0-10V"
        public double AnalogOutput2_SetPointVoltage => (TheStateOfTheProcess == ProcessState.BothPumpsRunning)
            ? (_savedLevelB5 / 260.0) * 10.0
            : (_savedLevelB2 / 260.0) * 10.0;

        // Nivelurile senzorilor in pixeli (bazin = 260px total)
        // Canvas.Top corespunzator = 400 - LevelBx
        public double LevelB1 = 30;   // nivel minim - oprire pompe
        public double LevelB2 => IsAnalogModeActive ? _savedLevelB2 : 100.0;  // pornire P1
        public double LevelB4 = 80;   // backup pentru B1
        public double LevelB5 => IsAnalogModeActive ? _savedLevelB5 : 180.0;  // pornire P2
        public double LevelB3 = 240;  // nivel critic - alarma

        public PompeViewModel() { }

        public void Init()
        {
            _monitorSender = new Sender("127.0.0.1", 3000);
            timer.Elapsed += _timer_Elapsed;

            alarmTimer.Interval = 5000;
            alarmTimer.AutoReset = false;
            alarmTimer.Elapsed += (s, e) => IsAlarmSounding = false;

            worker.DoWork += _worker_DoWork;
            worker.RunWorkerAsync();
        }

        // ===== STAREA PROCESULUI =====

        private ProcessState _currentState = ProcessState.Off;

        public ProcessState TheStateOfTheProcess
        {
            get => _currentState;
            set
            {
                _currentState = value;
                // Notifica Monitor-ul de fiecare data cand starea se schimba
                _monitorSender.Send(Convert.ToByte(_currentState));
            }
        }

        // ===== TRANZITII CU TIMER =====

        private bool _isChangingStateInProgress = false;
        private ProcessState _nextState;

        private void _timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _isChangingStateInProgress = false;
            TheStateOfTheProcess = _nextState;
            timer.Stop();
        }

        private void ChangeProcessState(ProcessState nextState, int timeInterval)
        {
            if (!_isChangingStateInProgress)
            {
                _isChangingStateInProgress = true;
                _nextState = nextState;
                timer.Interval = timeInterval;
                timer.Start();
            }
        }

        // ===== LOOP PRINCIPAL =====

        private void _worker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                ComputeNextState(TheStateOfTheProcess);
                System.Threading.Thread.Sleep(100);
            }
        }

        // ===== NIVELUL APEI =====

        private double _waterLevel = 10;

        public double WaterLevel
        {
            get => _waterLevel;
            set
            {
                _waterLevel = Math.Max(0, Math.Min(260, value));
                OnPropertyChanged(nameof(WaterLevel));
                OnPropertyChanged(nameof(WaterTop));

                // Notificam PLC-ul ca s-au schimbat tensiunile pe iesirile analogice
                OnPropertyChanged(nameof(AnalogOutput1_WaterLevelVoltage));
                OnPropertyChanged(nameof(AnalogOutput2_SetPointVoltage));
            }
        }

        public double WaterTop => 400 - _waterLevel;

        // ===== FLAGS SIMULARE =====

        private bool _isB1Defect = false;
        private bool _isHighInflowRate = false;

        public bool IsB1Defect
        {
            get => _isB1Defect;
            set { _isB1Defect = value; OnPropertyChanged(nameof(IsB1Defect)); }
        }

        public bool IsHighInflowRate
        {
            get => _isHighInflowRate;
            set { _isHighInflowRate = value; OnPropertyChanged(nameof(IsHighInflowRate)); }
        }

        private double EffectiveLevelMin => _isB1Defect ? LevelB4 : LevelB1;

        // ===== LOGICA PROCESULUI =====
        // Model fizic: nivel += InflowRate (apa care intra) - capacitate pompe active
        // InflowRate vine din potentiometrul 1, constant in toate starile.
        // In modul normal (non-analogic), InflowRate-ul ramane irelevant si se
        // folosesc valorile fixe ca pana acum, pentru compatibilitate cu modul clasic.

        public void ComputeNextState(ProcessState currentState)
        {
            switch (currentState)
            {
                case ProcessState.Off:
                    IsPump1Running = false;
                    IsPump2Running = false;
                    IsAlarmOn = false;
                    IsAlarmSounding = false;
                    IsSystemOn = false;
                    break;

                case ProcessState.On_NoPump:
                    IsSystemOn = true;
                    IsPump1Running = false;
                    IsPump2Running = false;
                    IsAlarmOn = false;

                    if (IsAnalogModeActive)
                    {
                        // Nicio pompa - apa creste cu rata de admisie setata pe potentiometrul 1
                        WaterLevel += InflowRate;

                        if (WaterLevel >= _savedLevelB2)
                            ForceNextState(ProcessState.Pump1Running);
                        if (WaterLevel >= _savedLevelB5)
                            ForceNextState(ProcessState.BothPumpsRunning);
                    }
                    else
                    {
                        // Mod normal (non-analogic) - rata fixa de admisie
                        WaterLevel += 0.5;

                        if (WaterLevel >= LevelB2)
                            ForceNextState(ProcessState.Pump1Running);
                    }

                    if (WaterLevel >= LevelB3)
                        TriggerAlarmB3();

                    break;

                case ProcessState.Pump1Running:
                    IsSystemOn = true;
                    IsPump1Running = true;
                    IsPump2Running = false;
                    IsAlarmOn = false;

                    if (IsAnalogModeActive)
                    {
                        // Model fizic: nivel = admisie (potentiometru) - capacitate P1 (fixa)
                        double netFlow = InflowRate - Pump1Capacity;
                        // Garantam ca nivelul se misca mereu - eliminam posibilitatea
                        // de echilibru exact (freeze) care apare cand admisia egaleaza
                        // exact capacitatea pompei la anumite valori ale potentiometrului
                        if (Math.Abs(netFlow) < 0.05)
                            netFlow = netFlow >= 0 ? 0.05 : -0.05;
                        WaterLevel += netFlow;

                        if (WaterLevel >= _savedLevelB5)
                            ForceNextState(ProcessState.BothPumpsRunning);
                    }
                    else
                    {
                        // --- LOGICA IMPLICITA (mod non-analogic) ---
                        if (_isHighInflowRate)
                        {
                            WaterLevel += 0.2;
                            if (WaterLevel >= LevelB5)
                                ForceNextState(ProcessState.BothPumpsRunning);
                        }
                        else
                        {
                            WaterLevel -= 0.5;
                        }
                    }

                    if (WaterLevel <= EffectiveLevelMin)
                        ForceNextState(ProcessState.On_NoPump);

                    if (WaterLevel >= LevelB3)
                        TriggerAlarmB3();

                    break;

                case ProcessState.BothPumpsRunning:
                    IsSystemOn = true;
                    IsPump1Running = true;
                    IsPump2Running = true;
                    IsAlarmOn = false;

                    if (IsAnalogModeActive)
                    {
                        // Ambele pompe: admisie - (capacitate P1 + capacitate P2)
                        double netFlow = InflowRate - (Pump1Capacity + Pump2Capacity);
                        // Acelasi fix - eliminam posibilitatea de echilibru exact
                        if (Math.Abs(netFlow) < 0.05)
                            netFlow = netFlow >= 0 ? 0.05 : -0.05;
                        WaterLevel += netFlow;
                    }
                    else
                    {
                        WaterLevel -= 0.8;
                    }

                    // Chiar cu ambele pompe active, daca admisia e prea mare,
                    // nivelul poate atinge B3 - trebuie verificat si aici
                    if (WaterLevel >= LevelB3)
                        TriggerAlarmB3();

                    if (WaterLevel <= EffectiveLevelMin)
                        ForceNextState(ProcessState.On_NoPump);
                    break;

                case ProcessState.AlarmB3:
                    // Nivel critic: ambele pompe pornite fortat pana sub nivelul minim
                    IsSystemOn = true;
                    IsPump1Running = true;
                    IsPump2Running = true;
                    WaterLevel -= 0.8;
                    if (WaterLevel <= EffectiveLevelMin)
                    {
                        IsAlarmOn = false;
                        ForceNextState(ProcessState.On_NoPump);
                    }
                    break;

                case ProcessState.Alarm:
                    // Releu protectie declansat: pompe oprite, apa creste liber
                    IsPump1Running = false;
                    IsPump2Running = false;
                    IsAlarmOn = true;

                    if (IsAnalogModeActive)
                        WaterLevel += InflowRate;
                    else
                        WaterLevel += 0.3;

                    if (WaterLevel >= LevelB3)
                        TriggerAlarmB3();
                    break;

                case ProcessState.TestPump1:
                    IsSystemOn = true;
                    IsPump1Running = true;
                    IsPump2Running = false;
                    IsAlarmOn = false;
                    WaterLevel -= 0.3;
                    ChangeProcessState(ProcessState.On_NoPump, 3000);
                    break;

                case ProcessState.TestPump2:
                    IsSystemOn = true;
                    IsPump1Running = false;
                    IsPump2Running = true;
                    IsAlarmOn = false;
                    WaterLevel -= 0.3;
                    ChangeProcessState(ProcessState.On_NoPump, 3000);
                    break;
            }
        }

        private void TriggerAlarmB3()
        {
            IsAlarmOn = true;
            IsAlarmSounding = true;
            alarmTimer.Start();
            TheStateOfTheProcess = ProcessState.AlarmB3;
        }

        public void ForceNextState(ProcessState nextState)
        {
            _isChangingStateInProgress = false;
            timer.Stop();
            TheStateOfTheProcess = nextState;
        }

        // ===== LOGICA MOD ANALOGIC (FUNCTIONALITATEA 2) =====

        public bool IsS1Pressed { get; set; } = false;
        public bool IsS3Pressed { get; set; } = false;
        public bool IsS4Pressed { get; set; } = false;

        private bool _isP1ThresholdSet = false;
        private bool _isP2ThresholdSet = false;

        public double LevelB2Top => 400 - _savedLevelB2;
        public double LevelB5Top => 400 - _savedLevelB5;

        public double Pot2TargetPixelLevel => (PotentiometerVoltage2 / 10.0) * 260.0;
        public double Pot2TargetTop => 400 - Pot2TargetPixelLevel;

        public System.Windows.Visibility AnalogLinesVisibility =>
            IsAnalogModeActive ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;

        /// <summary>
        /// Verifica daca butoanele sunt apasate simultan si salveaza noul prag din Potentiometrul 2
        /// </summary>
        public void CheckAndSaveAnalogThresholds()
        {
            if (!IsAnalogModeActive) return;

            double targetPixelLevel = (_potentiometerVoltage2 / 10.0) * 260.0;

            if (IsS1Pressed && IsS3Pressed)
            {
                // S1 + S3 -> Setare prag Pompa 1
                _savedLevelB2 = Math.Max(LevelB1 + 10, Math.Min(LevelB3 - 20, targetPixelLevel));
                _isP1ThresholdSet = true;
                OnPropertyChanged(nameof(LevelB2));
                OnPropertyChanged(nameof(LevelB2Top));
            }
            else if (IsS1Pressed && IsS4Pressed)
            {
                // S1 + S4 -> Setare prag Pompa 2
                double minB5 = _savedLevelB2 + 20;
                _savedLevelB5 = Math.Max(minB5, Math.Min(LevelB3 - 10, targetPixelLevel));
                _isP2ThresholdSet = true;
                OnPropertyChanged(nameof(LevelB5));
                OnPropertyChanged(nameof(LevelB5Top));
            }
        }

        // ===== PROPRIETATI BINDING =====

        private bool _isPump1Running;
        public bool IsPump1Running
        {
            get => _isPump1Running;
            set
            {
                _isPump1Running = value;
                OnPropertyChanged(nameof(IsPump1Running));
                OnPropertyChanged(nameof(Pump1Visibility));
                OnPropertyChanged(nameof(Pump1HiddenVisibility));
            }
        }
        public System.Windows.Visibility Pump1Visibility =>
            _isPump1Running ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
        public System.Windows.Visibility Pump1HiddenVisibility =>
            _isPump1Running ? System.Windows.Visibility.Hidden : System.Windows.Visibility.Visible;

        private bool _isPump2Running;
        public bool IsPump2Running
        {
            get => _isPump2Running;
            set
            {
                _isPump2Running = value;
                OnPropertyChanged(nameof(IsPump2Running));
                OnPropertyChanged(nameof(Pump2Visibility));
                OnPropertyChanged(nameof(Pump2HiddenVisibility));
            }
        }
        public System.Windows.Visibility Pump2Visibility =>
            _isPump2Running ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
        public System.Windows.Visibility Pump2HiddenVisibility =>
            _isPump2Running ? System.Windows.Visibility.Hidden : System.Windows.Visibility.Visible;

        private bool _isAlarmOn;
        public bool IsAlarmOn
        {
            get => _isAlarmOn;
            set
            {
                _isAlarmOn = value;
                OnPropertyChanged(nameof(IsAlarmOn));
                OnPropertyChanged(nameof(AlarmVisibility));
            }
        }
        public System.Windows.Visibility AlarmVisibility =>
            _isAlarmOn ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;

        private bool _isAlarmSounding;
        public bool IsAlarmSounding
        {
            get => _isAlarmSounding;
            set
            {
                _isAlarmSounding = value;
                OnPropertyChanged(nameof(IsAlarmSounding));
                OnPropertyChanged(nameof(AlarmSoundingVisibility));
            }
        }
        public System.Windows.Visibility AlarmSoundingVisibility =>
            _isAlarmSounding ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;

        private bool _isSystemOn;
        public bool IsSystemOn
        {
            get => _isSystemOn;
            set
            {
                _isSystemOn = value;
                OnPropertyChanged(nameof(IsSystemOn));
                OnPropertyChanged(nameof(SystemOnVisibility));
            }
        }
        public System.Windows.Visibility SystemOnVisibility =>
            _isSystemOn ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
    }
}