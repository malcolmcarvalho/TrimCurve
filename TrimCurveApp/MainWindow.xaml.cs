using System.Windows;

namespace TrimCurveApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void UpdatePowerGraphs_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainWindowViewModel;
            vm.UpdatePowerGraphs();

            AbsolutePowerGraph.InvalidatePlot();
            PowerSavingsGraph.InvalidatePlot();
        }
    }
}
