using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace NecessaryAdminTool.Helpers
{
    // TAG: #AUTO_UPDATE_UI_ENGINE #ANIMATION #DETAIL_DRAWER
    /// <summary>
    /// Animates GridLength values for smooth column/row width transitions.
    /// Used by the detail drawer slide-in panel.
    /// </summary>
    public class GridLengthAnimation : AnimationTimeline
    {
        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register("From", typeof(GridLength), typeof(GridLengthAnimation));

        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register("To", typeof(GridLength), typeof(GridLengthAnimation));

        public GridLength From
        {
            get => (GridLength)GetValue(FromProperty);
            set => SetValue(FromProperty, value);
        }

        public GridLength To
        {
            get => (GridLength)GetValue(ToProperty);
            set => SetValue(ToProperty, value);
        }

        public override Type TargetPropertyType => typeof(GridLength);

        protected override Freezable CreateInstanceCore()
        {
            return new GridLengthAnimation();
        }

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
        {
            double fromVal = From.Value;
            double toVal = To.Value;

            double progress = animationClock.CurrentProgress ?? 0.0;
            // Ease-out cubic
            double eased = 1 - Math.Pow(1 - progress, 3);

            double current = fromVal + (toVal - fromVal) * eased;
            return new GridLength(Math.Max(0, current), GridUnitType.Pixel);
        }
    }
}
