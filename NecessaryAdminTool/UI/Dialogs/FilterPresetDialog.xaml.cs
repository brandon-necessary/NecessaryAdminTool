using System;
using System.Linq;
using System.Windows;
using NecessaryAdminTool.Models;
using NecessaryAdminTool.Managers;
using NecessaryAdminTool.Managers.UI;

namespace NecessaryAdminTool.UI.Dialogs
{
    // TAG: #AUTO_UPDATE_UI_ENGINE #FILTER_SYSTEM #FLUENT_DESIGN #DIALOG
    /// <summary>
    /// Dialog for saving filter presets
    /// Validates input and displays current filter criteria
    /// </summary>
    public partial class FilterPresetDialog : Window
    {
        private FilterCriteria _criteria;
        public bool SaveSuccessful { get; private set; }

        public FilterPresetDialog(FilterCriteria criteria)
        {
            InitializeComponent();
            _criteria = criteria ?? new FilterCriteria();

            LoadPresets();
            DisplayCriteria();

            // TAG: #KEYBOARD_SHORTCUTS
            TxtPresetName.Focus();

            // Enter key saves, Escape cancels
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    BtnSave_Click(null, null);
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.Escape)
                {
                    BtnCancel_Click(null, null);
                    e.Handled = true;
                }
            };
        }

        /// <summary>
        /// Load existing presets for reference
        /// TAG: #PRESET_MANAGEMENT
        /// </summary>
        private void LoadPresets()
        {
            try
            {
                var presets = FilterManager.GetPresets();
                ListPresets.ItemsSource = presets.Select(p => new
                {
                    Display = p.IsBuiltIn ? $"🔒 {p.Name}" : $"📌 {p.Name}",
                    p.Name,
                    p.Description
                });
            }
            catch (Exception ex)
            {
                LogManager.LogError("[FilterPresetDialog] Failed to load presets", ex);
            }
        }

        /// <summary>
        /// Display current filter criteria in dialog
        /// TAG: #UI_FEEDBACK
        /// </summary>
        private void DisplayCriteria()
        {
            try
            {
                // Name Pattern
                if (!string.IsNullOrWhiteSpace(_criteria.NamePattern))
                {
                    PanelNamePattern.Visibility = Visibility.Visible;
                    TxtCriteriaName.Text = _criteria.NamePattern;
                }

                // Status
                if (!string.IsNullOrWhiteSpace(_criteria.StatusFilter))
                {
                    PanelStatus.Visibility = Visibility.Visible;
                    TxtCriteriaStatus.Text = _criteria.StatusFilter;
                }

                // OS
                if (!string.IsNullOrWhiteSpace(_criteria.OSFilter))
                {
                    PanelOS.Visibility = Visibility.Visible;
                    TxtCriteriaOS.Text = _criteria.OSFilter;
                }

                // OU
                if (!string.IsNullOrWhiteSpace(_criteria.OUFilter))
                {
                    PanelOU.Visibility = Visibility.Visible;
                    TxtCriteriaOU.Text = _criteria.OUFilter;
                }

                // RAM
                if (_criteria.MinRamGB.HasValue || _criteria.MaxRamGB.HasValue)
                {
                    PanelRAM.Visibility = Visibility.Visible;
                    if (_criteria.MinRamGB.HasValue && _criteria.MaxRamGB.HasValue)
                        TxtCriteriaRAM.Text = $"{_criteria.MinRamGB} - {_criteria.MaxRamGB} GB";
                    else if (_criteria.MinRamGB.HasValue)
                        TxtCriteriaRAM.Text = $"≥ {_criteria.MinRamGB} GB";
                    else
                        TxtCriteriaRAM.Text = $"≤ {_criteria.MaxRamGB} GB";
                }

                // Last Seen
                if (_criteria.LastSeenAfter.HasValue || _criteria.LastSeenBefore.HasValue)
                {
                    PanelLastSeen.Visibility = Visibility.Visible;
                    if (_criteria.LastSeenAfter.HasValue && _criteria.LastSeenBefore.HasValue)
                        TxtCriteriaLastSeen.Text = $"{_criteria.LastSeenAfter:yyyy-MM-dd} to {_criteria.LastSeenBefore:yyyy-MM-dd}";
                    else if (_criteria.LastSeenAfter.HasValue)
                        TxtCriteriaLastSeen.Text = $"After {_criteria.LastSeenAfter:yyyy-MM-dd}";
                    else
                        TxtCriteriaLastSeen.Text = $"Before {_criteria.LastSeenBefore:yyyy-MM-dd}";
                }

                // Logic Operator
                TxtCriteriaLogic.Text = _criteria.LogicOperator;
            }
            catch (Exception ex)
            {
                LogManager.LogError("[FilterPresetDialog] Failed to display criteria", ex);
            }
        }

        /// <summary>
        /// Save preset button handler
        /// TAG: #SECURITY_CRITICAL - Validates all inputs before saving
        /// </summary>
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string name = TxtPresetName.Text?.Trim();
                string description = TxtDescription.Text?.Trim();

                // TAG: #SECURITY_CRITICAL #INPUT_VALIDATION
                // Validate preset name
                if (string.IsNullOrWhiteSpace(name))
                {
                    ToastManager.ShowWarning("Please enter a preset name", category: "validation");
                    TxtPresetName.Focus();
                    return;
                }

                if (name.Length > 100)
                {
                    ToastManager.ShowWarning("Preset name too long (max 100 characters)", category: "validation");
                    TxtPresetName.Focus();
                    return;
                }

                // Check for invalid characters
                if (name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
                {
                    ToastManager.ShowWarning("Preset name contains invalid characters", category: "validation");
                    TxtPresetName.Focus();
                    return;
                }

                // Validate description length
                if (!string.IsNullOrWhiteSpace(description) && description.Length > 500)
                {
                    ToastManager.ShowWarning("Description too long (max 500 characters)", category: "validation");
                    TxtDescription.Focus();
                    return;
                }

                // Save the preset via FilterManager
                FilterManager.SavePreset(name, description, _criteria);

                SaveSuccessful = true;
                LogManager.LogInfo($"[FilterPresetDialog] Preset saved: {name}");

                // Close dialog
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                LogManager.LogError("[FilterPresetDialog] Failed to save preset", ex);
                ToastManager.ShowError($"Failed to save preset: {ex.Message}", category: "error");
            }
        }

        /// <summary>
        /// Cancel button handler
        /// TAG: #KEYBOARD_SHORTCUTS
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            SaveSuccessful = false;
            DialogResult = false;
            Close();
        }
    }
}
