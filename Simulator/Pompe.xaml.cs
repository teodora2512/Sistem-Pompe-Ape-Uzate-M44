using DataModel;
using System.Diagnostics.Eventing.Reader;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            if (_viewModel.IsAnalogModeActive)
            {
               //Mod Analogic - Toggle
                if (!_viewModel.IsS3Pressed)
                {
                    // primul click: intra in reglaj
                    _viewModel.IsS3Pressed = true;
                    _viewModel.IsS1Pressed = true;
                    StatusLabel.Text = "REGLAJ P1: Rotiti U2, apoi apasati S3 din nou pentru salvare.";
                    ((Button)sender).Background = System.Windows.Media.Brushes.DarkOrange; ;
                }
                else
                {
                    // al doilea click: salveaza
                    _viewModel.CheckAndSaveAnalogThresholds();
                    _viewModel.IsS3Pressed = false;
                    _viewModel.IsS1Pressed = false;
                    StatusLabel.Text = $"Prag P1 salvat: {_viewModel.LevelB2:F0} px";
                    ((Button)sender).Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x1D, 0x4E, 0xD8));
                }
            }
            else
            {
                _viewModel.ForceNextState(ProcessState.TestPump1);
                StatusLabel.Text = "Status: Test Pompa 1 activ - 3 secunde...";
            }
           
        }

        // S4 - test manual P2, ruleaza 3s apoi revine automat
        private void Button_Click_S4_TestP2(object sender, System.Windows.RoutedEventArgs e)
        {

            // Daca modul analogic este activat, butonul devine MEMORARE prag
            if (_viewModel.IsAnalogModeActive)
            {
                //Mod Analogic - Toggle
                if (!_viewModel.IsS4Pressed)
                {
                    _viewModel.IsS4Pressed = true;
                    _viewModel.IsS1Pressed = true;
                    StatusLabel.Text = "REGLAJ P2: Rotiti U2, apoi apasati S4 din nou pentru salvare.";
                    ((Button)sender).Background = System.Windows.Media.Brushes.DarkOrange;
                
                }
                else
                {
                    _viewModel.CheckAndSaveAnalogThresholds();
                    _viewModel.IsS4Pressed = false;
                    _viewModel.IsS1Pressed = false;
                    StatusLabel.Text = $"Prag P2 salvat: {_viewModel.LevelB5:F0} px";
                    ((Button)sender).Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x1D, 0x4E, 0xD8));
                }
            }
            else
            {
                _viewModel.ForceNextState(ProcessState.TestPump2);
                StatusLabel.Text = "Status: Test Pompa 2 activ - 3 secunde...";
            }
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

        // === POTENTIOMETRE - control prin drag rotativ ===

        private bool _isDraggingPot1 = false;
        private bool _isDraggingPot2 = false;

        private double AngleToVoltage(Point mousePos, Point center)
        {
            double dx = mousePos.X - center.X;
            double dy = mousePos.Y - center.Y;
            double angleDeg = System.Math.Atan2(dx, -dy) * (180.0 / System.Math.PI);
            angleDeg = System.Math.Max(-135, System.Math.Min(135, angleDeg));
            double voltage = (angleDeg + 135) / 270.0 * 10.0;
            return System.Math.Max(0, System.Math.Min(10, voltage));
        }

        private void Potentiometer1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingPot1 = true;
            ((UIElement)sender).CaptureMouse();
            var element = (FrameworkElement)sender;
            var center = new Point(element.ActualWidth / 2, element.ActualHeight / 2);
            _viewModel.PotentiometerVoltage1 = AngleToVoltage(e.GetPosition(element), center);
        }

        private void Potentiometer1_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingPot1 && e.LeftButton == MouseButtonState.Pressed)
            {
                var element = (FrameworkElement)sender;
                var center = new Point(element.ActualWidth / 2, element.ActualHeight / 2);
                _viewModel.PotentiometerVoltage1 = AngleToVoltage(e.GetPosition(element), center);
            }
        }

        private void Potentiometer1_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingPot1 = false;
            ((UIElement)sender).ReleaseMouseCapture();
        }

        private void Potentiometer2_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingPot2 = true;
            ((UIElement)sender).CaptureMouse();
            var element = (FrameworkElement)sender;
            var center = new Point(element.ActualWidth / 2, element.ActualHeight / 2);
            _viewModel.PotentiometerVoltage2 = AngleToVoltage(e.GetPosition(element), center);
        }

        private void Potentiometer2_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingPot2 && e.LeftButton == MouseButtonState.Pressed)
            {
                var element = (FrameworkElement)sender;
                var center = new Point(element.ActualWidth / 2, element.ActualHeight / 2);
                _viewModel.PotentiometerVoltage2 = AngleToVoltage(e.GetPosition(element), center);
            }
        }

        private void Potentiometer2_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingPot2 = false;
            ((UIElement)sender).ReleaseMouseCapture();
        }

        private void Potentiometer1_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double step = e.Delta > 0 ? 0.5 : -0.5;
            _viewModel.PotentiometerVoltage1 =
                System.Math.Max(0, System.Math.Min(10, _viewModel.PotentiometerVoltage1 + step));
            e.Handled = true;
        }

        private void Potentiometer2_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double step = e.Delta > 0 ? 0.5 : -0.5;
            _viewModel.PotentiometerVoltage2 =
                System.Math.Max(0, System.Math.Min(10, _viewModel.PotentiometerVoltage2 + step));
            e.Handled = true;
        }
    }
}