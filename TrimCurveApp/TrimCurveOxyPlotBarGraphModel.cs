using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

namespace TrimCurveApp {
    class TrimCurveOxyplotBarGraphModel : PlotModel {
        private static OxyColor LINE_SERIES_COLOR = OxyColor.Parse("#FF0000FF");

        public TrimCurveOxyplotBarGraphModel(string categoryName, double val) {
            CategoryName = categoryName;
            UpdateGraph(val);
        }

        public string CategoryName { get; private set; }

        public void Reset() {
            Series.Clear();
            Axes.Clear();
            Annotations.Clear();
        }

        public void UpdateGraph(double val) {
            Reset();
            try {
                Debug.Assert(val >= 0 && val <= 20);
                var columnSeries = new ColumnSeries() {
                    ItemsSource = new List<ColumnItem>(new[] {
                            new ColumnItem { Value = val }
                    }),
                    FillColor = OxyColors.Black
                };
                columnSeries.Background = OxyColor.FromRgb(255, 0, 0);
                Series.Add(columnSeries);
                Axes.Clear();

                var catAxis = new CategoryAxis {
                    Position = AxisPosition.Bottom,
                    Key = "DraftAxis"
                };
                catAxis.ActualLabels.Add(CategoryName);
                Axes.Add(catAxis);

                var yAxisNew = new LinearAxis() {
                    AbsoluteMinimum = 0,
                    AbsoluteMaximum = 20,
                    Maximum = 20,
                    Position = AxisPosition.Left,
                    MinorStep = 2,
                    MajorStep = 4
                };
                Axes.Add(yAxisNew);

            }
            catch (Exception e) {
                MessageBox.Show(e.InnerException.Message, e.Message);
            }

        }

    }
}