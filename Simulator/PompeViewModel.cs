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

        // Nivelurile senzorilor in pixeli (bazin = 260px total)
        // Canvas.Top corespunzator = 400 - LevelBx
        public const double LevelB1 = 30;   // nivel minim - oprire pompe
        public const double LevelB2 = 100;  // pornire P1
        public const double LevelB4 = 80;   // backup pentru B1
        public const double LevelB5 = 180;  // pornire P2
        public const double LevelB3 = 240;  // nivel critic - alarma

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
                    // Nicio pompa - apa creste continuu
                    WaterLevel += 0.5;
                    if (WaterLevel >= LevelB2)
                        ForceNextState(ProcessState.Pump1Running);
                    if (WaterLevel >= LevelB3)
                        TriggerAlarmB3();
                    break;

                case ProcessState.Pump1Running:
                    IsSystemOn = true;
                    IsPump1Running = true;
                    IsPump2Running = false;
                    IsAlarmOn = false;
                    if (_isHighInflowRate)
                    {
                        // Admisie mare: P1 nu face fata - nivel creste lent spre B5
                        WaterLevel += 0.2;
                        if (WaterLevel >= LevelB5)
                            ForceNextState(ProcessState.BothPumpsRunning);
                    }
                    else
                    {
                        // Caz normal: P1 evacueaza mai repede decat intra - nivel scade
                        WaterLevel -= 0.5;
                        if (WaterLevel <= EffectiveLevelMin)
                            ForceNextState(ProcessState.On_NoPump);
                    }
                    if (WaterLevel >= LevelB3)
                        TriggerAlarmB3();
                    break;

                case ProcessState.BothPumpsRunning:
                    IsSystemOn = true;
                    IsPump1Running = true;
                    IsPump2Running = true;
                    IsAlarmOn = false;
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

        // Forteaza imediat o stare noua, anulând orice tranzitie in asteptare
        public void ForceNextState(ProcessState nextState)
        {
            _isChangingStateInProgress = false;
            timer.Stop();
            TheStateOfTheProcess = nextState;
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