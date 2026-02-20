using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace NecessaryAdminTool.UI.Components
{
    // TAG: #AUTO_UPDATE_UI_ENGINE #KPI_CARD #DASHBOARD #ANIMATED_VALUES
    /// <summary>
    /// Hero KPI card with sparkline trend and animated value transitions.
    /// </summary>
    public partial class KpiCard : UserControl
    {
        private double _displayValue;
        private double _targetValue;
        private double _startValue;
        private DispatcherTimer _animTimer;
        private DateTime _animStart;
        private const double AnimDurationMs = 350;

        public KpiCard()
        {
            InitializeComponent();
        }

        public string Label
        {
            get => TxtLabel.Text;
            set => TxtLabel.Text = value;
        }

        public Brush AccentColor
        {
            get => AccentStripe.Background;
            set
            {
                AccentStripe.Background = value;
                Sparkline.LineBrush = value;
                TxtLabel.Foreground = value;
                CardBorder.BorderBrush = value;
            }
        }

        public string Subtitle
        {
            get => TxtSubtitle.Text;
            set => TxtSubtitle.Text = value;
        }

        public string FormatString { get; set; } = "N0";

        public List<double> SparklineData
        {
            set
            {
                if (value != null && value.Count >= 2)
                    Sparkline.Data = value;
            }
        }

        public void SetValue(double newValue, double previousValue = double.NaN)
        {
            _startValue = _displayValue;
            _targetValue = newValue;

            // Delta
            if (!double.IsNaN(previousValue) && previousValue != 0)
            {
                double delta = newValue - previousValue;
                double pct = delta / previousValue * 100;
                bool positive = delta >= 0;
                TxtDelta.Text = $"{(positive ? "\u2191" : "\u2193")} {Math.Abs(pct):F1}%";
                TxtDelta.Foreground = positive
                    ? new SolidColorBrush(Color.FromRgb(16, 185, 129))
                    : new SolidColorBrush(Color.FromRgb(239, 68, 68));
                DeltaPanel.Visibility = Visibility.Visible;
            }
            else
            {
                DeltaPanel.Visibility = Visibility.Collapsed;
            }

            // Animate value
            _animStart = DateTime.UtcNow;
            if (_animTimer == null)
            {
                _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                _animTimer.Tick += AnimTimer_Tick;
            }
            _animTimer.Start();
        }

        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            double elapsed = (DateTime.UtcNow - _animStart).TotalMilliseconds;
            double progress = Math.Min(1.0, elapsed / AnimDurationMs);
            // Ease-out cubic
            double eased = 1 - Math.Pow(1 - progress, 3);

            _displayValue = _startValue + (_targetValue - _startValue) * eased;
            TxtValue.Text = _displayValue.ToString(FormatString);

            if (progress >= 1.0)
            {
                _animTimer.Stop();
                _displayValue = _targetValue;
                TxtValue.Text = _targetValue.ToString(FormatString);

                // Brief accent flash
                var original = TxtValue.Foreground;
                TxtValue.Foreground = AccentStripe.Background ?? Brushes.Orange;
                var flashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                flashTimer.Tick += (s2, e2) =>
                {
                    TxtValue.Foreground = Brushes.White;
                    ((DispatcherTimer)s2).Stop();
                };
                flashTimer.Start();
            }
        }
    }
}
