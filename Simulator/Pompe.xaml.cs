using DataModel;
using System.Windows.Controls;

namespace Simulator
{
    public partial class Pompe : UserControl
    {
        private PompeViewModel _viewModel;

        public Pompe()
        {
            InitializeComponent();
            // Cream ViewModel-ul si il setam ca sursa de date pentru binding
            _viewModel = new PompeViewModel();
            this.DataContext = _viewModel;
            _viewModel.Init();
        }

        // === COMENZI PRINCIPALE ===

        // S1 - pornire sistem, apa incepe sa creasca spre B2
        private void Button_Click_S1_On(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.ForceNextState(ProcessState.On_NoPump);
            StatusLabel.Text = "Status: Sistem pornit. Apa creste spre B2...";
        }

        // S0 - oprire completa, reseteaza si flagurile de simulare
        private void Button_Click_S0_Off(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.ForceNextState(ProcessState.Off);
            _viewModel.IsHighInflowRate = false;
            _viewModel.IsB1Defect = false;
            StatusLabel.Text = "Status: Sistem oprit.";
        }

        // === TEST POMPE ===

        // S3 - test manual P1, ruleaza 3s apoi revine automat
        private void Button_Click_S3_TestP1(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.ForceNextState(ProcessState.TestPump1);
            StatusLabel.Text = "Status: Test Pompa 1 activ - 3 secunde...";
        }

        // S4 - test manual P2, ruleaza 3s apoi revine automat
        private void Button_Click_S4_TestP2(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.ForceNextState(ProcessState.TestPump2);
            StatusLabel.Text = "Status: Test Pompa 2 activ - 3 secunde...";
        }

        // === RESET ===

        // S5 - reseteaza alarma si opreste sistemul complet
        private void Button_Click_S5_Reset(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.ForceNextState(ProcessState.Off);
            _viewModel.IsHighInflowRate = false;
            _viewModel.IsB1Defect = false;
            StatusLabel.Text = "Status: Alarma resetata. Sistem oprit.";
        }

        // === SIMULARI ===

        // Activeaza rata mare de admisie - P1 nu face fata, nivel urca spre B5
        private void Button_Click_HighInflow(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.IsHighInflowRate = true;
            StatusLabel.Text = "Simulare: Rata admisie MARE - apa urca spre B5, pornire P2...";
        }

        // Dezactiveaza rata mare - P1 face fata singur, ciclu normal B1-B2
        private void Button_Click_NormalInflow(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.IsHighInflowRate = false;
            StatusLabel.Text = "Status: Rata admisie normala - P1 face fata singur (B1-B2).";
        }

        // Simuleaza B1 defect - sistemul comuta pe B4 ca nivel minim
        private void Button_Click_B1Defect(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.IsB1Defect = true;
            StatusLabel.Text = "Simulare: B1 DEFECT - sistem foloseste B4 ca nivel minim...";
        }

        // Reseteaza B1 la stare normala
        private void Button_Click_B1Normal(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.IsB1Defect = false;
            StatusLabel.Text = "Status: B1 functional - oprire pompe la nivel normal.";
        }

        // Simuleaza releu protectie declansat - pompe oprite, apa urca liber spre B3
        private void Button_Click_SimulateAlarm(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.ForceNextState(ProcessState.Alarm);
            StatusLabel.Text = "Simulare: Releu protectie! Pompe oprite - apa urca spre B3...";
        }
    }
}