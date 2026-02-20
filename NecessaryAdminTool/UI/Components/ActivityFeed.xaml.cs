using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NecessaryAdminTool.Models.UI;

namespace NecessaryAdminTool.UI.Components
{
    // TAG: #AUTO_UPDATE_UI_ENGINE #ACTIVITY_FEED #DASHBOARD
    /// <summary>
    /// Chronological event timeline with severity filtering.
    /// </summary>
    public partial class ActivityFeed : UserControl
    {
        private ObservableCollection<ActivityEvent> _allEvents = new ObservableCollection<ActivityEvent>();

        public ActivityFeed()
        {
            InitializeComponent();
        }

        public void AddEvent(ActivityEvent evt)
        {
            _allEvents.Insert(0, evt);

            // Cap at 200 events
            while (_allEvents.Count > 200)
                _allEvents.RemoveAt(_allEvents.Count - 1);

            ApplyFilter();

            // Auto-scroll to top (newest)
            if (EventList.Items.Count > 0)
                EventList.ScrollIntoView(EventList.Items[0]);
        }

        public void AddEvent(ActivitySeverity severity, string message, string source = "System")
        {
            AddEvent(new ActivityEvent
            {
                Timestamp = DateTime.Now,
                Severity = severity,
                Message = message,
                Source = source
            });
        }

        private void ApplyFilter()
        {
            bool showSuccess = BtnShowSuccess.IsChecked == true;
            bool showInfo = BtnShowInfo.IsChecked == true;
            bool showWarning = BtnShowWarning.IsChecked == true;
            bool showError = BtnShowError.IsChecked == true;

            var filtered = _allEvents.Where(e =>
                (e.Severity == ActivitySeverity.Success && showSuccess) ||
                (e.Severity == ActivitySeverity.Info && showInfo) ||
                (e.Severity == ActivitySeverity.Warning && showWarning) ||
                (e.Severity == ActivitySeverity.Error && showError)
            ).ToList();

            EventList.ItemsSource = filtered;
        }

        private void SeverityToggle_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }
    }
}
