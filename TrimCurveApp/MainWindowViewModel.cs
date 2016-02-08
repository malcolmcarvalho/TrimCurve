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
            this.AbsolutePowerUsagePlotModel = new PlotModel { Title = "Absolute power usage" };
            UpdatePowerConsumptionGraph();
        }

        private void UpdatePowerConsumptionGraph()
        {
            var filteredPowerRecords = PowerRecords.Where(x => x.Draft == Draft && x.Speed == Speed);
            if (filteredPowerRecords.Any())
            {
                AbsolutePowerUsagePlotModel.Series.Clear();
                var ls = new LineSeries();
                ls.Smooth = true;
                var points = new List<DataPoint>();
                foreach (var powerRec in filteredPowerRecords)
                    points.Add(new DataPoint(powerRec.Trim, powerRec.Power));
                ls.ItemsSource = points;
                ls.Title = "Power";
                
                //ls.XAxis.Title = "Trim";
                //ls.YAxis.Title = "Power";

                AbsolutePowerUsagePlotModel.Series.Add(ls);
            }
        }

        private void InitPowerRecordsHardCoded()
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

                PowerConsumptionRecord[] recArray = new PowerConsumptionRecord[6];
                recArray[0] = new PowerConsumptionRecord(draft, 10, trim, power10, 0);
                recArray[1] = new PowerConsumptionRecord(draft, 12, trim, power12, 0);
                recArray[2] = new PowerConsumptionRecord(draft, 14, trim, power14, 0);
                recArray[3] = new PowerConsumptionRecord(draft, 16, trim, power16, 0);
                recArray[4] = new PowerConsumptionRecord(draft, 18, trim, power18, 0);
                recArray[5] = new PowerConsumptionRecord(draft, 20, trim, power20, 0);
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
