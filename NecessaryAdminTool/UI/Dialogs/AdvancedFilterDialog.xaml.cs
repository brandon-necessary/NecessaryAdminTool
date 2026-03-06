// TAG: #AUTO_UPDATE_UI_ENGINE #FILTER_SYSTEM #FLUENT_DESIGN #DIALOG
using System;
using System.Windows;
using NecessaryAdminTool.Managers;
using NecessaryAdminTool.Models;

namespace NecessaryAdminTool.UI.Dialogs
{
    public partial class AdvancedFilterDialog : Window
    {
        public FilterCriteria Result { get; private set; }

        public AdvancedFilterDialog(FilterCriteria existing = null)
        {
            InitializeComponent();
            if (existing != null)
                PopulateFrom(existing);
        }

        private void PopulateFrom(FilterCriteria c)
        {
            TxtNamePattern.Text = c.NamePattern ?? "";
            TxtOSFilter.Text = c.OSFilter ?? "";
            TxtOUFilter.Text = c.OUFilter ?? "";
            TxtMinRam.Text = c.MinRamGB.HasValue ? c.MinRamGB.Value.ToString() : "";
            TxtMaxRam.Text = c.MaxRamGB.HasValue ? c.MaxRamGB.Value.ToString() : "";
            DpLastSeenAfter.SelectedDate = c.LastSeenAfter;
            DpLastSeenBefore.SelectedDate = c.LastSeenBefore;

            // Status
            switch ((c.StatusFilter ?? "").ToLowerInvariant())
            {
                case "online":  CboStatus.SelectedIndex = 1; break;
                case "offline": CboStatus.SelectedIndex = 2; break;
                case "warning": CboStatus.SelectedIndex = 3; break;
                default:        CboStatus.SelectedIndex = 0; break;
            }

            // Logic
            CboLogic.SelectedIndex = string.Equals(c.LogicOperator, "OR", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            var criteria = new FilterCriteria
            {
                NamePattern  = string.IsNullOrWhiteSpace(TxtNamePattern.Text) ? null : TxtNamePattern.Text.Trim(),
                OSFilter     = string.IsNullOrWhiteSpace(TxtOSFilter.Text)    ? null : TxtOSFilter.Text.Trim(),
                OUFilter     = string.IsNullOrWhiteSpace(TxtOUFilter.Text)    ? null : TxtOUFilter.Text.Trim(),
                LastSeenAfter  = DpLastSeenAfter.SelectedDate,
                LastSeenBefore = DpLastSeenBefore.SelectedDate,
                LogicOperator  = (CboLogic.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "AND"
            };

            // Status
            string statusSel = (CboStatus.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "All";
            criteria.StatusFilter = statusSel == "All" ? null : statusSel;

            // RAM
            if (int.TryParse(TxtMinRam.Text, out int minRam) && minRam >= 0)
                criteria.MinRamGB = minRam;
            if (int.TryParse(TxtMaxRam.Text, out int maxRam) && maxRam >= 0)
                criteria.MaxRamGB = maxRam;

            if (!FilterManager.ValidateCriteria(criteria))
            {
                NecessaryAdminTool.Managers.UI.ToastManager.ShowWarning("One or more filter values are invalid. Check RAM ranges and date values.");
                return;
            }

            Result = criteria;
            DialogResult = true;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            TxtNamePattern.Text = "";
            TxtOSFilter.Text = "";
            TxtOUFilter.Text = "";
            TxtMinRam.Text = "";
            TxtMaxRam.Text = "";
            DpLastSeenAfter.SelectedDate = null;
            DpLastSeenBefore.SelectedDate = null;
            CboStatus.SelectedIndex = 0;
            CboLogic.SelectedIndex = 0;
            Result = null;
            DialogResult = true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
