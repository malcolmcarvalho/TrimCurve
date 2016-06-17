using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace TrimCurveApp
{
    class PowerSavingsForSpeed
    {
        public List<string> Savings { get; set; }
        public PowerSavingsForSpeed()
        {
            Savings = new List<string>();
        }
    }

    class MainWindowViewModel : INotifyPropertyChanged
    {
        private static string INVALID_RANGE_MESSAGE = "Draft or speed values provided are not within range. Cannot redraw the graphs.";
        private static string TRIM = "Trim";
        private static string POWER_USAGE = "Power usage (kW)";
        private static string POWER_SAVINGS_PERCENTAGE = "Relative power savings %";
        private static string ABSOLUTE_POWER_USAGE = "Absolute power usage";
        private static string POWER_SAVINGS = "Power savings";
        private static string SPEED_IN_KNOTS = "Speed (Knots)";
        private static string FUEL_CONSUMPTION = "Fuel Consumption (Tons per day)";

        public TrimCurveOxyplotModel AbsolutePowerUsagePlotModel { get; private set; }
        public TrimCurveOxyplotModel PowerSavingsPlotModel { get; private set; }
        public TrimCurveOxyplotModel SFOCPlotModel { get; private set; }

        public double Draft { get; set; }
        public double Speed { get; set; }

        public List<PowerConsumptionRecord> PowerRecords { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private ObservableCollection<PowerSavingsForSpeed> _powerSavingsForSpeedColl;
        public ObservableCollection<PowerSavingsForSpeed> PowerSavingsForSpeedColl
        {
            get { return _powerSavingsForSpeedColl; }
            set
            {
                if (_powerSavingsForSpeedColl != value) {
                    _powerSavingsForSpeedColl = value;
                    OnPropertyChanged("PowerSavingsForSpeedColl");
                }
            }
        }

        private ObservableCollection<string> _headersList;
        public ObservableCollection<string> HeadersList
        {
            get { return _headersList; }
            set
            {
                if (_headersList != value)
                {
                    _headersList = value;
                    OnPropertyChanged("HeadersList");
                }
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainWindowViewModel()
        {
            Draft = 25;
            Speed = 18;
            AbsolutePowerUsagePlotModel = new TrimCurveOxyplotModel { Title = ABSOLUTE_POWER_USAGE };
            PowerSavingsPlotModel = new TrimCurveOxyplotModel { Title = POWER_SAVINGS };
            SFOCPlotModel = new TrimCurveOxyplotModel { Title = "SFOC" };

            PowerRecords = ExcelFileDataExtractor.ReadPowerValuesFromXLS();
            ReadSFOCValuesFromXLS();
            //UpdatePowerGraphs();
        }

        public void UpdatePowerGraphs()
        {
            ResetPlotModels();
            var filteredPowerRecords = PowerRecords.Where(x => x.Draft == Draft && x.Speed == Speed);

            var puPoints = new List<DataPoint>();
            var psPoints = new List<DataPoint>();

            if (filteredPowerRecords.Any())
            {
                foreach (var powerRec in filteredPowerRecords)
                {
                    puPoints.Add(new DataPoint(powerRec.Trim, powerRec.Power));
                    psPoints.Add(new DataPoint(powerRec.Trim, powerRec.PowerSavings));
                }
            }
            else
            {
                var draftMatches = PowerRecords.Where(x => x.Draft == Draft);
                var speedMatches = PowerRecords.Where(x => x.Speed == Speed);
                if (draftMatches.Any())
                    InterpolateGraphPointsForMissingSpeed(draftMatches, psPoints, puPoints);
                else if (speedMatches.Any())
                    InterpolateGraphPointsForMissingDraft(speedMatches, psPoints, puPoints);
                else
                    InterpolateGraphPointsFroMissingDraftAndSpeed(psPoints, puPoints);
            }

            if (psPoints.Any() && puPoints.Any())
            {
                PowerSavingsPlotModel.UpdateGraph(psPoints, TRIM, POWER_SAVINGS_PERCENTAGE);
                AbsolutePowerUsagePlotModel.UpdateGraph(puPoints, TRIM, POWER_USAGE);
                AddBackgroundColorsToPowerSavingsGraph();
            }
        }

        private void InterpolateGraphPointsForMissingSpeed(
            IEnumerable<PowerConsumptionRecord> draftMatches,
            IList<DataPoint> psPoints,
            IList<DataPoint> puPoints)
        {
            var lowerRecords = GetPrevSpeedRecords(draftMatches);
            var upperRecords = GetNextSpeedRecords(draftMatches);
            if (!lowerRecords.Any() || !upperRecords.Any())
            {
                MessageBox.Show(INVALID_RANGE_MESSAGE);
                return;
            }

            var lowerSpeed = lowerRecords.FirstOrDefault().Speed;
            var upperSpeed = upperRecords.FirstOrDefault().Speed;
            var wtFunction = (Speed - lowerSpeed) / (upperSpeed - lowerSpeed);
            GenerateGraphPoints(lowerRecords, upperRecords, psPoints, puPoints, wtFunction);
        }

        private void InterpolateGraphPointsForMissingDraft(
            IEnumerable<PowerConsumptionRecord> speedMatches,
            IList<DataPoint> psPoints,
            IList<DataPoint> puPoints)
        {
            var lowerRecords = GetPrevDraftRecords(speedMatches);
            var upperRecords = GetNextDraftRecords(speedMatches);
            if (!lowerRecords.Any() || !upperRecords.Any())
            {
                MessageBox.Show(INVALID_RANGE_MESSAGE);
                return;
            }

            var lowerDraft = lowerRecords.FirstOrDefault().Draft;
            var upperDraft = upperRecords.FirstOrDefault().Draft;
            var wtFunction = (Draft - lowerDraft) / (upperDraft - lowerDraft);
            GenerateGraphPoints(lowerRecords, upperRecords, psPoints, puPoints, wtFunction);
        }

        private void InterpolateGraphPointsFroMissingDraftAndSpeed(
            IList<DataPoint> psPoints,
            IList<DataPoint> puPoints)
        {
            var lowerSpeedRecords = GetPrevSpeedRecords(PowerRecords);
            var upperSpeedRecords = GetNextSpeedRecords(PowerRecords);
            if (!lowerSpeedRecords.Any() || !upperSpeedRecords.Any())
            {
                MessageBox.Show(INVALID_RANGE_MESSAGE);
                return;
            }

            var leftLowerRecords = GetPrevDraftRecords(lowerSpeedRecords);
            var leftUpperRecords = GetNextDraftRecords(lowerSpeedRecords);
            var rightLowerRecords = GetPrevDraftRecords(upperSpeedRecords);
            var rightUpperRecords = GetNextDraftRecords(upperSpeedRecords);
            GenerateGraphPoints(leftLowerRecords, leftUpperRecords, rightLowerRecords, rightUpperRecords, psPoints, puPoints);
        }

        private OxyImage GetGradientImage(OxyColor color1, OxyColor color2)
        {
            int n = 256;
            var imageData = new OxyColor[1, n];
            for (int i = 0; i < n; i++)
            {
                imageData[0, i] = OxyColor.Interpolate(color1, color2, i / (n - 1.0));
            }

            var encoder = new PngEncoder(new PngEncoderOptions());
            return new OxyImage(encoder.Encode(imageData));
        }

        private void AddBackgroundGradient(Axis xAxis, double yStart, double yEnd, OxyColor color1, OxyColor color2)
        {
            var image = GetGradientImage(color1, color2);
            var colorAnnotation = new ImageAnnotation
            {
                ImageSource = image,
                Interpolate = true,
                Layer = AnnotationLayer.BelowAxes,
                X = new PlotLength(xAxis.ActualMinimum, PlotLengthUnit.Data),
                Y = new PlotLength(yStart, PlotLengthUnit.Data),
                Width = new PlotLength(xAxis.ActualMaximum - xAxis.ActualMinimum, PlotLengthUnit.Data),
                Height = new PlotLength(Math.Abs(yEnd - yStart), PlotLengthUnit.Data),
                HorizontalAlignment = OxyPlot.HorizontalAlignment.Left,
                VerticalAlignment = OxyPlot.VerticalAlignment.Bottom
            };
            PowerSavingsPlotModel.Annotations.Add(colorAnnotation);
        }

        private void AddBackgroundColorsToPowerSavingsGraph()
        {
            var lineSeries = PowerSavingsPlotModel.Series.ElementAt(0) as LineSeries;
            var points = lineSeries.ItemsSource as IEnumerable<DataPoint>;
            var xAxis = PowerSavingsPlotModel.Axes.Where(x => x.Title == TRIM).First();
            
            var yMin = points.Min(p => p.Y);
            var yMax = points.Max(p => p.Y);

            AddBackgroundGradient(xAxis, yMin, 0, OxyColors.LightPink, OxyColors.Red);
            AddBackgroundGradient(xAxis, 0, yMax, OxyColors.Green, OxyColors.GreenYellow);
        }

        private void ResetPlotModels() {
            AbsolutePowerUsagePlotModel.Reset();
            PowerSavingsPlotModel.Reset();
        }

        private void GenerateGraphPoints(
            IEnumerable<PowerConsumptionRecord> lowerRecords,
            IEnumerable<PowerConsumptionRecord> upperRecords,
            IList<DataPoint> psPoints,
            IList<DataPoint> puPoints,
            double wtFunction)
        {
            if (!lowerRecords.Any() || !upperRecords.Any())
            {
                MessageBox.Show(INVALID_RANGE_MESSAGE);
                return;
            }

            Debug.Assert(lowerRecords.Count() == upperRecords.Count());
            foreach (var rec in lowerRecords)
            {
                var upperMatch = upperRecords.Where(x => x.Trim == rec.Trim).FirstOrDefault();
                var newPower = rec.Power + wtFunction * (upperMatch.Power - rec.Power);
                var newPowerSavings = rec.PowerSavings + wtFunction * (upperMatch.PowerSavings - rec.PowerSavings);
                puPoints.Add(new DataPoint(rec.Trim, newPower));
                psPoints.Add(new DataPoint(rec.Trim, newPowerSavings));
            }
        }

        private void GenerateGraphPoints(
            IEnumerable<PowerConsumptionRecord> leftLowerRecords,
            IEnumerable<PowerConsumptionRecord> leftUpperRecords,
            IEnumerable<PowerConsumptionRecord> rightLowerRecords,
            IEnumerable<PowerConsumptionRecord> rightUpperRecords,
            IList<DataPoint> psPoints, IList<DataPoint> puPoints)
        {
            Debug.Assert(leftLowerRecords.Count() == leftUpperRecords.Count());
            Debug.Assert(rightLowerRecords.Count() == rightUpperRecords.Count());
            Debug.Assert(leftLowerRecords.Count() == rightLowerRecords.Count());

            if (!leftLowerRecords.Any() || !leftUpperRecords.Any() || !rightLowerRecords.Any() || !rightUpperRecords.Any())
            {
                MessageBox.Show(INVALID_RANGE_MESSAGE);
                return;
            }

            var lowerSpeed = leftLowerRecords.FirstOrDefault().Speed;
            var upperSpeed = rightLowerRecords.FirstOrDefault().Speed;
            var speedWtFunction = (Speed - lowerSpeed) / (upperSpeed - lowerSpeed);

            var lowerDraft = leftLowerRecords.FirstOrDefault().Draft;
            var upperDraft = leftUpperRecords.FirstOrDefault().Draft;
            var draftWtFunction = (Draft - lowerDraft) / (upperDraft - lowerDraft);

            foreach (var rec in leftLowerRecords)
            {
                var rightLowerMatch = rightLowerRecords.Where(x => x.Trim == rec.Trim).FirstOrDefault();
                var leftUpperMatch = leftUpperRecords.Where(x => x.Trim == rec.Trim).FirstOrDefault();
                var rightUpperMatch = rightUpperRecords.Where(x => x.Trim == rec.Trim).FirstOrDefault();

                // power consumption
                var avg1 = rec.Power + speedWtFunction * (rightLowerMatch.Power - rec.Power);
                var avg2 = leftUpperMatch.Power + speedWtFunction * (rightUpperMatch.Power - leftUpperMatch.Power);
                var finalAvg = avg1 + draftWtFunction * (avg2 - avg1);
                puPoints.Add(new DataPoint(rec.Trim, finalAvg));

                // power savings
                avg1 = rec.PowerSavings + speedWtFunction * (rightLowerMatch.PowerSavings - rec.PowerSavings);
                avg2 = leftUpperMatch.PowerSavings + speedWtFunction * (rightUpperMatch.PowerSavings - leftUpperMatch.PowerSavings);
                finalAvg = avg1 + draftWtFunction * (avg2 - avg1);
                psPoints.Add(new DataPoint(rec.Trim, finalAvg));
            }
        }

        private IEnumerable<PowerConsumptionRecord> GetPrevSpeedRecords(IEnumerable<PowerConsumptionRecord> records)
        {
            var lowerGroup = records.Where(x => x.Speed < Speed);
            return lowerGroup.Where(x => x.Speed == lowerGroup.Max<PowerConsumptionRecord>(rec => rec.Speed))
                             .OrderBy(x => x.Trim);
        }

        private IEnumerable<PowerConsumptionRecord> GetNextSpeedRecords(IEnumerable<PowerConsumptionRecord> records)
        {
            var upperGroup = records.Where(x => x.Speed > Speed);
            return upperGroup.Where(x => x.Speed == upperGroup.Min<PowerConsumptionRecord>(rec => rec.Speed))
                             .OrderBy(x => x.Trim);
        }

        private IEnumerable<PowerConsumptionRecord> GetPrevDraftRecords(IEnumerable<PowerConsumptionRecord> records)
        {
            var lowerGroup = records.Where(x => x.Draft < Draft);
            return lowerGroup.Where(x => x.Draft == lowerGroup.Max<PowerConsumptionRecord>(rec => rec.Draft))
                             .OrderBy(x => x.Trim);
        }

        private IEnumerable<PowerConsumptionRecord> GetNextDraftRecords(IEnumerable<PowerConsumptionRecord> records)
        {
            var lowerGroup = records.Where(x => x.Draft > Draft);
            return lowerGroup.Where(x => x.Draft == lowerGroup.Min<PowerConsumptionRecord>(rec => rec.Draft))
                             .OrderBy(x => x.Trim);
        }

        private void ReadSFOCValuesFromXLS() {
            var sfocPoints = ExcelFileDataExtractor.ReadSFOCValuesFromXLS();
            SFOCPlotModel.UpdateGraph(sfocPoints, SPEED_IN_KNOTS, FUEL_CONSUMPTION);
        }

        public void UpdateSpeedPowerSavingsColl(double meanDraft) {
            var records = PowerRecords.Where(rec => rec.Draft == meanDraft);
            if (!records.Any())
            {
                MessageBox.Show("No data exists for given draft.");
                return;
            }

            var speeds = records.Select(x => x.Speed).Distinct().OrderBy(y => y);
            var speedList = new ObservableCollection<PowerSavingsForSpeed>();
            foreach (var speed in speeds)
            {
                var speedRow = new PowerSavingsForSpeed();
                var filteredRecords = records.Where(x => x.Speed == speed).OrderBy(x => x.Trim);
                foreach (var rec in filteredRecords)
                    speedRow.Savings.Add(String.Format("{0:N2}", rec.PowerSavings));
                speedList.Add(speedRow);
            }
            PowerSavingsForSpeedColl = speedList;
            var trims = records.Select(x => x.Trim).Distinct().OrderBy(x => x);
            var headerlist = new ObservableCollection<string>();
            foreach (var item in trims)
            {
                headerlist.Add(String.Format("{0:N2}", item));
            }
            HeadersList = headerlist;
        }
    }
}