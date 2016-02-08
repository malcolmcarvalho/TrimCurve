using OxyPlot;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CSharp;
using Excel = Microsoft.Office.Interop.Excel;
using System.Windows;
using System.Diagnostics;

namespace TrimCurveApp
{
    class MainWindowViewModel
    {
        public PlotModel AbsolutePowerUsagePlotModel { get; private set; }
        public PlotModel PowerSavingsPlotModel { get; private set; }
        public double Draft { get; set; }
        public double Speed { get; set; }

        public List<PowerConsumptionRecord> PowerRecords = new List<PowerConsumptionRecord>();

        public MainWindowViewModel()
        {
            Draft = 25;
            Speed = 18;
            InitPowerRecordsHardCoded();
            AbsolutePowerUsagePlotModel = new PlotModel { Title = "Absolute power usage" };
            PowerSavingsPlotModel = new PlotModel { Title = "Power savings" };
            UpdatePowerGraphs();
        }

        public void UpdatePowerGraphs()
        {
            ResetPlotModels();

            var filteredPowerRecords = PowerRecords.Where(x => x.Draft == Draft && x.Speed == Speed);

            var powerUsageSeries = new LineSeries();
            var powerSavingsSeries = new LineSeries();
            powerUsageSeries.Smooth = true;
            powerSavingsSeries.Smooth = true;
            var puPoints = new List<DataPoint>();
            var psPoints = new List<DataPoint>();

            if (filteredPowerRecords.Any())
            {
                foreach (var powerRec in filteredPowerRecords)
                {
                    puPoints.Add(new DataPoint(powerRec.Trim, powerRec.Power));
                    psPoints.Add(new DataPoint(powerRec.Trim, powerRec.PowerSavingPercentage));
                }
            }
            else
            {
                var draftMatches = PowerRecords.Where(x => x.Draft == Draft);
                var speedMatches = PowerRecords.Where(x => x.Speed == Speed);
                if (draftMatches.Any())
                {
                    var lowerRecords = GetPrevSpeedRecords(draftMatches);
                    var upperRecords = GetNextSpeedRecords(draftMatches);
                    if (!lowerRecords.Any() || !upperRecords.Any())
                    {
                        MessageBox.Show("Draft or speed values provided are not within range. Cannot redraw the graphs.");
                        return;
                    }

                    var lowerSpeed = lowerRecords.FirstOrDefault().Speed;
                    var upperSpeed = upperRecords.FirstOrDefault().Speed;
                    var wtFunction = (Speed - lowerSpeed) / (upperSpeed - lowerSpeed);
                    GenerateGraphPoints(lowerRecords, upperRecords, psPoints, puPoints, wtFunction);
                }
                else if (speedMatches.Any())
                {
                    var lowerRecords = GetPrevDraftRecords(speedMatches);
                    var upperRecords = GetNextDraftRecords(speedMatches);
                    if (!lowerRecords.Any() || !upperRecords.Any())
                    {
                        MessageBox.Show("Draft or speed values provided are not within range. Cannot redraw the graphs.");
                        return;
                    }

                    var lowerDraft = lowerRecords.FirstOrDefault().Draft;
                    var upperDraft = upperRecords.FirstOrDefault().Draft;
                    var wtFunction = (Draft - lowerDraft) / (upperDraft - lowerDraft);
                    GenerateGraphPoints(lowerRecords, upperRecords, psPoints, puPoints, wtFunction);
                }
                else
                {
                    var lowerSpeedRecords = GetPrevSpeedRecords(PowerRecords);
                    var upperSpeedRecords = GetNextSpeedRecords(PowerRecords);
                    if (!lowerSpeedRecords.Any() || !upperSpeedRecords.Any())
                    {
                        MessageBox.Show("Draft or speed values provided are not within range. Cannot redraw the graphs.");
                        return;
                    }

                    var leftLowerRecords = GetPrevDraftRecords(lowerSpeedRecords);
                    var leftUpperRecords = GetNextDraftRecords(lowerSpeedRecords);
                    var rightLowerRecords = GetPrevDraftRecords(upperSpeedRecords);
                    var rightUpperRecords = GetNextDraftRecords(upperSpeedRecords);
                    GenerateGraphPoints(leftLowerRecords, leftUpperRecords, rightLowerRecords, rightUpperRecords, psPoints, puPoints);
                }
            }

            powerSavingsSeries.ItemsSource = psPoints;
            PowerSavingsPlotModel.Series.Add(powerSavingsSeries);
            powerUsageSeries.ItemsSource = puPoints;
            AbsolutePowerUsagePlotModel.Series.Add(powerUsageSeries);
        }

        private void ResetPlotModels() {
            AbsolutePowerUsagePlotModel.Series.Clear();
            AbsolutePowerUsagePlotModel.Axes.Clear();
            PowerSavingsPlotModel.Series.Clear();
            PowerSavingsPlotModel.Axes.Clear();
        }
        }

