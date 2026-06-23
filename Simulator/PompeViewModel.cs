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

        private double _potentiometerVoltage1 = 1.0; // Rata de incarcare (default: 1.0V)

        // Calculeaza automat unghiul pentru Potentiometrul 1 (36 de grade per Volt)
        public double Potentiometer1Angle =>
    -135 + (PotentiometerVoltage1 / 10.0) * 270;
        public double PotentiometerVoltage1
        {
            get => _potentiometerVoltage1;
            set
            {
                _potentiometerVoltage1 = Math.Max(0.0, Math.Min(10.0, value)); // Limiteaza intre 0 si 10V
                OnPropertyChanged(nameof(PotentiometerVoltage1));
                OnPropertyChanged(nameof(Potentiometer1Angle));
            }
        }

        private double _potentiometerVoltage2 = 0.0; //Reglare praguri
                                                     // Calculeaza automat unghiul pentru Potentiometrul 2
        public double Potentiometer2Angle =>
    -135 + (PotentiometerVoltage2 / 10.0) * 270;
        public double PotentiometerVoltage2
        {
            get => _potentiometerVoltage2;
            set
            {
                _potentiometerVoltage2 = Math.Max(0.0, Math.Min(10.0, value)); // Limiteaza intre 0 si 10V
                OnPropertyChanged(nameof(PotentiometerVoltage2));
                OnPropertyChanged(nameof(Potentiometer2Angle));

                OnPropertyChanged(nameof(Pot2TargetPixelLevel));
                OnPropertyChanged(nameof(Pot2TargetTop));

                CheckAndSaveAnalogThresholds();
            }
        }

      

        private double _savedLevelB2 = 100.0; // P1 Start (implicit 100)
        private double _savedLevelB5 = 180.0; // P2 Start (implicit 180)

        // --- IESIRI ANALOGICE (0-10V pentru export) ---
        public double AnalogOutput1_WaterLevelVoltage => (WaterLevel / 260.0) * 10.0;
        public double AnalogOutput2_SetPointVoltage => (TheStateOfTheProcess == ProcessState.BothPumpsRunning)
            ? (_savedLevelB5 / 260.0) * 10.0
            : (_savedLevelB2 / 260.0) * 10.0;

        // Nivelurile senzorilor in pixeli (bazin = 260px total)
        // Canvas.Top corespunzator = 400 - LevelBx
        public double LevelB1 = 30;  // nivel minim - oprire pompe
        public  double LevelB2 =>IsAnalogModeActive ? _savedLevelB2:100.0;  // pornire P1
        public double LevelB4 = 80;   // backup pentru B1
        public double LevelB5 => IsAnalogModeActive ?_savedLevelB5 : 180.0;  // pornire P2
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

        // Apelat cand expirat timerul - efectueaza tranzitia planificata
        private void _timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _isChangingStateInProgress = false;
            TheStateOfTheProcess = _nextState;
            timer.Stop();
        }

        // Planifica o tranzitie dupa un interval fix (ms)
        // Ignora apelurile daca o tranzitie e deja in asteptare
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

        // Ruleaza la infinit pe thread-ul BackgroundWorker
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
                // Limiteaza intre 0 si 260px (dimensiunea bazinului)
                _waterLevel = Math.Max(0, Math.Min(260, value));
                OnPropertyChanged(nameof(WaterLevel));
                // WaterTop se recalculeaza automat din WaterLevel
                OnPropertyChanged(nameof(WaterTop));

                // Notificăm PLC-ul că s-a schimbat tensiunea pe ieșirile analogice
                OnPropertyChanged(nameof(AnalogOutput1_WaterLevelVoltage));
                OnPropertyChanged(nameof(AnalogOutput2_SetPointVoltage));
            }
        }

        // Canvas.Top al dreptunghiului de apa - scade cand apa urca
        public double WaterTop => 400 - _waterLevel;

        // ===== FLAGS SIMULARE =====

        private bool _isB1Defect = false;
        private bool _isHighInflowRate = false;

        // Simuleaza defectarea senzorului B1 - sistemul va folosi B4
        public bool IsB1Defect
        {
            get => _isB1Defect;
            set { _isB1Defect = value; OnPropertyChanged(nameof(IsB1Defect)); }
        }

        // Simuleaza rata de admisie mare - P1 nu mai face fata singur
        public bool IsHighInflowRate
        {
            get => _isHighInflowRate;
            set { _isHighInflowRate = value; OnPropertyChanged(nameof(IsHighInflowRate)); }
        }

        // Returneaza nivelul minim activ: B1 normal sau B4 daca B1 e defect
        private double EffectiveLevelMin => _isB1Defect ? LevelB4 : LevelB1;

        // ===== LOGICA PROCESULUI =====

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

                        WaterLevel += (PotentiometerVoltage1 * 0.1); // Crește conform debitului setat de U1

                        if (WaterLevel >= _savedLevelB2)
                            ForceNextState(ProcessState.Pump1Running);
                        if (WaterLevel >= _savedLevelB5)
                            ForceNextState(ProcessState.BothPumpsRunning);


                    }
                    else
                    {
                        // Nicio pompa - apa creste continuu
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

                        if (_isHighInflowRate)
                        {
                            //// Dacă admisia e MARE, multiplicăm efectul potențiometrului 1
                            WaterLevel += -0.5 + (PotentiometerVoltage1 * 0.15);
                        }
                        else
                        {
                            // Caz analogic normal
                            WaterLevel += -0.5 + (PotentiometerVoltage1 * 0.05);
                        }
                        if (WaterLevel >= _savedLevelB5)
                            ForceNextState(ProcessState.BothPumpsRunning);
                    }
                    else
                    {
                        // --- LOGICA IMPLICITA (CAND MODUL ANALOGIC E DEZACTIVAT) ---
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

                    // conditia de oprire 
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
                        WaterLevel += -0.8 + (PotentiometerVoltage1 * 0.05);
                    else
                        // Ambele pompe - nivel scade rapid
                        WaterLevel -= 0.8;

                    if (WaterLevel <= EffectiveLevelMin)
                        ForceNextState(ProcessState.On_NoPump);
                    break;

                case ProcessState.AlarmB3:
                    // Nivel critic: ambele pompe pornite fortat pana sub B1
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
                        WaterLevel += (PotentiometerVoltage1 * 0.05);
                    else
                        WaterLevel += 0.3;

                    if (WaterLevel >= LevelB3)
                        TriggerAlarmB3();
                    break;

                case ProcessState.TestPump1:
                    // Test manual P1 - ruleaza 3 secunde apoi revine
                    IsSystemOn = true;
                    IsPump1Running = true;
                    IsPump2Running = false;
                    IsAlarmOn = false;
                    WaterLevel -= 0.3;
                    ChangeProcessState(ProcessState.On_NoPump, 3000);
                    break;

                case ProcessState.TestPump2:
                    // Test manual P2 - ruleaza 3 secunde apoi revine
                    IsSystemOn = true;
                    IsPump1Running = false;
                    IsPump2Running = true;
                    IsAlarmOn = false;
                    WaterLevel -= 0.3;
                    ChangeProcessState(ProcessState.On_NoPump, 3000);
                    break;
            }
        }

        // Declanseaza alarma de nivel critic B3
        private void TriggerAlarmB3()
        {
            IsAlarmOn = true;
            IsAlarmSounding = true;
            alarmTimer.Start(); // alarma sonora se opreste dupa 5s
            TheStateOfTheProcess = ProcessState.AlarmB3;
        }

        // Forteaza imediat o stfare noua, anulând orice tranzitie in asteptare
        public void ForceNextState(ProcessState nextState)
        {
            _isChangingStateInProgress = false;
            timer.Stop();
            TheStateOfTheProcess = nextState;
        }

        // ===== LOGICA MOD ANALOGIC (FUNCTIONALITATEA 2) =====

        // Flags pentru a ști dacă butoanele de pe interfață sunt ținute apăsate
        public bool IsS1Pressed { get; set; } = false;
        public bool IsS3Pressed { get; set; } = false;
        public bool IsS4Pressed { get; set; } = false;

        private bool _isP1ThresholdSet = false;
        private bool _isP2ThresholdSet = false;

        // Canvas.Top pentru liniile analogice (400 - nivel salvat)
        public double LevelB2Top => 400 - _savedLevelB2;
        public double LevelB5Top => 400 - _savedLevelB5;

        public double Pot2TargetPixelLevel => (PotentiometerVoltage2 / 10.0) * 260.0;
        public double Pot2TargetTop => 400 - Pot2TargetPixelLevel;

        // Vizibilitatea liniilor analogice - apar doar cand modul e activ
        public System.Windows.Visibility AnalogLinesVisibility =>
            IsAnalogModeActive ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;

        /// <summary>
        /// Verifica dacă butoanele sunt apasate simultan și salveaza noul prag din Potentiometrul 2
        /// </summary>
        public void CheckAndSaveAnalogThresholds()
        {
            if (!IsAnalogModeActive) return;

            // Convertim tensiunea de pe Potențiometrul 2 (0-10V) în nivel de pixeli (0-260px)
            double targetPixelLevel = (_potentiometerVoltage2 / 10.0) * 260.0;

            if (IsS1Pressed && IsS3Pressed)
            {
                // S1 + S3 -> Setare Pompa 1
                _savedLevelB2 = Math.Max(LevelB1 + 10, Math.Min(LevelB3 - 20, targetPixelLevel));
                _isP1ThresholdSet = true;
                OnPropertyChanged(nameof(LevelB2));
                OnPropertyChanged(nameof(LevelB2Top));
            }
            else if (IsS1Pressed && IsS4Pressed)
            {
                // S1 + S4 -> Setare Pompa 2
                double minB5 = _savedLevelB2 + 20;
                _savedLevelB5 = Math.Max(minB5, Math.Min(LevelB3 - 10, targetPixelLevel));
                _isP2ThresholdSet = true;
                OnPropertyChanged(nameof(LevelB5));
                OnPropertyChanged(nameof(LevelB5Top));
            }
        }

        // ===== PROPRIETATI BINDING =====
        // Fiecare proprietate bool are o pereche Visibility pentru XAML
        // OnPropertyChanged notifica UI-ul sa se actualizeze

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
        // Inversa - pentru indicatorul gri cand pompa e oprita
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
        // Vizibil doar in primele 5s de la declansare
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