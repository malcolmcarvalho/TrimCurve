using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace TrimCurveApp {
    class TrimCurveOxyplotModel : PlotModel {
        private static OxyColor LINE_SERIES_COLOR = OxyColor.Parse("#FF0000FF");

        public void Reset() {
            Series.Clear();
            Axes.Clear();
            Annotations.Clear();
        }

        public void UpdateGraph(
            IEnumerable<DataPoint> points,
            string xAxis,
            string yAxis) {
            var lineSeries = new LineSeries();
            lineSeries.Smooth = true;
            lineSeries.ItemsSource = points;
            lineSeries.MarkerType = MarkerType.Circle;
            lineSeries.MarkerFill = LINE_SERIES_COLOR;
            lineSeries.Color = LINE_SERIES_COLOR;
            Series.Add(lineSeries);

            PlotAreaBackground = OxyColor.FromArgb(255, 255, 255, 255);
            SetPlotModelAxes(points, xAxis, yAxis);
        }


        public void SetPlotModelAxes(
            IEnumerable<DataPoint> seriesPoints,
            string xAxisTitle, string yAxisTitle) {
            double minXVal = seriesPoints.Min<DataPoint>(dp => dp.X);
            double maxXVal = seriesPoints.Max<DataPoint>(dp => dp.X);
            double minYVal = seriesPoints.Min<DataPoint>(dp => dp.Y);
            double maxYVal = seriesPoints.Max<DataPoint>(dp => dp.Y);

            PlotType = PlotType.XY;
            SetXAxisForPlotModel(minXVal, maxXVal, xAxisTitle);
            SetYAxisForPlotModel(minYVal, maxYVal, yAxisTitle);
        }

        private void SetXAxisForPlotModel(double minVal, double maxVal, string title) {
            var xAxis = CreateAxisForPlotModel(minVal, maxVal, title, true);
            xAxis.MajorStep = 1;
            xAxis.MinorStep = 0.2;
            Axes.Add(xAxis);
        }

        private void SetYAxisForPlotModel(double minVal, double maxVal, string title) {
            var yAxis = CreateAxisForPlotModel(minVal, maxVal, title, false);
            Axes.Add(yAxis);
        }

        private LinearAxis CreateAxisForPlotModel(double minVal, double maxVal, string title, bool isXAxis) {
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
    }
}