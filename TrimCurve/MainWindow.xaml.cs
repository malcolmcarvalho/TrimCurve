using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;

namespace TrimCurve
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public List<Point> _graphPoints;

        public MainWindow()
        {
            InitializeComponent();
        }

        public void DrawPolylineGraph()
        {
            var prevPt = _graphPoints[0];
            for (int i = 1; i < _graphPoints.Count; i++)
            {
                var curPt = _graphPoints[i];

                Line myLine = new Line()
                {
                    X1 = prevPt.X,
                    X2 = curPt.X,
                    Y1 = prevPt.Y,
                    Y2 = curPt.Y
                };

                myLine.Stroke = System.Windows.Media.Brushes.Black;
                myLine.StrokeThickness = 1;
                myLine.SnapsToDevicePixels = true;
                myLine.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);

                prevPt = curPt;
                GraphCanvas.Children.Add(myLine);
            }

            GraphCanvas.InvalidateVisual();
            GraphCanvas.UpdateLayout();
        }

        public void DrawBezierGraph()
        {
            BezierSegment.IsStroked = true;
            BezierSegment.Points = new PointCollection(_graphPoints);
            GraphCanvas.InvalidateVisual();
            GraphCanvas.UpdateLayout();
        }

        private void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            ReadPoints();
            DrawBezierGraph();
        }

        private void ReadPoints()
        {
            var reader = new StreamReader(File.OpenRead(@"C:\Malcolm\CodeProjects\TrimCurve\TrimCurve\Data\Table.csv"));
            _graphPoints = new List<Point>();
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                var values = line.Split(',');
                _graphPoints.Add(new Point(Convert.ToDouble(values[0]), Convert.ToDouble(values[1])));
            }

            reader.Close();
        }
    }
}