        private void GenerateGraphPoints(IEnumerable<PowerConsumptionRecord> lowerRecords, IEnumerable<PowerConsumptionRecord> upperRecords,
            List<DataPoint> psPoints, List<DataPoint> puPoints, double wtFunction)
        {
            if (!lowerRecords.Any() || !upperRecords.Any())
            {
                MessageBox.Show("Draft or speed values provided are not within range. Cannot redraw the graphs.");
                return;
            }

            Debug.Assert(lowerRecords.Count() == upperRecords.Count());
            foreach (var rec in lowerRecords)
            {
                var upperMatch = upperRecords.Where(x => x.Trim == rec.Trim).FirstOrDefault();
                var newPower = rec.Power + wtFunction * (upperMatch.Power - rec.Power);
                var newPowerSavings = rec.PowerSavingPercentage + wtFunction * (upperMatch.PowerSavingPercentage - rec.PowerSavingPercentage);
                puPoints.Add(new DataPoint(rec.Trim, newPower));
                psPoints.Add(new DataPoint(rec.Trim, newPowerSavings));
            }
        }

        private void InitPowerRecordsHardCoded()
        private void GenerateGraphPoints(IEnumerable<PowerConsumptionRecord> leftLowerRecords, IEnumerable<PowerConsumptionRecord> leftUpperRecords,
            IEnumerable<PowerConsumptionRecord> rightLowerRecords, IEnumerable<PowerConsumptionRecord> rightUpperRecords,
            List<DataPoint> psPoints, List<DataPoint> puPoints)
        {
            Debug.Assert(leftLowerRecords.Count() == leftUpperRecords.Count());
            Debug.Assert(rightLowerRecords.Count() == rightUpperRecords.Count());
            Debug.Assert(leftLowerRecords.Count() == rightLowerRecords.Count());

            if (!leftLowerRecords.Any() || !leftUpperRecords.Any() || rightLowerRecords.Any() || rightUpperRecords.Any())
            {
                MessageBox.Show("Draft or speed values provided are not within range. Cannot redraw the graphs.");
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
                avg1 = rec.PowerSavingPercentage + speedWtFunction * (rightLowerMatch.PowerSavingPercentage - rec.PowerSavingPercentage);
                avg2 = leftUpperMatch.PowerSavingPercentage + speedWtFunction * (rightUpperMatch.PowerSavingPercentage - leftUpperMatch.PowerSavingPercentage);
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
        {
            var xlApp = new Excel.Application();
            var xlWorkbook = xlApp.Workbooks.Open(@"C:\Malcolm\GreenOptilfoat\TrimCurve\Data\TrimCurveModifiedSample.xlsx", 0, true, 5, "", "", true, Microsoft.Office.Interop.Excel.XlPlatform.xlWindows, "\t", false, false, 0, true, 1, 0);
            var xlWorksheet = xlApp.Worksheets.get_Item(1);

            var range = xlWorksheet.UsedRange;
            for (int rCnt = 4; rCnt <= range.Rows.Count; rCnt++)
            {
                double draft = (double)(range.Cells[rCnt, 1] as Excel.Range).Value2;
                double trim = (double)(range.Cells[rCnt, 2] as Excel.Range).Value2;

                double power10 = (double)(range.Cells[rCnt, 3] as Excel.Range).Value2;
                double power12 = (double)(range.Cells[rCnt, 4] as Excel.Range).Value2;
                double power14 = (double)(range.Cells[rCnt, 5] as Excel.Range).Value2;
                double power16 = (double)(range.Cells[rCnt, 6] as Excel.Range).Value2;
                double power18 = (double)(range.Cells[rCnt, 7] as Excel.Range).Value2;
                double power20 = (double)(range.Cells[rCnt, 8] as Excel.Range).Value2;

                double percentage10 = (double)(range.Cells[rCnt, 10] as Excel.Range).Value2;
                double percentage12 = (double)(range.Cells[rCnt, 11] as Excel.Range).Value2;
                double percentage14 = (double)(range.Cells[rCnt, 12] as Excel.Range).Value2;
                double percentage16 = (double)(range.Cells[rCnt, 13] as Excel.Range).Value2;
                double percentage18 = (double)(range.Cells[rCnt, 14] as Excel.Range).Value2;
                double percentage20 = (double)(range.Cells[rCnt, 15] as Excel.Range).Value2;

                PowerConsumptionRecord[] recArray = new PowerConsumptionRecord[6];
                recArray[0] = new PowerConsumptionRecord(draft, 10, trim, power10, percentage10);
                recArray[1] = new PowerConsumptionRecord(draft, 12, trim, power12, percentage12);
                recArray[2] = new PowerConsumptionRecord(draft, 14, trim, power14, percentage14);
                recArray[3] = new PowerConsumptionRecord(draft, 16, trim, power16, percentage16);
                recArray[4] = new PowerConsumptionRecord(draft, 18, trim, power18, percentage18);
                recArray[5] = new PowerConsumptionRecord(draft, 20, trim, power20, percentage20);
                PowerRecords.AddRange(recArray);
            }

            xlWorkbook.Close(true, null, null);
            xlApp.Quit();

            releaseObject(xlWorksheet);
            releaseObject(xlWorkbook);
            releaseObject(xlApp);



            //double draft = 15;
            //double basePower = 10000;
            //for (double speed = 10; speed <= 20; speed += 2) {
            //    double power = basePower;
            //    for (int trim = 3; trim >= -3; --trim)
            //    {
            //        PowerConsumptionRecord rec = new PowerConsumptionRecord(draft, speed, trim, power, 0);
            //        PowerRecords.Add(rec);
            //        power -= 200;
            //    }
            //    basePower += 200;
            //}
        }

        private void releaseObject(object obj)
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
