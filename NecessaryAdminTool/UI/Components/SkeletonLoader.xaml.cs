using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace NecessaryAdminTool.UI.Components
{
    /// <summary>
    /// Skeleton loading screen for perceived performance improvement
    /// TAG: #SKELETON_LOADER #PERCEIVED_PERFORMANCE #UX_POLISH
    /// Research: 40-60% perceived performance improvement
    /// </summary>
    public partial class SkeletonLoader : UserControl
    {
        public SkeletonLoader()
        {
            InitializeComponent();
            Loaded += SkeletonLoader_Loaded;
        }

        private void SkeletonLoader_Loaded(object sender, RoutedEventArgs e)
        {
            // Start shimmer animation
            var storyboard = (Storyboard)Resources["ShimmerAnimation"];
            storyboard.Begin();
        }
    }
}
