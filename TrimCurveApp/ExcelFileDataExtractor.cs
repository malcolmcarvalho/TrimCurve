using OxyPlot;
using System;
using System.Collections.Generic;
using System.Windows;
using Excel = Microsoft.Office.Interop.Excel;

namespace TrimCurveApp {
    static class ExcelFileDataExtractor {
        public static List<PowerConsumptionRecord> ReadPowerValuesFromXLS() {
            const string TRIM_CURVE_FILE_NAME = @"C:\Malcolm\GreenOptilfoat\TrimCurve\Data\TrimCurveModifiedSample.xlsx";
            Excel.Application xlApp;
            Excel.Workbook xlWorkbook;
            Excel.Worksheet xlWorksheet;
            GetExcelReferences(TRIM_CURVE_FILE_NAME, out xlApp, out xlWorkbook, out xlWorksheet);

            var range = xlWorksheet.UsedRange;
            const int HEADER_ROW_INDEX = 2;
            const int SPEED_CELL_START = 3;
            const int DRAFT_INDEX = 1;
            const int TRIM_INDEX = 2;
            var speeds = new List<int>();
            int speedIndex = SPEED_CELL_START;
            while (true) {
                var speedCell = range.Cells[HEADER_ROW_INDEX, speedIndex++] as Excel.Range;
                if (speedCell.Value2 == null)
                    break;
                var value = (int)(speedCell).Value2;
                speeds.Add(value);
            }

            var powerRecords = new List<PowerConsumptionRecord>();
            for (int rCnt = HEADER_ROW_INDEX + 1; rCnt <= range.Rows.Count; rCnt++) {
                double draft = (double)(range.Cells[rCnt, DRAFT_INDEX] as Excel.Range).Value2;
                double trim = (double)(range.Cells[rCnt, TRIM_INDEX] as Excel.Range).Value2;

                for (int i = 0; i < speeds.Count; i++) {
                    var curUsageCell = SPEED_CELL_START + i;
                    var powerUsage = (double)(range.Cells[rCnt, curUsageCell] as Excel.Range).Value2;
                    var powerSavings = (double)(range.Cells[rCnt, curUsageCell + speeds.Count + 1] as Excel.Range).Value2 * 100;
                    var rec = new PowerConsumptionRecord(draft, speeds[i], trim, powerUsage, powerSavings);
                    powerRecords.Add(rec);
                }
            }

            CloseAndReleaseExcelObjects(xlApp, xlWorkbook, xlWorksheet);
            return powerRecords;
        }

        public static List<DataPoint> ReadSFOCValuesFromXLS() {
            const string SFOC_FILE_NAME = @"C:\Malcolm\GreenOptilfoat\TrimCurve\Data\SFOC.xlsx";
            Excel.Application xlApp;
            Excel.Workbook xlWorkbook;
            Excel.Worksheet xlWorksheet;
            GetExcelReferences(SFOC_FILE_NAME, out xlApp, out xlWorkbook, out xlWorksheet);

            var range = xlWorksheet.UsedRange;
            const int SPEED_COL = 3;
            const int CONSUMPTION_COL = 6;
            var sfocPoints = new List<DataPoint>();
            for (int rIndex = 2; rIndex <= range.Rows.Count; rIndex++) {
                double speed = (double)(range.Cells[rIndex, SPEED_COL] as Excel.Range).Value2;
                double consumption = (double)(range.Cells[rIndex, CONSUMPTION_COL] as Excel.Range).Value2;
                sfocPoints.Add(new DataPoint(speed, consumption));
            }

            CloseAndReleaseExcelObjects(xlApp, xlWorkbook, xlWorksheet);
            return sfocPoints;
        }

        private static void GetExcelReferences(string fileName, out Excel.Application xlApp, out Excel.Workbook xlworkBook, out Excel.Worksheet xlWorksheet) {
            xlApp = new Excel.Application();
            xlworkBook = xlApp.Workbooks.Open(
                fileName,
                0, true, 5, "", "", true,
                Microsoft.Office.Interop.Excel.XlPlatform.xlWindows,
                "\t", false, false, 0, true, 1, 0);
            xlWorksheet = xlApp.Worksheets.get_Item(1);
        }

        private static void CloseAndReleaseExcelObjects(Excel.Application xlApp, Excel.Workbook xlworkBook, Excel.Worksheet xlWorksheet) {
            xlworkBook.Close(false, null, null);
            xlApp.Quit();
            ReleaseObject(xlWorksheet);
            ReleaseObject(xlworkBook);
            ReleaseObject(xlApp);
        }

        private static void ReleaseObject(object obj) {
            try {
                int r = System.Runtime.InteropServices.Marshal.ReleaseComObject(obj);
                obj = null;
            }
            catch (Exception ex) {
                obj = null;
                MessageBox.Show("Unable to release the Object " + ex.ToString());
            }
            finally {
                GC.Collect();
            }
        }
    }
}
