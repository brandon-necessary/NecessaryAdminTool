using System;
using System.Windows;
using System.Windows.Controls;

namespace NecessaryAdminTool.UI.Components
{
    // TAG: #AUTO_UPDATE_UI_ENGINE #FILTER_BAR #GLOBAL_FILTER
    /// <summary>
    /// Global filter bar with OS, Status, and Scan Period filters.
    /// Raises FilterChanged event when any filter combo changes.
    /// </summary>
    public partial class FilterBar : UserControl
    {
        private bool _suppressEvents;

        public event EventHandler<FilterCriteria> FilterChanged;

        public FilterBar()
        {
            InitializeComponent();
        }

        public FilterCriteria CurrentCriteria => new FilterCriteria
        {
            OsFilter = GetComboText(ComboOsFilter),
            StatusFilter = GetComboText(ComboStatusFilter),
            ScanPeriod = GetComboText(ComboScanPeriod)
        };

        private string GetComboText(ComboBox combo)
        {
            if (combo.SelectedItem is ComboBoxItem item)
                return item.Content?.ToString() ?? "";
            return "";
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            FilterChanged?.Invoke(this, CurrentCriteria);
        }

        private void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            _suppressEvents = true;
            ComboOsFilter.SelectedIndex = 0;
            ComboStatusFilter.SelectedIndex = 0;
            ComboScanPeriod.SelectedIndex = 0;
            _suppressEvents = false;
            FilterChanged?.Invoke(this, CurrentCriteria);
        }
    }

    /// <summary>
    /// Criteria from the global filter bar.
    /// </summary>
    public class FilterCriteria
    {
        public string OsFilter { get; set; } = "All OS";
        public string StatusFilter { get; set; } = "All";
        public string ScanPeriod { get; set; } = "Any Time";

        public bool IsDefault => OsFilter == "All OS" && StatusFilter == "All" && ScanPeriod == "Any Time";
    }
}
