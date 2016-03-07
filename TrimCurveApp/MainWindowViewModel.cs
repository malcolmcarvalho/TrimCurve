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
        private static OxyColor LINE_SERIES_COLOR = OxyColor.Parse("#FFFF0000");

        private static string INVALID_RANGE_MESSAGE = "Draft or speed values provided are not within range. Cannot redraw the graphs.";
        private static string TRIM = "Trim";
        private static string POWER_USAGE = "Power usage (kW)";
        private static string POWER_SAVINGS = "Relative power savings %";

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
            AbsolutePowerUsagePlotModel = new PlotModel { Title = "Absolute power usage" };
            PowerSavingsPlotModel = new PlotModel { Title = "Power savings" };
            //UpdatePowerGraphs();
        }

        public void UpdatePowerGraphs()
        {
            ResetPlotModels();

            var filteredPowerRecords = PowerRecords.Where(x => x.Draft == Draft && x.Speed == Speed);

            IList<DataPoint> puPoints = new List<DataPoint>();
            IList<DataPoint> psPoints = new List<DataPoint>();

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
                UpdateGraph(psPoints, PowerSavingsPlotModel, TRIM, POWER_SAVINGS);
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
            var imageData1 = new OxyColor[n, 1];
            for (int i = 0; i < n; i++)
            {
                imageData1[i, 0] = OxyColor.Interpolate(color1, color2, i / (n - 1.0));
            }

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            PngEncoder encode = new PngEncoder(new PngEncoderOptions());
            return new OxyImage(encode.Encode(imageData1));
        }

        private void AddBackgroundGradient(double xStart, double xEnd, OxyColor color1, OxyColor color2)
        {
            var image = GetGradientImage(color1, color2);
            ImageAnnotation imgAnnotation = new ImageAnnotation
            {
                ImageSource = image,
                Interpolate = true,
                Layer = AnnotationLayer.BelowAxes,
                X = new PlotLength(xStart, PlotLengthUnit.Data),
                Y = new PlotLength(0, PlotLengthUnit.RelativeToPlotArea),
                Width = new PlotLength((xEnd - xStart), PlotLengthUnit.Data),
                Height = new PlotLength(1, PlotLengthUnit.RelativeToPlotArea),
                HorizontalAlignment = OxyPlot.HorizontalAlignment.Left,
                VerticalAlignment = OxyPlot.VerticalAlignment.Middle
            };
            PowerSavingsPlotModel.Annotations.Add(imgAnnotation);
        }

        private void AddBackgroundColorsToPowerSavingsGraph()
        {
            var lineSeries = PowerSavingsPlotModel.Series.ElementAt(0) as LineSeries;
            var points = lineSeries.ItemsSource as IEnumerable<DataPoint>;
            var xAxis = PowerSavingsPlotModel.Axes.Where(x => x.Title == TRIM).First();

            var xMin = points.Min(p => p.X);
            var xMax = points.Max(p => p.X);
            var xRange = xMax - xMin;

            AddBackgroundGradient(xMin, xMin + xRange / 3, OxyColors.Red, OxyColors.Pink);
            AddBackgroundGradient(xMin + xRange / 3, xMin + 2 * xRange / 3, OxyColors.LightYellow, OxyColors.Yellow);
            AddBackgroundGradient(xMin + 2 * xRange / 3, xMax, OxyColors.LightGreen, OxyColors.Green);
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
            LinearAxis axis = new LinearAxis();
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
            for (int rCnt = 4; rCnt <= range.Rows.Count; rCnt++)
            {
                double draft = (double)(range.Cells[rCnt, 1] as Excel.Range).Value2;
                double trim = (double)(range.Cells[rCnt, 2] as Excel.Range).Value2;

                for (int i = 0; i < 6; i++)
                {
                    var powerUsage = (double)(range.Cells[rCnt, i + 3] as Excel.Range).Value2;
                    var powerSavings = (double)(range.Cells[rCnt, i + 10] as Excel.Range).Value2 * 100;
                    PowerConsumptionRecord rec = new PowerConsumptionRecord(draft, 10 + i * 2, trim, powerUsage, powerSavings);
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
