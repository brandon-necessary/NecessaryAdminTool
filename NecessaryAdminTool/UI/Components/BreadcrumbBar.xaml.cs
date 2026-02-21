using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NecessaryAdminTool.UI.Components
{
    // TAG: #AUTO_UPDATE_UI_ENGINE #BREADCRUMB #FLEET_NAVIGATION
    /// <summary>
    /// Breadcrumb navigation bar for fleet inventory navigation context.
    /// </summary>
    public partial class BreadcrumbBar : UserControl
    {
        private ObservableCollection<BreadcrumbSegment> _segments = new ObservableCollection<BreadcrumbSegment>();

        public event EventHandler<BreadcrumbSegment> SegmentClicked;

        public BreadcrumbBar()
        {
            InitializeComponent();
            BreadcrumbItems.ItemsSource = _segments;
        }

        public void SetPath(IEnumerable<BreadcrumbSegment> segments)
        {
            _segments.Clear();
            bool first = true;
            foreach (var seg in segments)
            {
                seg.SeparatorVisibility = first ? Visibility.Collapsed : Visibility.Visible;
                seg.Separator = ">";
                seg.Foreground = seg.IsActive
                    ? Helpers.ThemeHelper.PrimaryBrush
                    : Helpers.ThemeHelper.SecondaryBrush;
                seg.FontWeight = seg.IsActive ? FontWeights.SemiBold : FontWeights.Normal;
                _segments.Add(seg);
                first = false;
            }
        }

        public void Clear()
        {
            _segments.Clear();
        }

        private void Segment_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is BreadcrumbSegment segment)
            {
                segment.OnClick?.Invoke();
                SegmentClicked?.Invoke(this, segment);
            }
        }
    }

    /// <summary>
    /// Individual breadcrumb segment.
    /// </summary>
    public class BreadcrumbSegment
    {
        public string Label { get; set; }
        public Action OnClick { get; set; }
        public bool IsActive { get; set; }

        // Display properties set by BreadcrumbBar.SetPath
        public string Separator { get; set; }
        public Visibility SeparatorVisibility { get; set; }
        public Brush Foreground { get; set; }
        public FontWeight FontWeight { get; set; }
    }
}
