using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Excel = Microsoft.Office.Interop.Excel;

namespace TrimCurveApp
{
    class MainWindowViewModel
    {
        private static OxyColor LINE_SERIES_COLOR = OxyColor.Parse("#FF0000FF");

        private static string INVALID_RANGE_MESSAGE = "Draft or speed values provided are not within range. Cannot redraw the graphs.";
        private static string TRIM = "Trim";
        private static string POWER_USAGE = "Power usage (kW)";
        private static string POWER_SAVINGS_PERCENTAGE = "Relative power savings %";
        private static string ABSOLUTE_POWER_USAGE = "Absolute power usage";
        private static string POWER_SAVINGS = "Power savings";

        public PlotModel AbsolutePowerUsagePlotModel { get; private set; }
        public PlotModel PowerSavingsPlotModel { get; private set; }
        public double Draft { get; set; }
        public double Speed { get; set; }

        public List<PowerConsumptionRecord> PowerRecords = new List<PowerConsumptionRecord>();

        public MainWindowViewModel()
        {
            Draft = 25;
            Speed = 18;
            ReadPowerValuesFromXLS();
            AbsolutePowerUsagePlotModel = new PlotModel { Title = ABSOLUTE_POWER_USAGE };
            PowerSavingsPlotModel = new PlotModel { Title = POWER_SAVINGS };
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
                UpdateGraph(psPoints, PowerSavingsPlotModel, TRIM, POWER_SAVINGS_PERCENTAGE);
                UpdateGraph(puPoints, AbsolutePowerUsagePlotModel, TRIM, POWER_USAGE);
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

        private void UpdateGraph(
            IEnumerable<DataPoint> points,
            PlotModel plotModel,
            string xAxis,
            string yAxis)
        {
            var lineSeries = new LineSeries();
            lineSeries.Smooth = true;
            lineSeries.ItemsSource = points;
            lineSeries.MarkerType = MarkerType.Circle;
            lineSeries.MarkerFill = LINE_SERIES_COLOR;
            lineSeries.Color = LINE_SERIES_COLOR;
            plotModel.Series.Add(lineSeries);

            plotModel.PlotAreaBackground = OxyColor.FromArgb(255, 255, 255, 255);
            SetPlotModelAxes(plotModel, points, xAxis, yAxis);
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
            AbsolutePowerUsagePlotModel.Series.Clear();
            AbsolutePowerUsagePlotModel.Axes.Clear();
            AbsolutePowerUsagePlotModel.Annotations.Clear();

            PowerSavingsPlotModel.Series.Clear();
            PowerSavingsPlotModel.Axes.Clear();
            PowerSavingsPlotModel.Annotations.Clear();
        }

        private void SetPlotModelAxes(
            PlotModel plotModel,
            IEnumerable<DataPoint> seriesPoints,
            string xAxisTitle, string yAxisTitle)
        {
            double minXVal = seriesPoints.Min<DataPoint>(dp => dp.X);
            double maxXVal = seriesPoints.Max<DataPoint>(dp => dp.X);
            double minYVal = seriesPoints.Min<DataPoint>(dp => dp.Y);
            double maxYVal = seriesPoints.Max<DataPoint>(dp => dp.Y);

            plotModel.PlotType = PlotType.XY;
            SetXAxisForPlotModel(plotModel, minXVal, maxXVal, xAxisTitle);
            SetYAxisForPlotModel(plotModel, minYVal, maxYVal, yAxisTitle);
        }

        private LinearAxis CreateAxisForPlotModel(PlotModel plotModel, double minVal, double maxVal, string title, bool isXAxis)
        {
            var axis = new LinearAxis();
            const double offset = 0.1;
            double range = maxVal - minVal;
            axis.AbsoluteMinimum = minVal - offset * range;
            axis.AbsoluteMaximum = maxVal + offset * range;
            axis.Position = isXAxis ? AxisPosition.Bottom : AxisPosition.Left;
            axis.Title = title;
            axis.Zoom(axis.AbsoluteMinimum, axis.AbsoluteMaximum);
            axis.IsZoomEnabled = false;
            axis.MajorGridlineStyle = LineStyle.Solid;
            axis.MinorGridlineStyle = LineStyle.Dot;
            return axis;
        }

        private void SetXAxisForPlotModel(PlotModel plotModel, double minVal, double maxVal, string title)
        {
            var xAxis = CreateAxisForPlotModel(plotModel, minVal, maxVal, title, true);
            xAxis.MajorStep = 1;
            xAxis.MinorStep = 0.2;
            plotModel.Axes.Add(xAxis);
        }

        private void SetYAxisForPlotModel(PlotModel plotModel, double minVal, double maxVal, string title)
        {
            var yAxis = CreateAxisForPlotModel(plotModel, minVal, maxVal, title, false);
            plotModel.Axes.Add(yAxis);
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

        private void ReadPowerValuesFromXLS()
        {
            var xlApp = new Excel.Application();
            var xlWorkbook = xlApp.Workbooks.Open(
                @"C:\Malcolm\GreenOptilfoat\TrimCurve\Data\TrimCurveModifiedSample.xlsx",
                0, true, 5, "", "", true,
                Microsoft.Office.Interop.Excel.XlPlatform.xlWindows,
                "\t", false, false, 0, true, 1, 0);
            var xlWorksheet = xlApp.Worksheets.get_Item(1);
            var range = xlWorksheet.UsedRange;

            // temp workaround for adding speed
            // TODO: Change this later
            var speedMap = new Dictionary<int, int>();
            speedMap.Add(0, 13);
            speedMap.Add(1, 16);
            speedMap.Add(2, 18);
            speedMap.Add(3, 20);

            for (int rCnt = 5; rCnt <= range.Rows.Count; rCnt++)
            {
                double draft = (double)(range.Cells[rCnt, 1] as Excel.Range).Value2;
                double trim = (double)(range.Cells[rCnt, 2] as Excel.Range).Value2;

                for (int i = 0; i < 4; i++)
                {
                    var powerUsage = (double)(range.Cells[rCnt, i + 3] as Excel.Range).Value2;
                    var powerSavings = (double)(range.Cells[rCnt, i + 8] as Excel.Range).Value2 * 100;
                    var rec = new PowerConsumptionRecord(draft, speedMap[i], trim, powerUsage, powerSavings);
                    PowerRecords.Add(rec);
                }
            }

            xlWorkbook.Close(true, null, null);
            xlApp.Quit();

            ReleaseObject(xlWorksheet);
            ReleaseObject(xlWorkbook);
            ReleaseObject(xlApp);
        }

        private void ReleaseObject(object obj)
        {
            try
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(obj);
                obj = null;
            }
            catch (Exception ex)
            {
                obj = null;
                MessageBox.Show("Unable to release the Object " + ex.ToString());
            }
            finally
            {
                GC.Collect();
            }
        }
    }
}