using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NecessaryAdminTool.UI.Components
{
    // TAG: #AUTO_UPDATE_UI_ENGINE #SPARKLINE #DASHBOARD
    /// <summary>
    /// Reusable sparkline chart control. Renders trend data as a Polyline in a Canvas.
    /// </summary>
    public partial class SparklineControl : UserControl
    {
        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register("Data", typeof(IEnumerable<double>), typeof(SparklineControl),
                new PropertyMetadata(null, OnDataChanged));

        public static readonly DependencyProperty LineBrushProperty =
            DependencyProperty.Register("LineBrush", typeof(Brush), typeof(SparklineControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(255, 133, 51)), OnAppearanceChanged));

        public IEnumerable<double> Data
        {
            get => (IEnumerable<double>)GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        public Brush LineBrush
        {
            get => (Brush)GetValue(LineBrushProperty);
            set => SetValue(LineBrushProperty, value);
        }

        public SparklineControl()
        {
            InitializeComponent();
            SizeChanged += (s, e) => Redraw();
        }

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SparklineControl)d).Redraw();
        }

        private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SparklineControl)d).Redraw();
        }

        private void Redraw()
        {
            SparkLine.Points.Clear();
            FillArea.Points.Clear();

            var data = Data?.ToList();
            if (data == null || data.Count < 2) return;

            double w = SparkCanvas.ActualWidth;
            double h = SparkCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            double min = data.Min();
            double max = data.Max();
            double range = max - min;
            if (range < 0.001) range = 1;

            double padding = 2;
            double drawH = h - padding * 2;
            double stepX = w / (data.Count - 1);

            var points = new PointCollection();
            for (int i = 0; i < data.Count; i++)
            {
                double x = i * stepX;
                double y = padding + drawH - ((data[i] - min) / range * drawH);
                points.Add(new Point(x, y));
            }

            SparkLine.Stroke = LineBrush;
            SparkLine.Points = points;

            // Fill polygon: line points + bottom-right + bottom-left
            var fillPoints = new PointCollection(points);
            fillPoints.Add(new Point(w, h));
            fillPoints.Add(new Point(0, h));
            FillArea.Fill = LineBrush;
            FillArea.Points = fillPoints;
        }
    }
}
