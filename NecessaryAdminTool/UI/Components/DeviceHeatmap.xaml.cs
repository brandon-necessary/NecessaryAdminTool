using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NecessaryAdminTool.UI.Components
{
    // TAG: #AUTO_UPDATE_UI_ENGINE #DEVICE_HEATMAP #DASHBOARD
    /// <summary>
    /// Device health heatmap showing fleet status as colored tiles.
    /// Caps display at 1000 devices with "Show All" toggle.
    /// </summary>
    public partial class DeviceHeatmap : UserControl
    {
        private const int DefaultMaxDevices = 1000;
        private IEnumerable _fullSource;
        private bool _showingAll;

        public event EventHandler<string> DeviceClicked;

        public DeviceHeatmap()
        {
            InitializeComponent();
        }

        public void SetDevices(IEnumerable devices)
        {
            _fullSource = devices;
            _showingAll = false;
            ApplySource();
        }

        private void ApplySource()
        {
            if (_fullSource == null) return;

            var list = _fullSource.Cast<object>().ToList();
            int total = list.Count;

            if (!_showingAll && total > DefaultMaxDevices)
            {
                HeatmapItems.ItemsSource = list.Take(DefaultMaxDevices);
                BtnShowAll.Visibility = Visibility.Visible;
                BtnShowAll.Content = $"Show All ({total})";
                TxtDeviceCount.Text = $"Showing {DefaultMaxDevices} of {total}";
            }
            else
            {
                HeatmapItems.ItemsSource = list;
                BtnShowAll.Visibility = Visibility.Collapsed;
                TxtDeviceCount.Text = $"{total} devices";
            }
        }

        private void BtnShowAll_Click(object sender, RoutedEventArgs e)
        {
            _showingAll = true;
            ApplySource();
        }

        private void DeviceTile_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string hostname)
            {
                DeviceClicked?.Invoke(this, hostname);
            }
        }
    }
}
