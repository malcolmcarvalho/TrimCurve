using System;
using System.Windows;

namespace GreenFLOAT {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
        }

        private void UpdatePowerGraphs_Click(object sender, RoutedEventArgs e) {
            var vm = DataContext as MainWindowViewModel;
            vm.UpdatePowerGraphs();

            AbsolutePowerGraph.InvalidatePlot();
            PowerSavingsGraph.InvalidatePlot();
            SFOCGraph.InvalidatePlot();
            DraftAtAftBarGraph.InvalidatePlot();
            DraftAtFwdBarGraph.InvalidatePlot();

            ActualTrimTextBlock.Text = Convert.ToString(vm.DraftAtAft);
            OptimalTrimTextBlock.Text = Convert.ToString(vm.DraftAtFwd);
        }

        private void ShowTrimCurve_Click(object sender, RoutedEventArgs e) {
            double meanDraft;
            if (!Double.TryParse(MeanDraftText.Text, out meanDraft)) {
                MessageBox.Show("Mean draft is not valid.");
                return;
            }

            var vm = DataContext as MainWindowViewModel;
            vm.UpdateSpeedPowerSavingsColl(meanDraft);
        }
    }
}
