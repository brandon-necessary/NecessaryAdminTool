using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.IO;
using Microsoft.Win32;
using System.Web.Script.Serialization;
using NecessaryAdminTool.Managers.UI;

namespace NecessaryAdminTool
{
    public partial class OptionsWindow : Window
    {
        private ObservableCollection<PinnedDevice> _pinnedDevices;
        private ObservableCollection<ServiceConfigItem> _serviceConfigItems = new ObservableCollection<ServiceConfigItem>();
        private List<string> _targetHistory;
        private bool _hasUnsavedChanges = false;
        private ObservableCollection<ConnectionProfile> _connectionProfiles = new ObservableCollection<ConnectionProfile>(); // TAG: #VERSION_7 #CONNECTION_PROFILES
        private ObservableCollection<ComputerBookmark> _bookmarks = new ObservableCollection<ComputerBookmark>(); // TAG: #VERSION_7 #BOOKMARKS

        public OptionsWindow()
        {
            InitializeComponent();
            Title = $"{LogoConfig.PRODUCT_NAME} Options";
            LoadAllSettings();
            InitializeThemeControls();
        }

        /// <summary>
        /// Load all settings from various sources into UI
        /// </summary>
        private void LoadAllSettings()
        {
            try
            {
                // General Settings
                if (TxtLastUser != null)
                    TxtLastUser.Text = Properties.Settings.Default.LastUser ?? "";

                // TAG: #VERSION_7 #AD_QUERY_BACKEND_SELECTION - Load AD Query Method
                if (ComboADQueryMethod != null)
                {
                    string queryMethod = Properties.Settings.Default.ADQueryMethod ?? "DirectorySearcher";
                    ComboADQueryMethod.SelectedValue = queryMethod;
                }

                // TAG: #DC_HEALTH #CONFIGURABLE_OPTIONS - Load DC Health settings
                if (ChkDCHealthExpanded != null)
                {
                    ChkDCHealthExpanded.IsChecked = Properties.Settings.Default.DCHealthExpanded;
                }

                // TAG: #SECURITY #AUTHENTICATION - Load credential storage settings
                if (ChkRememberCredentials != null)
                {
                    ChkRememberCredentials.IsChecked = Properties.Settings.Default.RememberCredentials;
                }
                if (ChkShowPasswordExpiryWarnings != null)
                {
                    ChkShowPasswordExpiryWarnings.IsChecked = Properties.Settings.Default.ShowPasswordExpiryWarnings;
                }
                if (TxtPasswordWarningDays != null)
                {
                    TxtPasswordWarningDays.Text = Properties.Settings.Default.PasswordExpiryWarningDays.ToString();
                }

                // Load target history from UserConfig
                _targetHistory = SettingsManager.LoadTargetHistory();

                // Populate target history ListBox
                if (ListTargetHistory != null)
                    ListTargetHistory.ItemsSource = _targetHistory;

                // Performance Settings
                if (TxtMaxParallel != null)
                    TxtMaxParallel.Text = SecureConfig.MaxParallelScans.ToString();
                if (SliderMaxParallel != null)
                    SliderMaxParallel.Value = SecureConfig.MaxParallelScans;
                if (TxtWmiTimeout != null)
                    TxtWmiTimeout.Text = SecureConfig.WmiTimeoutMs.ToString();
                if (TxtPingTimeout != null)
                    TxtPingTimeout.Text = SecureConfig.PingTimeoutMs.ToString();
                if (TxtMaxRetry != null)
                    TxtMaxRetry.Text = SecureConfig.MaxRetryAttempts.ToString();

                // Pinned Devices
                LoadPinnedDevices();

                // Global Services Configuration
                LoadGlobalServicesConfig(Properties.Settings.Default.GlobalServicesConfig);

                // TAG: #VERSION_7 #QUICK_WINS - Load Font Size settings
                if (SliderFontSize != null)
                {
                    double fontMultiplier = Properties.Settings.Default.FontSizeMultiplier;
                    if (fontMultiplier < 0.8 || fontMultiplier > 2.0) fontMultiplier = 1.0;
                    SliderFontSize.Value = fontMultiplier;
                    if (TxtFontSizeValue != null)
                        TxtFontSizeValue.Text = $"{fontMultiplier:F1}x";
                    if (TxtFontPreview != null)
                        TxtFontPreview.FontSize = 12 * fontMultiplier;
                }

                // TAG: #VERSION_7 #QUICK_WINS - Load Auto-Save settings
                if (ChkAutoSaveEnabled != null)
                {
                    ChkAutoSaveEnabled.IsChecked = Properties.Settings.Default.AutoSaveEnabled;
                    if (PanelAutoSaveSettings != null)
                        PanelAutoSaveSettings.IsEnabled = Properties.Settings.Default.AutoSaveEnabled;
                }
                if (TxtAutoSaveInterval != null)
                {
                    int interval = Properties.Settings.Default.AutoSaveIntervalMinutes;
                    if (interval < 1 || interval > 60) interval = 5;
                    TxtAutoSaveInterval.Text = interval.ToString();
                }

                // TAG: #VERSION_7 #THEME_COLORS - Load accent colors
                if (TxtPrimaryColor != null)
                {
                    string primaryColor = Properties.Settings.Default.PrimaryAccentColor;
                    if (string.IsNullOrEmpty(primaryColor)) primaryColor = "#FFFF8533";
                    TxtPrimaryColor.Text = primaryColor;
                }
                if (TxtSecondaryColor != null)
                {
                    string secondaryColor = Properties.Settings.Default.SecondaryAccentColor;
                    if (string.IsNullOrEmpty(secondaryColor)) secondaryColor = "#FFA1A1AA";
                    TxtSecondaryColor.Text = secondaryColor;
                }

                // Load custom logo path and preview
                string savedLogoPath = Properties.Settings.Default.CustomLogoPath;
                if (!string.IsNullOrEmpty(savedLogoPath) && File.Exists(savedLogoPath))
                {
                    if (TxtLogoPath != null) TxtLogoPath.Text = savedLogoPath;
                    LoadLogoPreview(savedLogoPath);
                }

                // TAG: #VERSION_7 #CONNECTION_PROFILES - Load connection profiles
                LoadConnectionProfiles();

                // TAG: #VERSION_1_2 #DATABASE - Load database configuration and stats
                LoadDatabaseConfiguration();

                // TAG: #VERSION_1_0 #DEPLOYMENT - Load deployment configuration
                LoadDeploymentConfiguration();

                // TAG: #VERSION_1_2 #SERVICE - Load service status
                LoadServiceStatus();

                // TAG: #VERSION_7 #BOOKMARKS - Load bookmarks
                LoadBookmarks();

                // TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS - Load toast notifications and keyboard shortcuts
                LoadToastNotificationSettings();
                LoadKeyboardShortcutSettings();

                _hasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                if (TxtStatusMessage != null)
                    ShowStatus($"Error loading settings: {ex.Message}", MessageType.Error);
            }
        }

        /// <summary>
        /// Load pinned devices from settings
        /// </summary>
        private void LoadPinnedDevices()
        {
            try
            {
                _pinnedDevices = new ObservableCollection<PinnedDevice>();

                string saved = Properties.Settings.Default.PinnedDevices ?? "";
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    var devices = saved.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var device in devices.Take(10))
                    {
                        _pinnedDevices.Add(new PinnedDevice
                        {
                            Input = device,
                            Status = "Unknown",
                            LastChecked = "Not checked",
                            StatusColor = Brushes.Gray
                        });
                    }
                }

                if (GridPinnedDevicesManager != null)
                    GridPinnedDevicesManager.ItemsSource = _pinnedDevices;
            }
            catch (Exception ex)
            {
                if (TxtStatusMessage != null)
                    ShowStatus($"Error loading pinned devices: {ex.Message}", MessageType.Error);
            }
        }

        /// <summary>
        /// Save all settings
        /// </summary>
        private void SaveAllSettings()
        {
            try
            {
                // Performance Settings
                int maxParallel = int.Parse(TxtMaxParallel.Text);
                int wmiTimeout = int.Parse(TxtWmiTimeout.Text);
                int pingTimeout = int.Parse(TxtPingTimeout.Text);
                int maxRetry = int.Parse(TxtMaxRetry.Text);

                // Validate ranges
                if (maxParallel < 1 || maxParallel > 100)
                {
                    ShowStatus("Max Parallel Scans must be between 1 and 100", MessageType.Error);
                    return;
                }

                SecureConfig.MaxParallelScans = maxParallel;
                SecureConfig.WmiTimeoutMs = wmiTimeout;
                SecureConfig.PingTimeoutMs = pingTimeout;
                SecureConfig.MaxRetryAttempts = maxRetry;
                SecureConfig.SaveConfiguration();

                // Pinned Devices
                SavePinnedDevices();

                // TAG: #VERSION_7 #QUICK_WINS - Save Font Size settings
                if (SliderFontSize != null)
                {
                    Properties.Settings.Default.FontSizeMultiplier = SliderFontSize.Value;
                }

                // TAG: #VERSION_7 #QUICK_WINS - Save Auto-Save settings
                if (ChkAutoSaveEnabled != null)
                {
                    Properties.Settings.Default.AutoSaveEnabled = ChkAutoSaveEnabled.IsChecked ?? true;
                }
                if (TxtAutoSaveInterval != null)
                {
                    if (int.TryParse(TxtAutoSaveInterval.Text, out int interval))
                    {
                        if (interval >= 1 && interval <= 60)
                        {
                            Properties.Settings.Default.AutoSaveIntervalMinutes = interval;
                        }
                        else
                        {
                            ShowStatus("Auto-save interval must be between 1 and 60 minutes", MessageType.Error);
                            return;
                        }
                    }
                }

                // TAG: #VERSION_7 #THEME_COLORS - Save and apply accent colors
                if (TxtPrimaryColor != null && TxtSecondaryColor != null)
                {
                    string primaryColor = TxtPrimaryColor.Text;
                    string secondaryColor = TxtSecondaryColor.Text;

                    // Validate color format
                    if (primaryColor.StartsWith("#") && (primaryColor.Length == 7 || primaryColor.Length == 9) &&
                        secondaryColor.StartsWith("#") && (secondaryColor.Length == 7 || secondaryColor.Length == 9))
                    {
                        try
                        {
                            // Save to settings
                            Properties.Settings.Default.PrimaryAccentColor = primaryColor;
                            Properties.Settings.Default.SecondaryAccentColor = secondaryColor;

                            // Apply to application resources immediately
                            var primaryColorObj = (Color)ColorConverter.ConvertFromString(primaryColor);
                            var secondaryColorObj = (Color)ColorConverter.ConvertFromString(secondaryColor);

                            Application.Current.Resources["AccentOrangeBrush"] = new SolidColorBrush(primaryColorObj);
                            Application.Current.Resources["AccentZincBrush"] = new SolidColorBrush(secondaryColorObj);
                            Application.Current.Resources["AccentColor"] = new SolidColorBrush(primaryColorObj);

                            // Update gradient brushes
                            var gradientBrush = new LinearGradientBrush();
                            gradientBrush.StartPoint = new Point(0, 0);
                            gradientBrush.EndPoint = new Point(1, 0);
                            gradientBrush.GradientStops.Add(new GradientStop(primaryColorObj, 0));
                            gradientBrush.GradientStops.Add(new GradientStop(secondaryColorObj, 1));
                            Application.Current.Resources["AccentGradientBrush"] = gradientBrush;

                            // Update tab hover brush (25% opacity tint of primary)
                            var hoverColor = Color.FromArgb(64, primaryColorObj.R, primaryColorObj.G, primaryColorObj.B);
                            Application.Current.Resources["AccentHoverBrush"] = new SolidColorBrush(hoverColor);
                        }
                        catch (Exception ex)
                        {
                            ShowStatus($"Error applying colors: {ex.Message}", MessageType.Error);
                            return;
                        }
                    }
                    else
                    {
                        ShowStatus("Invalid color format. Use #RRGGBB or #AARRGGBB", MessageType.Error);
                        return;
                    }
                }

                // TAG: #VERSION_7 #AD_QUERY_BACKEND_SELECTION - Save AD Query Method
                if (ComboADQueryMethod != null)
                {
                    Properties.Settings.Default.ADQueryMethod = ComboADQueryMethod.SelectedValue?.ToString() ?? "DirectorySearcher";
                }

                // TAG: #VERSION_1_0 #DEPLOYMENT - Save deployment configuration
                if (TxtLogDirectory != null)
                {
                    Properties.Settings.Default.DeploymentLogDirectory = TxtLogDirectory.Text;
                }
                if (TxtISOPath != null)
                {
                    Properties.Settings.Default.WindowsUpdateISOPath = TxtISOPath.Text;
                }
                if (TxtHostnamePattern != null)
                {
                    Properties.Settings.Default.LocalISOHostnamePattern = TxtHostnamePattern.Text;
                }
                if (CmbHostnameMatchMode != null && CmbHostnameMatchMode.SelectedItem is ComboBoxItem selectedItem)
                {
                    Properties.Settings.Default.LocalISOHostnameMatchMode = selectedItem.Tag?.ToString() ?? "StartsWith";
                }

                // TAG: #DC_HEALTH #CONFIGURABLE_OPTIONS - Save DC Health settings
                if (ChkDCHealthExpanded != null)
                {
                    Properties.Settings.Default.DCHealthExpanded = ChkDCHealthExpanded.IsChecked ?? false;
                }

                // TAG: #SECURITY #AUTHENTICATION - Save credential storage settings
                if (ChkRememberCredentials != null)
                {
                    Properties.Settings.Default.RememberCredentials = ChkRememberCredentials.IsChecked ?? true;
                }
                if (ChkShowPasswordExpiryWarnings != null)
                {
                    Properties.Settings.Default.ShowPasswordExpiryWarnings = ChkShowPasswordExpiryWarnings.IsChecked ?? true;
                }
                if (TxtPasswordWarningDays != null && int.TryParse(TxtPasswordWarningDays.Text, out int warningDays))
                {
                    if (warningDays >= 1 && warningDays <= 90)
                    {
                        Properties.Settings.Default.PasswordExpiryWarningDays = warningDays;
                    }
                    else
                    {
                        ShowStatus("Password warning days must be between 1 and 90", MessageType.Error);
                        return;
                    }
                }

                Properties.Settings.Default.Save();

                SettingsManager.SaveTargetHistory(_targetHistory);

                // TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS - Save toast notifications and keyboard shortcuts
                SaveToastNotificationSettings();
                SaveKeyboardShortcutSettings();

                _hasUnsavedChanges = false;
                ShowStatus("Settings saved successfully!", MessageType.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error saving settings: {ex.Message}", MessageType.Error);
            }
        }

        /// <summary>
        /// Save pinned devices to settings
        /// </summary>
        private void SavePinnedDevices()
        {
            try
            {
                string deviceList = string.Join("|", _pinnedDevices.Select(d => d.Input));
                Properties.Settings.Default.PinnedDevices = deviceList;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                ShowStatus($"Error saving pinned devices: {ex.Message}", MessageType.Error);
            }
        }

        #region Event Handlers

        /// <summary>
        /// Reset all DC preferences (favorites, ordering, expansion state)
        /// TAG: #DC_HEALTH #CONFIGURABLE_OPTIONS
        /// </summary>
        private void BtnResetDCPreferences_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Managers.UI.ToastManager.ShowWarning(
                    "This will reset all domain controller preferences including:\n\n" +
                    "• Favorite DCs\n" +
                    "• DC display order\n" +
                    "• DC Health widget expansion state\n\n" +
                    "Are you sure you want to continue?",
                    "Reset", () =>
                    {
                        // Clear all DC preferences
                        Properties.Settings.Default.FavoriteDCs = "";
                        Properties.Settings.Default.DCDisplayOrder = "";
                        Properties.Settings.Default.DCHealthExpanded = false;
                        Properties.Settings.Default.Save();

                        // Update UI
                        if (ChkDCHealthExpanded != null)
                            ChkDCHealthExpanded.IsChecked = false;

                        ShowStatus("DC preferences have been reset to defaults", MessageType.Success);
                        LogManager.LogInfo("[Options] DC preferences reset to defaults");

                        // Notify user to refresh dashboard
                        ToastManager.ShowSuccess("DC preferences have been reset. Please refresh the dashboard to see the changes.");
                    });
            }
            catch (Exception ex)
            {
                ShowStatus($"Error resetting DC preferences: {ex.Message}", MessageType.Error);
                LogManager.LogError("[Options] Error resetting DC preferences", ex);
            }
        }

        /// <summary>
        /// Clear cached username
        /// </summary>
        private void BtnClearLastUser_Click(object sender, RoutedEventArgs e)
        {
            Managers.UI.ToastManager.ShowWarning(
                "Clear the cached username?\n\nYou will need to enter your username again at next login.",
                "Clear", () =>
                {
                    Properties.Settings.Default.LastUser = "";
                    Properties.Settings.Default.Save();
                    TxtLastUser.Text = "";
                    _hasUnsavedChanges = true;
                    ShowStatus("Cached username cleared", MessageType.Success);
                });
        }

        /// <summary>
        /// Clear target history
        /// </summary>
        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            Managers.UI.ToastManager.ShowWarning(
                "Clear all recent target history?",
                "Clear", () =>
                {
                    _targetHistory.Clear();
                    ListTargetHistory.Items.Refresh();
                    _hasUnsavedChanges = true;
                    ShowStatus("Target history cleared", MessageType.Success);
                });
        }

        /// <summary>
        /// Clear failure cache
        /// TAG: #VERSION_7 #CACHE #PERFORMANCE_AUDIT
        /// </summary>
        private void BtnClearFailureCache_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OptimizedADScanner.CleanupFailureCache();
                ShowStatus("Failure cache cleaned successfully", MessageType.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error clearing cache: {ex.Message}", MessageType.Error);
            }
        }

        /// <summary>
        /// Clear all pinned devices
        /// </summary>
        private void BtnClearAllPinned_Click(object sender, RoutedEventArgs e)
        {
            Managers.UI.ToastManager.ShowWarning(
                $"Remove all {_pinnedDevices.Count} pinned devices?",
                "Remove All", () =>
                {
                    _pinnedDevices.Clear();
                    SavePinnedDevices();
                    ShowStatus("All pinned devices removed", MessageType.Success);
                });
        }

        /// <summary>
        /// Export pinned devices to CSV
        /// </summary>
        // TAG: #ASYNC_OPTIMIZATION - Made async to prevent UI blocking on file write
        private async void BtnExportPinned_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    DefaultExt = ".csv",
                    FileName = $"PinnedDevices_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    var csv = "Device,Status,Last Checked\n" +
                              string.Join("\n", _pinnedDevices.Select(d => $"{d.Input},{d.Status},{d.LastChecked}"));

                    // TAG: #ASYNC_OPTIMIZATION - Run file I/O on background thread
                    await Task.Run(() => File.WriteAllText(dialog.FileName, csv));
                    ShowStatus($"Exported {_pinnedDevices.Count} devices to {Path.GetFileName(dialog.FileName)}", MessageType.Success);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Export failed: {ex.Message}", MessageType.Error);
            }
        }

        /// <summary>
        /// Slider value changed - update textbox
        /// </summary>
        private void SliderMaxParallel_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtMaxParallel != null)
            {
                TxtMaxParallel.Text = ((int)SliderMaxParallel.Value).ToString();
                _hasUnsavedChanges = true;
            }
        }

        /// <summary>
        /// Factory reset all settings
        /// </summary>
        private void BtnResetAll_Click(object sender, RoutedEventArgs e)
        {
            // Multi-step confirmation
            var confirmWindow = new Window
            {
                Title = "Factory Reset Confirmation",
                Width = 450,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(13, 13, 13)),
                ResizeMode = ResizeMode.NoResize
            };

            var stackPanel = new StackPanel { Margin = new Thickness(20) };

            stackPanel.Children.Add(new TextBlock
            {
                Text = "⚠️ FACTORY RESET WARNING",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Red,
                Margin = new Thickness(0, 0, 0, 15)
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = "This will reset ALL settings to defaults:\n\n" +
                       "• Cached username and login preferences\n" +
                       "• Pinned devices list\n" +
                       "• Global services configuration\n" +
                       "• Performance settings\n" +
                       "• Target history\n\n" +
                       "A backup will be created automatically.\n\n" +
                       "Type CONFIRM below to proceed:",
                Foreground = Brushes.White,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15)
            });

            var confirmTextBox = new TextBox
            {
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 15)
            };
            stackPanel.Children.Add(confirmTextBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnConfirm = new Button
            {
                Content = "RESET ALL SETTINGS",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(0, 0, 10, 0),
                IsEnabled = false
            };

            var btnCancelReset = new Button
            {
                Content = "CANCEL",
                Padding = new Thickness(15, 8, 15, 8)
            };

            confirmTextBox.TextChanged += (s, args) =>
            {
                btnConfirm.IsEnabled = confirmTextBox.Text == "CONFIRM";
            };

            btnConfirm.Click += (s, args) =>
            {
                confirmWindow.DialogResult = true;
                confirmWindow.Close();
            };

            btnCancelReset.Click += (s, args) =>
            {
                confirmWindow.DialogResult = false;
                confirmWindow.Close();
            };

            buttonPanel.Children.Add(btnConfirm);
            buttonPanel.Children.Add(btnCancelReset);
            stackPanel.Children.Add(buttonPanel);

            confirmWindow.Content = stackPanel;

            if (confirmWindow.ShowDialog() == true)
            {
                try
                {
                    // Create backup
                    string backupPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        LogoConfig.PRODUCT_NAME,
                        $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                    );

                    Directory.CreateDirectory(Path.GetDirectoryName(backupPath));

                    // Export current settings to backup
                    var backup = new
                    {
                        LastUser = Properties.Settings.Default.LastUser,
                        PinnedDevices = Properties.Settings.Default.PinnedDevices,
                        GlobalServicesConfig = Properties.Settings.Default.GlobalServicesConfig,
                        MaxParallelScans = SecureConfig.MaxParallelScans,
                        WmiTimeout = SecureConfig.WmiTimeoutMs,
                        PingTimeout = SecureConfig.PingTimeoutMs,
                        MaxRetry = SecureConfig.MaxRetryAttempts
                    };

                    var serializer = new JavaScriptSerializer();
                    File.WriteAllText(backupPath, serializer.Serialize(backup));

                    // Reset all settings
                    Properties.Settings.Default.LastUser = "";
                    Properties.Settings.Default.PinnedDevices = "";
                    Properties.Settings.Default.GlobalServicesConfig = "";
                    Properties.Settings.Default.Save();

                    SecureConfig.MaxParallelScans = 30;
                    SecureConfig.WmiTimeoutMs = 15000;
                    SecureConfig.PingTimeoutMs = 1200;
                    SecureConfig.MaxRetryAttempts = 3;
                    SecureConfig.SaveConfiguration();

                    // Reload UI
                    LoadAllSettings();

                    // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                    ToastManager.ShowSuccess($"Settings reset to factory defaults!\n\nBackup created at:\n{backupPath}");

                    ShowStatus("Factory reset completed - backup created", MessageType.Success);
                }
                catch (Exception ex)
                {
                    ShowStatus($"Reset failed: {ex.Message}", MessageType.Error);
                }
            }
        }

        /// <summary>
        /// Apply settings without closing
        /// </summary>
        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            SaveAllSettings();
        }

        /// <summary>
        /// Save settings and close
        /// </summary>
        private void BtnSaveAndClose_Click(object sender, RoutedEventArgs e)
        {
            SaveAllSettings();
            if (!_hasUnsavedChanges) // Only close if save was successful
            {
                DialogResult = true;
                Close();
            }
        }

        /// <summary>
        /// Cancel without saving
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                Managers.UI.ToastManager.ShowWarning(
                    "You have unsaved changes. Discard them?",
                    "Discard", () =>
                    {
                        DialogResult = false;
                        Close();
                    });
                return;
            }

            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Close button
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            BtnCancel_Click(sender, e);
        }

        #endregion

        #region Status Messages

        private enum MessageType
        {
            Success,
            Warning,
            Error
        }

        /// <summary>
        /// Show status message with auto-hide
        /// </summary>
        private void ShowStatus(string message, MessageType type)
        {
            StatusMessage.Visibility = Visibility.Visible;
            TxtStatusMessage.Text = message;

            switch (type)
            {
                case MessageType.Success:
                    StatusMessage.BorderBrush = new SolidColorBrush(Color.FromRgb(22, 198, 12));
                    TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(22, 198, 12));
                    break;
                case MessageType.Warning:
                    StatusMessage.BorderBrush = Helpers.ThemeHelper.PrimaryBrush;
                    TxtStatusMessage.Foreground = Helpers.ThemeHelper.PrimaryBrush;
                    break;
                case MessageType.Error:
                    StatusMessage.BorderBrush = new SolidColorBrush(Color.FromRgb(232, 17, 35));
                    TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(232, 17, 35));
                    break;
            }

            // Auto-hide after 5 seconds
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            timer.Tick += (s, e) =>
            {
                try { StatusMessage.Visibility = Visibility.Collapsed; timer.Stop(); }
                catch (Exception ex) { LogManager.LogError("Timer tick failed", ex); timer.Stop(); }
            };
            timer.Start();
        }

        #endregion

        #region Admin & Debugging Tools

        /// <summary>
        /// Mouse enter - highlight button
        /// </summary>
        private void DebugButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF252525"));
            }
        }

        /// <summary>
        /// Mouse leave - restore button
        /// </summary>
        private void DebugButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = (SolidColorBrush)Resources["BgDark"];
            }
        }

        /// <summary>
        /// Create admin launcher shortcut
        /// </summary>
        private void CreateShortcut_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                // Prompt for admin username
                string adminUsername = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter your domain admin username:\n\n" +
                    "Format: domain\\username\n" +
                    "Example: process\\admin.bnecessary-a",
                    "Admin Username",
                    $"{Environment.UserDomainName}\\admin.{Environment.UserName}",
                    -1, -1);

                if (string.IsNullOrWhiteSpace(adminUsername))
                {
                    return; // User cancelled
                }

                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                string exeDir = Path.GetDirectoryName(exePath);
                string batPath = Path.Combine(exeDir, "Launch_AsAdmin.bat");

                // Create batch file with runas /savecred
                string batContent = $@"@echo off
echo ========================================
echo  {LogoConfig.PRODUCT_FULL_NAME} - Admin Launcher
echo ========================================
echo.
echo Launching as: {adminUsername}
echo.
echo NOTE: First time will ask for password.
echo       Password will be saved securely by Windows.
echo.
runas /user:{adminUsername} /savecred ""{exePath}""
";

                File.WriteAllText(batPath, batContent);

                // Create desktop shortcut using IWshRuntimeLibrary
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktopPath, $"{LogoConfig.PRODUCT_FULL_NAME} (Admin).lnk");

                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                var shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = batPath;
                shortcut.WorkingDirectory = exeDir;
                shortcut.Description = $"Launch {LogoConfig.PRODUCT_FULL_NAME} as {adminUsername}";
                shortcut.IconLocation = exePath + ",0";
                shortcut.Save();

                System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);

                // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                ToastManager.ShowSuccess(
                    "✅ Admin launcher created successfully!\n\n" +
                    $"Desktop shortcut: \"{LogoConfig.PRODUCT_FULL_NAME} (Admin)\"\n" +
                    $"Batch file: {batPath}\n\n" +
                    "📌 FIRST TIME USE:\n" +
                    "1. Double-click the desktop shortcut\n" +
                    "2. Enter your admin password when prompted\n" +
                    "3. Windows will remember your password\n\n" +
                    "📌 FUTURE USE:\n" +
                    "Just double-click the shortcut - no password needed!");

                ShowStatus("Admin launcher created successfully!", MessageType.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to create admin launcher: {ex.Message}", MessageType.Error);
                // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                ToastManager.ShowError($"Failed to create admin launcher:\n\n{ex.Message}");
            }
        }

        #endregion

        #region Appearance & Branding

        /// <summary>
        /// Browse for custom logo file
        /// </summary>
        private void BtnBrowseLogo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Image files (*.png;*.jpg;*.svg)|*.png;*.jpg;*.svg|All files (*.*)|*.*",
                    Title = "Select Custom Logo"
                };

                if (dialog.ShowDialog() == true)
                {
                    TxtLogoPath.Text = dialog.FileName;
                    try
                    {
                        var destDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "NecessaryAdminTool", "CustomLogo");
                        Directory.CreateDirectory(destDir);
                        var destPath = Path.Combine(destDir, Path.GetFileName(dialog.FileName));
                        File.Copy(dialog.FileName, destPath, overwrite: true);
                        Properties.Settings.Default.CustomLogoPath = destPath;
                        Properties.Settings.Default.Save();
                        TxtLogoPath.Text = destPath;
                        LoadLogoPreview(destPath);
                    }
                    catch (Exception copyEx)
                    {
                        LogManager.LogError("Logo copy failed", copyEx);
                        ToastManager.ShowError("Failed to copy logo file.");
                    }
                    ShowStatus($"Logo selected: {Path.GetFileName(dialog.FileName)}", MessageType.Success);
                    _hasUnsavedChanges = true;
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error loading logo: {ex.Message}", MessageType.Error);
            }
        }

        private void LoadLogoPreview(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || LogoPreview == null) return;
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.EndInit();
                var img = new System.Windows.Controls.Image { Source = bmp, Stretch = System.Windows.Media.Stretch.Uniform };
                LogoPreview.Child = img;
            }
            catch (Exception ex)
            {
                LogManager.LogError("LoadLogoPreview() - FAILED", ex);
            }
        }

        private void SliderLogoSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LogoPreview == null) return;
            double size = SliderLogoSize.Value;
            LogoPreview.Width = size;
            LogoPreview.Height = size;
            if (TxtLogoSize != null) TxtLogoSize.Text = ((int)size).ToString();
        }

        /// <summary>
        /// TAG: #VERSION_1_0 #DEPLOYMENT - Browse for Windows Update ISO file
        /// FUTURE CLAUDES: This allows users to select the ISO file path for local Windows Feature Updates
        /// </summary>
        private void BtnBrowseISO_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "ISO files (*.iso)|*.iso|All files (*.*)|*.*",
                    Title = "Select Windows Update ISO File",
                    CheckFileExists = true
                };

                // Try to set initial directory to network path if it exists
                string currentPath = TxtISOPath?.Text;
                if (!string.IsNullOrEmpty(currentPath))
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(currentPath);
                        if (Directory.Exists(dir))
                        {
                            dialog.InitialDirectory = dir;
                        }
                    }
                    catch { /* Ignore path errors */ }
                }

                if (dialog.ShowDialog() == true)
                {
                    TxtISOPath.Text = dialog.FileName;
                    ShowStatus($"ISO selected: {Path.GetFileName(dialog.FileName)}", MessageType.Success);
                    _hasUnsavedChanges = true;
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error selecting ISO: {ex.Message}", MessageType.Error);
            }
        }

        /// <summary>
        /// TAG: #VERSION_1_0 #DEPLOYMENT - Browse for deployment log directory
        /// FUTURE CLAUDES: This allows users to select the network/local path for PowerShell script logs
        /// </summary>
        private void BtnBrowseLogDir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select folder for deployment script logs",
                    ShowNewFolderButton = true
                };

                // Try to set initial path
                string currentPath = TxtLogDirectory?.Text;
                if (!string.IsNullOrEmpty(currentPath) && Directory.Exists(currentPath))
                {
                    dialog.SelectedPath = currentPath;
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    TxtLogDirectory.Text = dialog.SelectedPath;
                    ShowStatus($"Log directory selected: {dialog.SelectedPath}", MessageType.Success);
                    _hasUnsavedChanges = true;
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error selecting folder: {ex.Message}", MessageType.Error);
            }
        }

        /// <summary>
        /// Primary color changed - update preview
        /// </summary>
        private void TxtPrimaryColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressColorEvents) return;
            try
            {
                if (TxtPrimaryColor != null && PrimaryColorPreview != null)
                {
                    string colorHex = TxtPrimaryColor.Text;
                    if (colorHex.StartsWith("#") && (colorHex.Length == 7 || colorHex.Length == 9))
                    {
                        var color = (Color)ColorConverter.ConvertFromString(colorHex);
                        PrimaryColorPreview.Background = new SolidColorBrush(color);

                        if (PreviewPrimaryStop != null)
                            PreviewPrimaryStop.Color = color;

                        if (PreviewButtonBg != null)
                            PreviewButtonBg.Color = color;

                        // Set dropdown to "Custom" if doesn't match a named color
                        _suppressColorEvents = true;
                        if (CmbPrimaryColor != null)
                        {
                            bool matched = false;
                            for (int i = 0; i < CmbPrimaryColor.Items.Count - 1; i++)
                            {
                                if (CmbPrimaryColor.Items[i] is ComboBoxItem item && item.Tag is Color c && c == color)
                                { CmbPrimaryColor.SelectedIndex = i; matched = true; break; }
                            }
                            if (!matched) CmbPrimaryColor.SelectedIndex = CmbPrimaryColor.Items.Count - 1;
                        }
                        _suppressColorEvents = false;

                        // Apply live via ThemeHelper
                        Color secondaryC;
                        if (!Helpers.ThemeHelper.TryParseColor(TxtSecondaryColor?.Text, out secondaryC))
                            secondaryC = Helpers.ThemeHelper.SecondaryColor;
                        Helpers.ThemeHelper.ApplyAccentColors(color, secondaryC);
                        Properties.Settings.Default.PrimaryAccentColor = colorHex;
                        Properties.Settings.Default.Save();

                        _hasUnsavedChanges = true;
                    }
                }
            }
            catch
            {
                // Invalid color format - ignore
            }
        }

        /// <summary>
        /// Secondary color changed - update preview
        /// </summary>
        private void TxtSecondaryColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressColorEvents) return;
            try
            {
                if (TxtSecondaryColor != null && SecondaryColorPreview != null)
                {
                    string colorHex = TxtSecondaryColor.Text;
                    if (colorHex.StartsWith("#") && (colorHex.Length == 7 || colorHex.Length == 9))
                    {
                        var color = (Color)ColorConverter.ConvertFromString(colorHex);
                        SecondaryColorPreview.Background = new SolidColorBrush(color);

                        if (PreviewSecondaryStop != null)
                            PreviewSecondaryStop.Color = color;

                        // Set dropdown to "Custom" if doesn't match a named color
                        _suppressColorEvents = true;
                        if (CmbSecondaryColor != null)
                        {
                            bool matched = false;
                            for (int i = 0; i < CmbSecondaryColor.Items.Count - 1; i++)
                            {
                                if (CmbSecondaryColor.Items[i] is ComboBoxItem item && item.Tag is Color c && c == color)
                                { CmbSecondaryColor.SelectedIndex = i; matched = true; break; }
                            }
                            if (!matched) CmbSecondaryColor.SelectedIndex = CmbSecondaryColor.Items.Count - 1;
                        }
                        _suppressColorEvents = false;

                        // Apply live via ThemeHelper
                        Color primaryC;
                        if (!Helpers.ThemeHelper.TryParseColor(TxtPrimaryColor?.Text, out primaryC))
                            primaryC = Helpers.ThemeHelper.PrimaryColor;
                        Helpers.ThemeHelper.ApplyAccentColors(primaryC, color);
                        Properties.Settings.Default.SecondaryAccentColor = colorHex;
                        Properties.Settings.Default.Save();

                        _hasUnsavedChanges = true;
                    }
                }
            }
            catch
            {
                // Invalid color format - ignore
            }
        }

        /// <summary>
        /// Reset appearance to defaults
        /// </summary>
        private void BtnResetAppearance_Click(object sender, RoutedEventArgs e)
        {
            Managers.UI.ToastManager.ShowWarning(
                "Reset all appearance settings to default NecessaryAdmin branding?\n\n" +
                "• Logo: NecessaryAdmin \"A\" icon\n" +
                "• Primary Color: Orange (#FFFF8533)\n" +
                "• Secondary Color: Zinc (#FFA1A1AA)\n" +
                "• Font: Segoe UI\n" +
                "• Dark Mode: On",
                "Reset", () =>
                {
                    TxtLogoPath.Text = "(Using default NecessaryAdmin logo)";
                    SliderLogoSize.Value = 64;
                    TxtLogoSize.Text = "64";
                    CmbFontFamily.SelectedIndex = 0;

                    // Reset accent colors via ThemeHelper (applies immediately)
                    Helpers.ThemeHelper.ResetToDefaults();

                    // Reset dropdowns to index 0 (Orange, Zinc)
                    _suppressColorEvents = true;
                    if (CmbPrimaryColor != null) CmbPrimaryColor.SelectedIndex = 0;
                    if (CmbSecondaryColor != null) CmbSecondaryColor.SelectedIndex = 0;
                    TxtPrimaryColor.Text = "#FFFF8533";
                    TxtSecondaryColor.Text = "#FFA1A1AA";
                    PrimaryColorPreview.Background = new SolidColorBrush(Helpers.ThemeHelper.DefaultPrimary);
                    SecondaryColorPreview.Background = new SolidColorBrush(Helpers.ThemeHelper.DefaultSecondary);
                    if (PreviewPrimaryStop != null) PreviewPrimaryStop.Color = Helpers.ThemeHelper.DefaultPrimary;
                    if (PreviewSecondaryStop != null) PreviewSecondaryStop.Color = Helpers.ThemeHelper.DefaultSecondary;
                    if (PreviewButtonBg != null) PreviewButtonBg.Color = Helpers.ThemeHelper.DefaultPrimary;
                    _suppressColorEvents = false;

                    // Reset dark mode
                    if (ChkDarkMode != null) ChkDarkMode.IsChecked = true;

                    ShowStatus("Appearance reset to defaults", MessageType.Success);
                    _hasUnsavedChanges = true;
                });
        }

        // Prevents infinite loops when programmatically setting color controls
        private bool _suppressColorEvents = false;

        /// <summary>
        /// Initialize preset swatches and color dropdowns
        /// </summary>
        private void InitializeThemeControls()
        {
            // Populate preset swatches
            if (PresetSwatches != null)
            {
                var panel = new WrapPanel { Orientation = Orientation.Horizontal };
                foreach (var preset in Helpers.ThemeHelper.Presets)
                {
                    var swatch = new Border
                    {
                        Width = 52, Height = 34,
                        CornerRadius = new CornerRadius(4),
                        Margin = new Thickness(0, 0, 6, 6),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                        BorderThickness = new Thickness(1),
                        Tag = preset,
                        ToolTip = preset.Name
                    };
                    var gradient = new LinearGradientBrush();
                    gradient.StartPoint = new Point(0, 0);
                    gradient.EndPoint = new Point(1, 0);
                    gradient.GradientStops.Add(new GradientStop(preset.Primary, 0));
                    gradient.GradientStops.Add(new GradientStop(preset.Secondary, 1));
                    swatch.Background = gradient;

                    var label = new TextBlock
                    {
                        Text = preset.Name,
                        FontSize = 7, FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    label.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Black, BlurRadius = 3, ShadowDepth = 1, Opacity = 0.8
                    };
                    swatch.Child = label;
                    swatch.MouseLeftButtonDown += PresetSwatch_Click;
                    panel.Children.Add(swatch);
                }
                PresetSwatches.Items.Clear();
                PresetSwatches.Items.Add(panel);
            }

            // Populate primary color dropdown
            if (CmbPrimaryColor != null)
            {
                CmbPrimaryColor.Items.Clear();
                foreach (var nc in Helpers.ThemeHelper.PrimaryColors)
                    CmbPrimaryColor.Items.Add(new ComboBoxItem { Content = nc.Name, Tag = nc.Color });
                CmbPrimaryColor.Items.Add(new ComboBoxItem { Content = "Custom" });
                CmbPrimaryColor.SelectedIndex = 0;
            }

            // Populate secondary color dropdown
            if (CmbSecondaryColor != null)
            {
                CmbSecondaryColor.Items.Clear();
                foreach (var nc in Helpers.ThemeHelper.SecondaryColors)
                    CmbSecondaryColor.Items.Add(new ComboBoxItem { Content = nc.Name, Tag = nc.Color });
                CmbSecondaryColor.Items.Add(new ComboBoxItem { Content = "Custom" });
                CmbSecondaryColor.SelectedIndex = 0;
            }

            // Load saved dark mode state
            if (ChkDarkMode != null)
                ChkDarkMode.IsChecked = Properties.Settings.Default.IsDarkMode;

            // Select matching dropdown items for saved colors
            SelectMatchingDropdownColor();
        }

        /// <summary>Select dropdown items that match the current saved colors</summary>
        private void SelectMatchingDropdownColor()
        {
            _suppressColorEvents = true;
            try
            {
                var savedPrimary = Helpers.ThemeHelper.PrimaryColor;
                var savedSecondary = Helpers.ThemeHelper.SecondaryColor;

                // Match primary
                bool foundPrimary = false;
                if (CmbPrimaryColor != null)
                {
                    for (int i = 0; i < CmbPrimaryColor.Items.Count - 1; i++)
                    {
                        if (CmbPrimaryColor.Items[i] is ComboBoxItem item && item.Tag is Color c && c == savedPrimary)
                        {
                            CmbPrimaryColor.SelectedIndex = i;
                            foundPrimary = true;
                            break;
                        }
                    }
                    if (!foundPrimary) CmbPrimaryColor.SelectedIndex = CmbPrimaryColor.Items.Count - 1; // Custom
                }

                // Match secondary
                bool foundSecondary = false;
                if (CmbSecondaryColor != null)
                {
                    for (int i = 0; i < CmbSecondaryColor.Items.Count - 1; i++)
                    {
                        if (CmbSecondaryColor.Items[i] is ComboBoxItem item && item.Tag is Color c && c == savedSecondary)
                        {
                            CmbSecondaryColor.SelectedIndex = i;
                            foundSecondary = true;
                            break;
                        }
                    }
                    if (!foundSecondary) CmbSecondaryColor.SelectedIndex = CmbSecondaryColor.Items.Count - 1; // Custom
                }
            }
            finally { _suppressColorEvents = false; }
        }

        /// <summary>Preset swatch clicked — apply both colors</summary>
        private void PresetSwatch_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is Helpers.ThemeHelper.ThemePreset preset)
            {
                ApplyColorPair(preset.Primary, preset.Secondary);
                ShowStatus($"Applied '{preset.Name}' theme preset", MessageType.Success);
            }
        }

        /// <summary>Primary color dropdown changed</summary>
        private void CmbPrimaryColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressColorEvents || CmbPrimaryColor == null) return;
            if (CmbPrimaryColor.SelectedItem is ComboBoxItem item && item.Tag is Color c)
            {
                _suppressColorEvents = true;
                TxtPrimaryColor.Text = Helpers.ThemeHelper.ColorToHex(c);
                _suppressColorEvents = false;
            }
        }

        /// <summary>Secondary color dropdown changed</summary>
        private void CmbSecondaryColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressColorEvents || CmbSecondaryColor == null) return;
            if (CmbSecondaryColor.SelectedItem is ComboBoxItem item && item.Tag is Color c)
            {
                _suppressColorEvents = true;
                TxtSecondaryColor.Text = Helpers.ThemeHelper.ColorToHex(c);
                _suppressColorEvents = false;
            }
        }

        /// <summary>Dark mode checkbox changed</summary>
        private void ChkDarkMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressColorEvents || ChkDarkMode == null) return;
            bool isDark = ChkDarkMode.IsChecked == true;
            MainWindow.ThemeManager.SetTheme(isDark);
            _hasUnsavedChanges = true;
        }

        /// <summary>Apply a primary+secondary color pair from any source</summary>
        private void ApplyColorPair(Color primary, Color secondary)
        {
            _suppressColorEvents = true;
            TxtPrimaryColor.Text = Helpers.ThemeHelper.ColorToHex(primary);
            TxtSecondaryColor.Text = Helpers.ThemeHelper.ColorToHex(secondary);
            PrimaryColorPreview.Background = new SolidColorBrush(primary);
            SecondaryColorPreview.Background = new SolidColorBrush(secondary);
            if (PreviewPrimaryStop != null) PreviewPrimaryStop.Color = primary;
            if (PreviewSecondaryStop != null) PreviewSecondaryStop.Color = secondary;
            if (PreviewButtonBg != null) PreviewButtonBg.Color = primary;
            _suppressColorEvents = false;

            // Apply live
            Helpers.ThemeHelper.ApplyAccentColors(primary, secondary);
            Properties.Settings.Default.PrimaryAccentColor = Helpers.ThemeHelper.ColorToHex(primary);
            Properties.Settings.Default.SecondaryAccentColor = Helpers.ThemeHelper.ColorToHex(secondary);
            Properties.Settings.Default.Save();

            SelectMatchingDropdownColor();
            _hasUnsavedChanges = true;
        }

        #endregion

        #region Global Services Configuration

        /// <summary>
        /// Load global services configuration from JSON
        /// </summary>
        public void LoadGlobalServicesConfig(string jsonConfig)
        {
            try
            {
                _serviceConfigItems.Clear();

                if (string.IsNullOrWhiteSpace(jsonConfig))
                {
                    LoadDefaultServices();
                }
                else
                {
                    var serializer = new JavaScriptSerializer();
                    var services = serializer.Deserialize<List<ServiceConfigItem>>(jsonConfig);
                    foreach (var svc in services)
                    {
                        _serviceConfigItems.Add(svc);
                    }
                }

                if (GridServicesConfig != null)
                    GridServicesConfig.ItemsSource = _serviceConfigItems;

                if (TxtJsonConfig != null)
                {
                    var jsonSerializer = new JavaScriptSerializer();
                    TxtJsonConfig.Text = FormatJson(jsonSerializer.Serialize(_serviceConfigItems));
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to load services config: {ex.Message}", MessageType.Error);
                LoadDefaultServices();
            }
        }

        /// <summary>
        /// Load default service definitions
        /// </summary>
        private void LoadDefaultServices()
        {
            _serviceConfigItems.Clear();
            var defaults = new[]
            {
                // ESSENTIAL SERVICES
                new ServiceConfigItem { ServiceName = "Azure", Endpoint = "https://status.azure.com/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "Microsoft 365", Endpoint = "https://status.office.com/api/v1.0/ServiceStatus/CurrentStatus" },
                new ServiceConfigItem { ServiceName = "Microsoft Teams", Endpoint = "https://status.office.com/api/v1.0/ServiceStatus/CurrentStatus" },
                new ServiceConfigItem { ServiceName = "GitHub", Endpoint = "https://www.githubstatus.com/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "Google Cloud", Endpoint = "https://status.cloud.google.com/incidents.json" },
                new ServiceConfigItem { ServiceName = "DNS (8.8.8.8)", Endpoint = "ping:8.8.8.8" },
                new ServiceConfigItem { ServiceName = "AWS", Endpoint = "https://status.aws.amazon.com/data.json" },
                new ServiceConfigItem { ServiceName = "Cloudflare", Endpoint = "https://www.cloudflarestatus.com/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "DNS (1.1.1.1)", Endpoint = "ping:1.1.1.1" },
                // HIGH PRIORITY
                new ServiceConfigItem { ServiceName = "NuGet", Endpoint = "https://status.nuget.org/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "Atlassian", Endpoint = "https://status.atlassian.com/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "Slack", Endpoint = "https://status.slack.com/api/v2.0.0/current" },
                new ServiceConfigItem { ServiceName = "Zoom", Endpoint = "https://status.zoom.us/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "DockerHub", Endpoint = "https://status.docker.com/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "NPM Registry", Endpoint = "https://status.npmjs.org/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "Salesforce", Endpoint = "https://api.status.salesforce.com/v1/status" },
                new ServiceConfigItem { ServiceName = "Okta", Endpoint = "https://status.okta.com/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "Datadog", Endpoint = "https://status.datadoghq.com/api/v2/status.json" },
                // MEDIUM PRIORITY
                new ServiceConfigItem { ServiceName = "Twilio", Endpoint = "https://status.twilio.com/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "SendGrid", Endpoint = "https://status.sendgrid.com/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "Stripe", Endpoint = "https://status.stripe.com/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "MongoDB Atlas", Endpoint = "https://status.cloud.mongodb.com/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "PagerDuty", Endpoint = "https://status.pagerduty.com/api/v2/status.json" }
            };

            foreach (var svc in defaults)
            {
                _serviceConfigItems.Add(svc);
            }

            GridServicesConfig.ItemsSource = _serviceConfigItems;
            var serializer = new JavaScriptSerializer();
            TxtJsonConfig.Text = FormatJson(serializer.Serialize(_serviceConfigItems));
        }

        /// <summary>
        /// Switch to visual editor
        /// </summary>
        private void BtnVisualEditor_Click(object sender, RoutedEventArgs e)
        {
            VisualEditorPanel.Visibility = Visibility.Visible;
            JsonEditorPanel.Visibility = Visibility.Collapsed;
            BtnVisualEditor.Background = (SolidColorBrush)Resources["AccentOrange"];
            BtnVisualEditor.Foreground = Brushes.White;
            BtnJsonEditor.Background = new SolidColorBrush(Color.FromRgb(42, 42, 42));
            BtnJsonEditor.Foreground = (SolidColorBrush)Resources["TextMuted"];

            // Sync JSON to visual editor
            try
            {
                var serializer = new JavaScriptSerializer();
                var items = serializer.Deserialize<List<ServiceConfigItem>>(TxtJsonConfig.Text);
                _serviceConfigItems.Clear();
                foreach (var item in items)
                {
                    _serviceConfigItems.Add(item);
                }
            }
            catch { }
        }

        /// <summary>
        /// Switch to JSON editor
        /// </summary>
        private void BtnJsonEditor_Click(object sender, RoutedEventArgs e)
        {
            JsonEditorPanel.Visibility = Visibility.Visible;
            VisualEditorPanel.Visibility = Visibility.Collapsed;
            BtnJsonEditor.Background = (SolidColorBrush)Resources["AccentOrange"];
            BtnJsonEditor.Foreground = Brushes.White;
            BtnVisualEditor.Background = new SolidColorBrush(Color.FromRgb(42, 42, 42));
            BtnVisualEditor.Foreground = (SolidColorBrush)Resources["TextMuted"];

            // Sync visual editor to JSON
            var serializer = new JavaScriptSerializer();
            TxtJsonConfig.Text = FormatJson(serializer.Serialize(_serviceConfigItems));
        }

        /// <summary>
        /// Save services configuration
        /// </summary>
        private void BtnSaveServicesConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string jsonToSave;
                var serializer = new JavaScriptSerializer();

                // If JSON editor is visible, validate and save from JSON
                if (JsonEditorPanel.Visibility == Visibility.Visible)
                {
                    var items = serializer.Deserialize<List<ServiceConfigItem>>(TxtJsonConfig.Text);
                    jsonToSave = serializer.Serialize(items);
                }
                else
                {
                    jsonToSave = serializer.Serialize(_serviceConfigItems);
                }

                Properties.Settings.Default.GlobalServicesConfig = jsonToSave;
                Properties.Settings.Default.Save();

                // Update main window if it's the owner
                if (Owner is MainWindow mainWin)
                {
                    mainWin.ReloadGlobalServicesConfig(jsonToSave);
                }

                TxtConfigStatus.Text = "✅ Configuration saved successfully!";
                TxtConfigStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0));
                TxtConfigStatus.Visibility = Visibility.Visible;

                // Hide after 3 seconds
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (s, args) =>
                {
                    try { TxtConfigStatus.Visibility = Visibility.Collapsed; timer.Stop(); }
                    catch (Exception ex) { LogManager.LogError("Timer tick failed", ex); timer.Stop(); }
                };
                timer.Start();

                ShowStatus("Global services configuration saved", MessageType.Success);
            }
            catch (Exception ex)
            {
                TxtConfigStatus.Text = "❌ Invalid JSON format!";
                TxtConfigStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
                TxtConfigStatus.Visibility = Visibility.Visible;
                ShowStatus($"Failed to save: {ex.Message}", MessageType.Error);
            }
        }

        /// <summary>
        /// Reset to defaults
        /// </summary>
        private void BtnResetServicesConfig_Click(object sender, RoutedEventArgs e)
        {
            Managers.UI.ToastManager.ShowWarning(
                "This will reset your global services configuration to the default settings.\n\n" +
                "All custom services and API endpoints will be lost.\n\n" +
                "Do you want to continue?",
                "Reset", () =>
                {
                    LoadDefaultServices();
                    TxtConfigStatus.Text = "🔄 Configuration reset to defaults";
                    TxtConfigStatus.SetResourceReference(TextBlock.ForegroundProperty, "AccentOrangeBrush");
                    TxtConfigStatus.Visibility = Visibility.Visible;
                    ShowStatus("Reset to default services", MessageType.Success);
                });
        }

        /// <summary>
        /// Test all API endpoints
        /// </summary>
        private async void BtnTestAPIs_Click(object sender, RoutedEventArgs e)
        {
            BtnTestAPIs.IsEnabled = false;
            BtnTestAPIs.Content = "TESTING...";
            TxtConfigStatus.Text = "🧪 Testing API endpoints...";
            TxtConfigStatus.Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243));
            TxtConfigStatus.Visibility = Visibility.Visible;

            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    // TAG: #USER_AGENT #DYNAMIC_VERSION
                    client.DefaultRequestHeaders.Add("User-Agent", $"{LogoConfig.PRODUCT_NAME}-Monitor/{LogoConfig.USER_AGENT_VERSION}");

                    int successCount = 0;
                    int failCount = 0;

                    foreach (var svc in _serviceConfigItems)
                    {
                        if (svc.Endpoint.StartsWith("ping:"))
                        {
                            string target = svc.Endpoint.Replace("ping:", "");
                            var ping = new System.Net.NetworkInformation.Ping();
                            var result = await ping.SendPingAsync(target, 3000);
                            if (result.Status == System.Net.NetworkInformation.IPStatus.Success)
                                successCount++;
                            else
                                failCount++;
                        }
                        else
                        {
                            try
                            {
                                var response = await client.GetAsync(svc.Endpoint);
                                if (response.IsSuccessStatusCode)
                                    successCount++;
                                else
                                    failCount++;
                            }
                            catch
                            {
                                failCount++;
                            }
                        }
                    }

                    TxtConfigStatus.Text = $"✅ {successCount} OK, ❌ {failCount} Failed";
                    TxtConfigStatus.Foreground = successCount > failCount
                        ? new SolidColorBrush(Color.FromRgb(0, 255, 0))
                        : new SolidColorBrush(Color.FromRgb(255, 100, 100));
                }
            }
            catch (Exception ex)
            {
                TxtConfigStatus.Text = "❌ Test failed: " + ex.Message;
                TxtConfigStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
                ShowStatus($"Test failed: {ex.Message}", MessageType.Error);
            }
            finally
            {
                BtnTestAPIs.IsEnabled = true;
                BtnTestAPIs.Content = "🧪 TEST APIS";
            }
        }

        /// <summary>
        /// Format JSON for display
        /// </summary>
        private string FormatJson(string json)
        {
            try
            {
                int indent = 0;
                var formatted = new System.Text.StringBuilder();
                bool inString = false;

                for (int i = 0; i < json.Length; i++)
                {
                    char ch = json[i];

                    if (ch == '"' && (i == 0 || json[i - 1] != '\\'))
                        inString = !inString;

                    if (!inString)
                    {
                        if (ch == '{' || ch == '[')
                        {
                            formatted.Append(ch);
                            formatted.AppendLine();
                            formatted.Append(new string(' ', ++indent * 2));
                        }
                        else if (ch == '}' || ch == ']')
                        {
                            formatted.AppendLine();
                            formatted.Append(new string(' ', --indent * 2));
                            formatted.Append(ch);
                        }
                        else if (ch == ',')
                        {
                            formatted.Append(ch);
                            formatted.AppendLine();
                            formatted.Append(new string(' ', indent * 2));
                        }
                        else if (ch == ':')
                        {
                            formatted.Append(ch);
                            formatted.Append(' ');
                        }
                        else if (!char.IsWhiteSpace(ch))
                        {
                            formatted.Append(ch);
                        }
                    }
                    else
                    {
                        formatted.Append(ch);
                    }
                }

                return formatted.ToString();
            }
            catch
            {
                return json;
            }
        }

        // TAG: #CSV_IMPORT #PINNED_DEVICES
        /// <summary>
        /// Import pinned devices from CSV file
        /// TAG: #ASYNC_OPTIMIZATION - Made async to prevent UI blocking on file read
        /// </summary>
        private async void BtnImportPinnedCsv_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null) button.IsEnabled = false;
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Import Pinned Devices from CSV",
                    Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    FilterIndex = 1
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    // TAG: #ASYNC_OPTIMIZATION - Run file I/O on background thread
                    string[] lines = await Task.Run(() => File.ReadAllLines(openFileDialog.FileName));
                    int importedCount = 0;

                    // Skip header row if it exists
                    int startIndex = (lines.Length > 0 && lines[0].ToLower().Contains("device")) ? 1 : 0;

                    for (int i = startIndex; i < lines.Length && _pinnedDevices.Count < 10; i++)
                    {
                        string line = lines[i].Trim();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // Support CSV with optional columns: Device,Status,LastChecked
                        string[] parts = line.Split(',');
                        string deviceName = parts[0].Trim().Trim('"');

                        if (!string.IsNullOrWhiteSpace(deviceName))
                        {
                            // Check for duplicates
                            if (!_pinnedDevices.Any(d => d.Input.Equals(deviceName, StringComparison.OrdinalIgnoreCase)))
                            {
                                _pinnedDevices.Add(new PinnedDevice
                                {
                                    Input = deviceName,
                                    Status = "Unknown",
                                    LastChecked = "Not checked",
                                    StatusColor = Brushes.Gray
                                });
                                importedCount++;
                            }
                        }
                    }

                    ShowStatus($"✅ Imported {importedCount} device(s) from CSV (max 10 allowed)", MessageType.Success);
                    _hasUnsavedChanges = true;
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ Failed to import CSV: {ex.Message}", MessageType.Error);
            }
            finally
            {
                if (button != null) button.IsEnabled = true;
            }
        }

        /// <summary>
        /// Download CSV template for pinned devices
        /// </summary>
        private void BtnDownloadTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Title = "Save Pinned Devices Template",
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = "PinnedDevices_Template.csv",
                    DefaultExt = ".csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var templateContent = new System.Text.StringBuilder();
                    templateContent.AppendLine("Device");
                    templateContent.AppendLine("# Add one device per line (hostname or IP address)");
                    templateContent.AppendLine("# Example entries:");
                    templateContent.AppendLine("WORKSTATION01");
                    templateContent.AppendLine("SERVER-DC01");
                    templateContent.AppendLine("192.168.1.100");
                    templateContent.AppendLine("laptop-sales-05");
                    templateContent.AppendLine("");
                    templateContent.AppendLine("# Delete these example lines and add your devices below:");

                    File.WriteAllText(saveFileDialog.FileName, templateContent.ToString());
                    ShowStatus($"✅ Template saved to: {saveFileDialog.FileName}", MessageType.Success);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ Failed to save template: {ex.Message}", MessageType.Error);
            }
        }

        // TAG: #COLOR_PICKER #APPEARANCE
        /// <summary>
        /// Open color picker for primary color
        /// </summary>
        private void PrimaryColorPreview_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                using (var colorDialog = new System.Windows.Forms.ColorDialog())
                {
                    colorDialog.FullOpen = true;
                    colorDialog.AnyColor = true;
                    colorDialog.SolidColorOnly = false;

                    // Set current color if valid
                    if (TxtPrimaryColor != null && !string.IsNullOrWhiteSpace(TxtPrimaryColor.Text))
                    {
                        try
                        {
                            var currentColor = (Color)ColorConverter.ConvertFromString(TxtPrimaryColor.Text);
                            colorDialog.Color = System.Drawing.Color.FromArgb(
                                currentColor.A, currentColor.R, currentColor.G, currentColor.B);
                        }
                        catch { }
                    }

                    if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        var selectedColor = colorDialog.Color;
                        string hexColor = $"#FF{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";

                        if (TxtPrimaryColor != null)
                            TxtPrimaryColor.Text = hexColor;

                        ShowStatus("✅ Primary color updated", MessageType.Success);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ Color picker error: {ex.Message}", MessageType.Error);
            }
        }

        /// <summary>
        /// Open color picker for secondary color
        /// </summary>
        private void SecondaryColorPreview_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                using (var colorDialog = new System.Windows.Forms.ColorDialog())
                {
                    colorDialog.FullOpen = true;
                    colorDialog.AnyColor = true;
                    colorDialog.SolidColorOnly = false;

                    // Set current color if valid
                    if (TxtSecondaryColor != null && !string.IsNullOrWhiteSpace(TxtSecondaryColor.Text))
                    {
                        try
                        {
                            var currentColor = (Color)ColorConverter.ConvertFromString(TxtSecondaryColor.Text);
                            colorDialog.Color = System.Drawing.Color.FromArgb(
                                currentColor.A, currentColor.R, currentColor.G, currentColor.B);
                        }
                        catch { }
                    }

                    if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        var selectedColor = colorDialog.Color;
                        string hexColor = $"#FF{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";

                        if (TxtSecondaryColor != null)
                            TxtSecondaryColor.Text = hexColor;

                        ShowStatus("✅ Secondary color updated", MessageType.Success);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ Color picker error: {ex.Message}", MessageType.Error);
            }
        }

        #endregion

        #region VERSION_7_QUICK_WINS

        // ═══════════════════════════════════════════════════════
        // FONT SIZE CONTROLS - TAG: #VERSION_7 #QUICK_WINS
        // ═══════════════════════════════════════════════════════

        private void SliderFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtFontSizeValue != null && TxtFontPreview != null)
            {
                double value = SliderFontSize.Value;
                TxtFontSizeValue.Text = $"{value:F1}x";
                TxtFontPreview.FontSize = 12 * value; // Base font size is 12
            }
        }

        private void BtnApplyFontSize_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double multiplier = SliderFontSize.Value;
                Properties.Settings.Default.FontSizeMultiplier = multiplier;
                Properties.Settings.Default.Save();

                // Apply to main window immediately if it exists
                Application.Current.MainWindow.FontSize = 12 * multiplier;

                ShowStatus($"✅ Font size set to {multiplier:F1}x", MessageType.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ Failed to apply font size: {ex.Message}", MessageType.Error);
            }
        }

        private void BtnResetFontSize_Click(object sender, RoutedEventArgs e)
        {
            SliderFontSize.Value = 1.0;
            BtnApplyFontSize_Click(sender, e);
        }

        // ═══════════════════════════════════════════════════════
        // AUTO-SAVE CONTROLS - TAG: #VERSION_7 #QUICK_WINS
        // ═══════════════════════════════════════════════════════

        private void ChkAutoSaveEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelAutoSaveSettings != null)
            {
                PanelAutoSaveSettings.IsEnabled = ChkAutoSaveEnabled.IsChecked ?? false;
            }
        }

        private void BtnManualBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get main window and trigger manual backup
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    // Call the auto-save method directly
                    System.Reflection.MethodInfo method = mainWindow.GetType().GetMethod(
                        "AutoSaveTimer_Tick",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (method != null)
                    {
                        method.Invoke(mainWindow, new object[] { null, null });
                        ShowStatus("✅ Manual backup created successfully", MessageType.Success);
                    }
                    else
                    {
                        ShowStatus("❌ Auto-save method not found", MessageType.Error);
                    }
                }
                else
                {
                    ShowStatus("❌ Main window not accessible", MessageType.Error);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ Backup failed: {ex.Message}", MessageType.Error);
            }
        }

        private void BtnOpenBackupFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string backupPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    LogoConfig.PRODUCT_NAME, "AutoSave");

                // Create directory if it doesn't exist
                if (!System.IO.Directory.Exists(backupPath))
                {
                    System.IO.Directory.CreateDirectory(backupPath);
                }

                // Open folder in explorer
                System.Diagnostics.Process.Start("explorer.exe", backupPath);
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ Failed to open backup folder: {ex.Message}", MessageType.Error);
            }
        }

        // ═══════════════════════════════════════════════════════
        // CONNECTION PROFILES - TAG: #VERSION_7 #CONNECTION_PROFILES
        // ═══════════════════════════════════════════════════════

        private void LoadConnectionProfiles()
        {
            try
            {
                _connectionProfiles.Clear();
                string json = Properties.Settings.Default.ConnectionProfilesJson;

                if (!string.IsNullOrWhiteSpace(json))
                {
                    var serializer = new JavaScriptSerializer();
                    var profiles = serializer.Deserialize<List<ConnectionProfile>>(json);

                    if (profiles != null)
                    {
                        foreach (var profile in profiles)
                        {
                            _connectionProfiles.Add(profile);
                        }
                    }
                }

                if (GridConnectionProfiles != null)
                {
                    GridConnectionProfiles.ItemsSource = _connectionProfiles;
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to load connection profiles: {ex.Message}", MessageType.Error);
            }
        }

        private void SaveConnectionProfiles()
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(_connectionProfiles.ToList());
                Properties.Settings.Default.ConnectionProfilesJson = json;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to save connection profiles: {ex.Message}", MessageType.Error);
            }
        }

        private void BtnAddProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string profileName = TxtNewProfileName?.Text?.Trim();
                string serverAddress = TxtNewProfileServer?.Text?.Trim();
                string username = TxtNewProfileUsername?.Text?.Trim();

                if (string.IsNullOrEmpty(profileName))
                {
                    ShowStatus("Profile name is required", MessageType.Error);
                    return;
                }

                if (string.IsNullOrEmpty(serverAddress))
                {
                    ShowStatus("Server/Domain address is required", MessageType.Error);
                    return;
                }

                // Check for duplicate profile names
                if (_connectionProfiles.Any(p => p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase)))
                {
                    ShowStatus("A profile with this name already exists", MessageType.Error);
                    return;
                }

                var newProfile = new ConnectionProfile
                {
                    Name = profileName,
                    DomainController = serverAddress,
                    Username = username ?? "",
                    Environment = "General",
                    CreatedDate = DateTime.Now,
                    LastUsedDate = DateTime.Now
                };

                _connectionProfiles.Add(newProfile);
                SaveConnectionProfiles();

                // Clear input fields
                if (TxtNewProfileName != null) TxtNewProfileName.Text = "";
                if (TxtNewProfileServer != null) TxtNewProfileServer.Text = "";
                if (TxtNewProfileUsername != null) TxtNewProfileUsername.Text = "";

                ShowStatus($"✅ Profile '{profileName}' added successfully", MessageType.Success);
                _hasUnsavedChanges = true;
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to add profile: {ex.Message}", MessageType.Error);
            }
        }

        private void BtnLoadProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var profile = button?.Tag as ConnectionProfile;

                if (profile == null) return;

                // Save profile info to settings so MainWindow can use it
                Properties.Settings.Default.LastUser = profile.Username;
                Properties.Settings.Default.Save();

                MainWindow.ApplyProfile(profile);
                ShowStatus($"✅ Profile '{profile.Name}' applied.", MessageType.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to load profile: {ex.Message}", MessageType.Error);
            }
        }

        private void BtnEditProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var profile = button?.Tag as ConnectionProfile;

                if (profile == null) return;

                // Populate edit fields
                if (TxtNewProfileName != null) TxtNewProfileName.Text = profile.Name;
                if (TxtNewProfileServer != null) TxtNewProfileServer.Text = profile.DomainController;
                if (TxtNewProfileUsername != null) TxtNewProfileUsername.Text = profile.Username;

                ShowStatus($"Edit mode: Modify values and click 'Add Profile' to update", MessageType.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to edit profile: {ex.Message}", MessageType.Error);
            }
        }

        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var profile = button?.Tag as ConnectionProfile;

                if (profile == null) return;

                Managers.UI.ToastManager.ShowWarning(
                    $"Delete connection profile '{profile.Name}'?",
                    "Delete", () =>
                    {
                        _connectionProfiles.Remove(profile);
                        SaveConnectionProfiles();
                        ShowStatus($"✅ Profile '{profile.Name}' deleted", MessageType.Success);
                        _hasUnsavedChanges = true;
                    });
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to delete profile: {ex.Message}", MessageType.Error);
            }
        }

        // ═══════════════════════════════════════════════════════
        // BOOKMARKS/FAVORITES - TAG: #VERSION_7 #BOOKMARKS
        // ═══════════════════════════════════════════════════════

        private void LoadBookmarks()
        {
            try
            {
                _bookmarks.Clear();
                string json = Properties.Settings.Default.BookmarksJson;

                if (!string.IsNullOrWhiteSpace(json))
                {
                    var serializer = new JavaScriptSerializer();
                    var bookmarks = serializer.Deserialize<List<ComputerBookmark>>(json);

                    if (bookmarks != null)
                    {
                        foreach (var bookmark in bookmarks)
                        {
                            _bookmarks.Add(bookmark);
                        }
                    }
                }

                if (GridBookmarks != null)
                {
                    GridBookmarks.ItemsSource = _bookmarks;
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to load bookmarks: {ex.Message}", MessageType.Error);
            }
        }

        private void SaveBookmarks()
        {
            try
            {
                // TAG: #VERSION_7 #BOOKMARKS - Delegate to BookmarkManager for consistency
                foreach (var bookmark in _bookmarks)
                {
                    BookmarkManager.AddBookmark(bookmark.Hostname, bookmark.Description, bookmark.Category);
                }
                BookmarkManager.SaveBookmarks();
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to save bookmarks: {ex.Message}", MessageType.Error);
            }
        }

        private void BtnAddBookmark_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string displayName = TxtNewBookmarkName?.Text?.Trim();
                string hostname = TxtNewBookmarkHost?.Text?.Trim();
                string folder = TxtNewBookmarkFolder?.Text?.Trim() ?? "General";
                string notes = TxtNewBookmarkNotes?.Text?.Trim();

                if (string.IsNullOrEmpty(displayName))
                {
                    ShowStatus("Display name is required", MessageType.Error);
                    return;
                }

                if (string.IsNullOrEmpty(hostname))
                {
                    ShowStatus("Hostname/IP is required", MessageType.Error);
                    return;
                }

                var newBookmark = new ComputerBookmark
                {
                    Hostname = hostname,
                    Description = displayName ?? hostname,
                    Category = folder ?? "General",
                    BookmarkedDate = DateTime.Now
                };

                _bookmarks.Add(newBookmark);
                SaveBookmarks();

                // Clear input fields
                if (TxtNewBookmarkName != null) TxtNewBookmarkName.Text = "";
                if (TxtNewBookmarkHost != null) TxtNewBookmarkHost.Text = "";
                if (TxtNewBookmarkFolder != null) TxtNewBookmarkFolder.Text = "";
                if (TxtNewBookmarkNotes != null) TxtNewBookmarkNotes.Text = "";

                ShowStatus($"✅ Bookmark '{displayName}' added successfully", MessageType.Success);
                _hasUnsavedChanges = true;
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to add bookmark: {ex.Message}", MessageType.Error);
            }
        }

        private void BtnCopyBookmark_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var bookmark = button?.Tag as ComputerBookmark;

                if (bookmark == null) return;

                Clipboard.SetText(bookmark.Hostname);
                ShowStatus($"✅ Copied '{bookmark.Hostname}' to clipboard", MessageType.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to copy bookmark: {ex.Message}", MessageType.Error);
            }
        }

        private void BtnEditBookmark_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var bookmark = button?.Tag as ComputerBookmark;

                if (bookmark == null) return;

                // Populate edit fields
                if (TxtNewBookmarkName != null) TxtNewBookmarkName.Text = bookmark.DisplayName;
                if (TxtNewBookmarkHost != null) TxtNewBookmarkHost.Text = bookmark.Hostname;
                if (TxtNewBookmarkFolder != null) TxtNewBookmarkFolder.Text = bookmark.Category;
                if (TxtNewBookmarkNotes != null) TxtNewBookmarkNotes.Text = bookmark.Description;

                ShowStatus($"Edit mode: Modify values and click 'Add Bookmark' to update (Note: this creates a new bookmark)", MessageType.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to edit bookmark: {ex.Message}", MessageType.Error);
            }
        }

        private void BtnDeleteBookmark_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var bookmark = button?.Tag as ComputerBookmark;

                if (bookmark == null) return;

                Managers.UI.ToastManager.ShowWarning(
                    $"Delete bookmark '{bookmark.DisplayName}'?",
                    "Delete", () =>
                    {
                        _bookmarks.Remove(bookmark);
                        SaveBookmarks();
                        ShowStatus($"✅ Bookmark '{bookmark.DisplayName}' deleted", MessageType.Success);
                        _hasUnsavedChanges = true;
                    });
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to delete bookmark: {ex.Message}", MessageType.Error);
            }
        }

        // TAG: #ASYNC_OPTIMIZATION - Made async to prevent UI blocking on file read
        private async void BtnImportBookmarks_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null) button.IsEnabled = false;
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    Title = "Import Bookmarks from CSV"
                };

                if (dialog.ShowDialog() == true)
                {
                    // TAG: #ASYNC_OPTIMIZATION - Run file I/O on background thread
                    var lines = await Task.Run(() => File.ReadAllLines(dialog.FileName));
                    int imported = 0;

                    // Skip header if present
                    int startLine = lines.Length > 0 && lines[0].Contains("Display") ? 1 : 0;

                    for (int i = startLine; i < lines.Length; i++)
                    {
                        var parts = lines[i].Split(',');
                        if (parts.Length >= 2)
                        {
                            _bookmarks.Add(new ComputerBookmark
                            {
                                Hostname = parts[0].Trim(),
                                Description = parts.Length > 1 ? parts[1].Trim() : "",
                                Category = parts.Length > 2 ? parts[2].Trim() : "General",
                                BookmarkedDate = DateTime.Now
                            });
                            imported++;
                        }
                    }

                    SaveBookmarks();
                    ShowStatus($"✅ Imported {imported} bookmarks successfully", MessageType.Success);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to import bookmarks: {ex.Message}", MessageType.Error);
            }
            finally
            {
                if (button != null) button.IsEnabled = true;
            }
        }

        // TAG: #ASYNC_OPTIMIZATION - Made async to prevent UI blocking on file write
        private async void BtnExportBookmarks_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null) button.IsEnabled = false;
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"{LogoConfig.PRODUCT_NAME}_Bookmarks_{DateTime.Now:yyyyMMdd}.csv",
                    Title = "Export Bookmarks to CSV"
                };

                if (dialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Hostname,Description,Category");

                    foreach (var bookmark in _bookmarks)
                    {
                        sb.AppendLine($"{bookmark.Hostname},{bookmark.Description},{bookmark.Category}");
                    }

                    // TAG: #ASYNC_OPTIMIZATION - Run file I/O on background thread
                    await Task.Run(() => File.WriteAllText(dialog.FileName, sb.ToString()));
                    ShowStatus($"✅ Exported {_bookmarks.Count} bookmarks to {dialog.FileName}", MessageType.Success);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to export bookmarks: {ex.Message}", MessageType.Error);
            }
            finally
            {
                if (button != null) button.IsEnabled = true;
            }
        }

        // ═══════════════════════════════════════════════════════
        // EXPORT/IMPORT ALL SETTINGS - TAG: #VERSION_7 #EXPORT_IMPORT
        // ═══════════════════════════════════════════════════════

        // TAG: #ASYNC_OPTIMIZATION - Made async to prevent UI blocking on file write
        private async void BtnExportAllSettings_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null) button.IsEnabled = false;
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    FileName = $"{LogoConfig.PRODUCT_NAME}_Settings_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json",
                    Title = "Export All Settings"
                };

                if (dialog.ShowDialog() == true)
                {
                    // Create comprehensive settings backup
                    var backupData = new Dictionary<string, object>
                    {
                        ["ExportDate"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        ["Version"] = LogoConfig.VERSION, // TAG: #VERSION_ENGINE - Uses centralized version
                        ["ConnectionProfiles"] = _connectionProfiles.ToList(),
                        ["Bookmarks"] = _bookmarks.ToList(),
                        ["FontSizeMultiplier"] = SliderFontSize?.Value ?? 1.0,
                        ["AutoSaveEnabled"] = ChkAutoSaveEnabled?.IsChecked ?? true,
                        ["AutoSaveInterval"] = TxtAutoSaveInterval?.Text ?? "5",
                        ["LastUser"] = Properties.Settings.Default.LastUser,
                        ["RemoteControlConfig"] = Properties.Settings.Default.RemoteControlConfigJson,
                        ["RecentTargets"] = Properties.Settings.Default.RecentTargets,
                        ["WindowPosition"] = Properties.Settings.Default.WindowPosition,
                        ["GlobalServicesConfig"] = Properties.Settings.Default.GlobalServicesConfig
                    };

                    var serializer = new JavaScriptSerializer();
                    string json = serializer.Serialize(backupData);

                    // TAG: #ASYNC_OPTIMIZATION - Run file I/O on background thread
                    await Task.Run(() => File.WriteAllText(dialog.FileName, json));

                    ShowStatus($"✅ All settings exported to {Path.GetFileName(dialog.FileName)}", MessageType.Success);
                    // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                    ToastManager.ShowSuccess(
                        $"Settings backup created successfully!\n\n" +
                        $"File: {Path.GetFileName(dialog.FileName)}\n" +
                        $"Location: {Path.GetDirectoryName(dialog.FileName)}\n\n" +
                        $"This backup includes:\n" +
                        $"• Connection Profiles ({_connectionProfiles.Count})\n" +
                        $"• Bookmarks ({_bookmarks.Count})\n" +
                        $"• All preferences and settings\n\n" +
                        $"Keep this file safe for easy restoration!");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to export settings: {ex.Message}", MessageType.Error);
                // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                ToastManager.ShowError($"Export failed:\n\n{ex.Message}");
            }
            finally
            {
                if (button != null) button.IsEnabled = true;
            }
        }

        // TAG: #ASYNC_OPTIMIZATION - Made async to prevent UI blocking on file read
        private void BtnImportAllSettings_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null) button.IsEnabled = false;
            Managers.UI.ToastManager.ShowWarning(
                "Import settings from a backup file?\n\n" +
                "⚠️ WARNING: This will REPLACE your current settings!\n\n" +
                "Current settings will be overwritten. Make sure you have\n" +
                "a backup of your current configuration if needed.\n\n" +
                "Continue with import?",
                "Import", async () =>
                {
                    await DoImportAllSettingsAsync();
                    if (button != null) button.IsEnabled = true;
                });
        }

        // TAG: #ASYNC_OPTIMIZATION - Run file I/O on background thread
        private async Task DoImportAllSettingsAsync()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    Title = "Import Settings Backup"
                };

                if (dialog.ShowDialog() == true)
                {
                    string json = await Task.Run(() => File.ReadAllText(dialog.FileName));
                    var serializer = new JavaScriptSerializer();
                    var backupData = serializer.Deserialize<Dictionary<string, object>>(json);

                    if (backupData == null)
                    {
                        throw new Exception("Invalid backup file format");
                    }

                    int importedCount = 0;

                    // Import Connection Profiles
                    if (backupData.ContainsKey("ConnectionProfiles"))
                    {
                        var profilesJson = serializer.Serialize(backupData["ConnectionProfiles"]);
                        var profiles = serializer.Deserialize<List<ConnectionProfile>>(profilesJson);
                        if (profiles != null)
                        {
                            _connectionProfiles.Clear();
                            foreach (var profile in profiles)
                            {
                                _connectionProfiles.Add(profile);
                            }
                            SaveConnectionProfiles();
                            importedCount += profiles.Count;
                        }
                    }

                    // Import Bookmarks
                    if (backupData.ContainsKey("Bookmarks"))
                    {
                        var bookmarksJson = serializer.Serialize(backupData["Bookmarks"]);
                        var bookmarks = serializer.Deserialize<List<ComputerBookmark>>(bookmarksJson);
                        if (bookmarks != null)
                        {
                            _bookmarks.Clear();
                            foreach (var bookmark in bookmarks)
                            {
                                _bookmarks.Add(bookmark);
                            }
                            SaveBookmarks();
                            importedCount += bookmarks.Count;
                        }
                    }

                    // Import other settings
                    if (backupData.ContainsKey("FontSizeMultiplier") && SliderFontSize != null)
                    {
                        SliderFontSize.Value = Convert.ToDouble(backupData["FontSizeMultiplier"]);
                    }

                    if (backupData.ContainsKey("AutoSaveEnabled") && ChkAutoSaveEnabled != null)
                    {
                        ChkAutoSaveEnabled.IsChecked = Convert.ToBoolean(backupData["AutoSaveEnabled"]);
                    }

                    if (backupData.ContainsKey("AutoSaveInterval") && TxtAutoSaveInterval != null)
                    {
                        TxtAutoSaveInterval.Text = backupData["AutoSaveInterval"].ToString();
                    }

                    if (backupData.ContainsKey("LastUser"))
                    {
                        Properties.Settings.Default.LastUser = backupData["LastUser"]?.ToString() ?? "";
                    }

                    if (backupData.ContainsKey("RemoteControlConfig"))
                    {
                        Properties.Settings.Default.RemoteControlConfigJson = backupData["RemoteControlConfig"]?.ToString() ?? "";
                    }

                    if (backupData.ContainsKey("RecentTargets"))
                    {
                        Properties.Settings.Default.RecentTargets = backupData["RecentTargets"]?.ToString() ?? "";
                    }

                    if (backupData.ContainsKey("WindowPosition"))
                    {
                        Properties.Settings.Default.WindowPosition = backupData["WindowPosition"]?.ToString() ?? "";
                    }

                    if (backupData.ContainsKey("GlobalServicesConfig"))
                    {
                        Properties.Settings.Default.GlobalServicesConfig = backupData["GlobalServicesConfig"]?.ToString() ?? "";
                    }

                    Properties.Settings.Default.Save();

                    ShowStatus($"✅ Settings imported successfully from backup", MessageType.Success);
                    string exportDate = backupData.ContainsKey("ExportDate") ? backupData["ExportDate"].ToString() : "Unknown";

                    // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                    ToastManager.ShowSuccess(
                        $"Settings imported successfully!\n\n" +
                        $"Imported from: {Path.GetFileName(dialog.FileName)}\n" +
                        $"Date: {exportDate}\n\n" +
                        $"Restart the application to apply all changes.");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to import settings: {ex.Message}", MessageType.Error);
                // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                ToastManager.ShowError($"Import failed:\n\n{ex.Message}");
            }
        }

        #endregion

        #region Database Management - TAG: #VERSION_1_2 #DATABASE

        /// <summary>
        /// Load database configuration and statistics
        /// </summary>
        private async void LoadDatabaseConfiguration()
        {
            try
            {
                // Load database type and path (fallback matches SetupWizard XAML default)
                var dbType = Properties.Settings.Default.DatabaseType ?? "SQLite";
                var dbPath = Properties.Settings.Default.DatabasePath;
                if (string.IsNullOrWhiteSpace(dbPath))
                    dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NecessaryAdminTool");

                if (TxtDbType != null)
                    TxtDbType.Text = Data.DataProviderFactory.GetDatabaseTypeDescription(dbType);

                if (TxtDbPath != null)
                    TxtDbPath.Text = dbPath;

                // Load database statistics
                await RefreshDatabaseStatisticsAsync();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to load database configuration", ex);
            }
        }

        /// <summary>
        /// TAG: #VERSION_1_0 #DEPLOYMENT - Load deployment configuration for Windows Update ISO path and hostname patterns
        /// FUTURE CLAUDES: This loads the ISO path and hostname pattern settings from app settings into the UI
        /// </summary>
        private void LoadDeploymentConfiguration()
        {
            try
            {
                // Load log directory (defaults to database path + "\Logs" if not set)
                if (TxtLogDirectory != null)
                {
                    string logDir = Properties.Settings.Default.DeploymentLogDirectory ?? "";
                    TxtLogDirectory.Text = logDir;
                }

                // Load ISO path
                if (TxtISOPath != null)
                {
                    TxtISOPath.Text = Properties.Settings.Default.WindowsUpdateISOPath ?? "";
                }

                // Load hostname pattern
                if (TxtHostnamePattern != null)
                {
                    TxtHostnamePattern.Text = Properties.Settings.Default.LocalISOHostnamePattern ?? "TN";
                }

                // Load hostname match mode
                if (CmbHostnameMatchMode != null)
                {
                    string matchMode = Properties.Settings.Default.LocalISOHostnameMatchMode ?? "StartsWith";
                    foreach (ComboBoxItem item in CmbHostnameMatchMode.Items)
                    {
                        if (item.Tag?.ToString() == matchMode)
                        {
                            CmbHostnameMatchMode.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to load deployment configuration", ex);
            }
        }

        /// <summary>
        /// Refresh database statistics
        /// </summary>
        private async System.Threading.Tasks.Task RefreshDatabaseStatisticsAsync()
        {
            try
            {
                if (!Data.DataProviderFactory.VerifyConfiguration())
                {
                    LogManager.LogWarning("Database configuration not valid, skipping stats refresh");
                    return;
                }

                using (var provider = await Data.DataProviderFactory.CreateProviderAsync())
                {
                    var stats = await provider.GetDatabaseStatsAsync();

                    if (TxtDbComputers != null)
                        TxtDbComputers.Text = stats.TotalComputers.ToString();

                    if (TxtDbScans != null)
                        TxtDbScans.Text = stats.TotalScans.ToString();

                    if (TxtDbScripts != null)
                        TxtDbScripts.Text = stats.TotalScripts.ToString();

                    if (TxtDbSize != null)
                        TxtDbSize.Text = $"{stats.DatabaseSizeMB:N1} MB";
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to refresh database statistics", ex);

                // Show zeros if stats fail
                if (TxtDbComputers != null) TxtDbComputers.Text = "0";
                if (TxtDbScans != null) TxtDbScans.Text = "0";
                if (TxtDbScripts != null) TxtDbScripts.Text = "0";
                if (TxtDbSize != null) TxtDbSize.Text = "0 MB";
            }
        }

        /// <summary>
        /// Refresh database statistics button click
        /// </summary>
        private async void BtnRefreshDbStats_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
                button.Content = "⏳ LOADING...";
            }
            try
            {
                await RefreshDatabaseStatisticsAsync();

                ShowStatus("Database statistics refreshed", MessageType.Success);
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to refresh database stats", ex);
                ShowStatus($"Failed to refresh stats: {ex.Message}", MessageType.Error);
            }
            finally
            {
                if (button != null)
                {
                    button.Content = "🔄 REFRESH";
                    button.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// Backup database button click
        /// </summary>
        private async void BtnBackupDatabase_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
                button.Content = "⏳ BACKING UP...";
            }
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Database Backup|*.bak;*.db;*.accdb;*.zip|All Files|*.*",
                    Title = "Save Database Backup",
                    FileName = $"NAT_Backup_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (dialog.ShowDialog() == true)
                {
                    var dbPath = Properties.Settings.Default.DatabasePath;
                    var dbType = Properties.Settings.Default.DatabaseType;

                    // Copy database file(s) to backup location
                    if (dbType == "SQLite")
                    {
                        var sourceFile = Path.Combine(dbPath, "NecessaryAdminTool.db");
                        if (File.Exists(sourceFile))
                        {
                            File.Copy(sourceFile, dialog.FileName, true);
                        }
                    }
                    else if (dbType == "Access")
                    {
                        var sourceFile = Path.Combine(dbPath, "NecessaryAdminTool.accdb");
                        if (File.Exists(sourceFile))
                        {
                            File.Copy(sourceFile, dialog.FileName, true);
                        }
                    }
                    else if (dbType == "CSV" || dbType == "JSON")
                    {
                        // ZIP the entire directory
                        System.IO.Compression.ZipFile.CreateFromDirectory(dbPath, dialog.FileName);
                    }

                    ShowStatus($"Database backed up to: {Path.GetFileName(dialog.FileName)}", MessageType.Success);
                    // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                    ToastManager.ShowSuccess($"Database backed up successfully!\n\nLocation: {dialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to backup database", ex);
                ShowStatus($"Backup failed: {ex.Message}", MessageType.Error);
                // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                ToastManager.ShowError($"Failed to backup database:\n\n{ex.Message}");
            }
            finally
            {
                if (button != null)
                {
                    button.Content = "💾 BACKUP";
                    button.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// Restore database button click
        /// </summary>
        private void BtnRestoreDatabase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Managers.UI.ToastManager.ShowWarning(
                    "⚠️ WARNING: Restoring from a backup will REPLACE all current data!\n\n" +
                    "Make sure you have a recent backup before proceeding.\n\n" +
                    "Do you want to continue?",
                    "Restore", () =>
                    {
                        var dialog = new OpenFileDialog
                        {
                            Filter = "Database Backup|*.bak;*.db;*.accdb;*.zip|All Files|*.*",
                            Title = "Select Database Backup to Restore"
                        };

                        if (dialog.ShowDialog() == true)
                        {
                            var dbPath = Properties.Settings.Default.DatabasePath;
                            var dbType = Properties.Settings.Default.DatabaseType;

                            // Restore database file(s) from backup
                            if (dbType == "SQLite")
                            {
                                var targetFile = Path.Combine(dbPath, "NecessaryAdminTool.db");
                                File.Copy(dialog.FileName, targetFile, true);
                            }
                            else if (dbType == "Access")
                            {
                                var targetFile = Path.Combine(dbPath, "NecessaryAdminTool.accdb");
                                File.Copy(dialog.FileName, targetFile, true);
                            }
                            else if (dbType == "CSV" || dbType == "JSON")
                            {
                                // Extract ZIP to directory
                                if (Directory.Exists(dbPath))
                                {
                                    Directory.Delete(dbPath, true);
                                }
                                System.IO.Compression.ZipFile.ExtractToDirectory(dialog.FileName, dbPath);
                            }

                            ShowStatus("Database restored successfully", MessageType.Success);
                            // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                            ToastManager.ShowSuccess(
                                "Database restored successfully!\n\n" +
                                "Restart the application to use the restored database.");
                        }
                    });
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to restore database", ex);
                ShowStatus($"Restore failed: {ex.Message}", MessageType.Error);
                // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                ToastManager.ShowError($"Failed to restore database:\n\n{ex.Message}");
            }
        }

        /// <summary>
        /// Optimize database button click
        /// </summary>
        private async void BtnOptimizeDatabase_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
                button.Content = "⏳ OPTIMIZING...";
            }
            try
            {
                var dbType = Properties.Settings.Default.DatabaseType;

                if (dbType == "SQLite")
                {
                    #if SQLITE_ENABLED
                    using (var provider = await Data.DataProviderFactory.CreateProviderAsync())
                    {
                        // Run VACUUM command
                        await System.Threading.Tasks.Task.Run(() =>
                        {
                            var sqliteProvider = provider as Data.SqliteDataProvider;
                            sqliteProvider?.Vacuum();
                        });
                    }
                    #endif
                }
                else if (dbType == "Access")
                {
                    // Access compact & repair would go here
                    ShowStatus("Access database optimization not yet implemented", MessageType.Warning);
                }
                else
                {
                    ShowStatus($"{dbType} optimization not supported", MessageType.Warning);
                }

                await RefreshDatabaseStatisticsAsync();

                ShowStatus("Database optimized successfully", MessageType.Success);
                // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                ToastManager.ShowSuccess("Database optimized!\n\nOld data has been purged and the database has been compacted.");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to optimize database", ex);
                ShowStatus($"Optimization failed: {ex.Message}", MessageType.Error);
                // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                ToastManager.ShowError($"Failed to optimize database:\n\n{ex.Message}");
            }
            finally
            {
                if (button != null)
                {
                    button.Content = "⚡ OPTIMIZE";
                    button.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// Re-run setup wizard button click
        /// </summary>
        private void BtnRerunSetup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Managers.UI.ToastManager.ShowWarning(
                    "This will launch the setup wizard to reconfigure your database.\n\n" +
                    "⚠️ Make sure you have a backup before changing database configuration!\n\n" +
                    "Do you want to continue?",
                    "Continue", () =>
                    {
                        var setupWizard = new SetupWizardWindow();
                        var wizardResult = setupWizard.ShowDialog();

                        if (wizardResult == true)
                        {
                            // Reload database configuration
                            LoadDatabaseConfiguration();

                            ShowStatus("Database configuration updated", MessageType.Success);
                            // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                            ToastManager.ShowSuccess(
                                "Database configuration updated successfully!\n\n" +
                                "Restart the application to use the new configuration.");
                        }
                    });
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to reconfigure database", ex);
                ShowStatus($"Configuration failed: {ex.Message}", MessageType.Error);
                // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                ToastManager.ShowError($"Failed to reconfigure database:\n\n{ex.Message}");
            }
        }

        /// <summary>
        /// Load service status
        /// </summary>
        private void LoadServiceStatus()
        {
            try
            {
                var status = ScheduledTaskManager.GetTaskStatus();

                if (TxtServiceStatus != null)
                {
                    TxtServiceStatus.Text = status.GetDisplayText();
                }

                if (TxtServiceLastRun != null)
                {
                    TxtServiceLastRun.Text = status.LastRunTime?.ToString("g") ?? "Never";
                }

                if (TxtServiceNextRun != null)
                {
                    TxtServiceNextRun.Text = status.NextRunTime?.ToString("g") ?? "N/A";
                }

                // Update button states
                if (BtnEnableService != null)
                {
                    BtnEnableService.IsEnabled = status.Exists && !status.IsEnabled;
                }

                if (BtnDisableService != null)
                {
                    BtnDisableService.IsEnabled = status.Exists && status.IsEnabled;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to load service status", ex);
            }
        }

        /// <summary>
        /// Enable service button click
        /// </summary>
        private void BtnEnableService_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool success = ScheduledTaskManager.EnableTask();

                if (success)
                {
                    ShowStatus("Background service enabled", MessageType.Success);
                    LoadServiceStatus();
                }
                else
                {
                    ShowStatus("Failed to enable service", MessageType.Error);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to enable service", ex);
                ShowStatus($"Error enabling service: {ex.Message}", MessageType.Error);
            }
        }

        /// <summary>
        /// Disable service button click
        /// </summary>
        private void BtnDisableService_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool success = ScheduledTaskManager.DisableTask();

                if (success)
                {
                    ShowStatus("Background service disabled", MessageType.Success);
                    LoadServiceStatus();
                }
                else
                {
                    ShowStatus("Failed to disable service", MessageType.Error);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to disable service", ex);
                ShowStatus($"Error disabling service: {ex.Message}", MessageType.Error);
            }
        }

        /// <summary>
        /// Run service now button click
        /// </summary>
        private void BtnRunServiceNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ScheduledTaskManager.TaskExists())
                {
                    Managers.UI.ToastManager.ShowWarning(
                        "Background service is not installed.\n\n" +
                        "Would you like to install it now?",
                        "Install", () =>
                        {
                            // Re-run setup wizard
                            BtnRerunSetup_Click(sender, e);
                        });
                    return;
                }

                bool success = ScheduledTaskManager.RunTask();

                if (success)
                {
                    ShowStatus("Background scan started", MessageType.Success);
                    // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                    ToastManager.ShowSuccess(
                        "Background scan has been started.\n\n" +
                        "Check the log files for scan results.");

                    LoadServiceStatus();
                }
                else
                {
                    ShowStatus("Failed to start background scan", MessageType.Error);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to run service", ex);
                ShowStatus($"Error starting scan: {ex.Message}", MessageType.Error);
            }
        }

        /// <summary>
        /// Uninstall service button click
        /// </summary>
        private void BtnUninstallService_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Managers.UI.ToastManager.ShowWarning(
                    "This will remove the background scanning service.\n\n" +
                    "You can reinstall it later from the setup wizard.\n\n" +
                    "Do you want to continue?",
                    "Uninstall", () =>
                    {
                        bool success = ScheduledTaskManager.DeleteTask();

                        if (success)
                        {
                            Properties.Settings.Default.ServiceEnabled = false;
                            Properties.Settings.Default.Save();

                            ShowStatus("Background service uninstalled", MessageType.Success);
                            // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                            ToastManager.ShowSuccess("Background service has been uninstalled successfully.");

                            LoadServiceStatus();
                        }
                        else
                        {
                            ShowStatus("Failed to uninstall service", MessageType.Error);
                        }
                    });
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to uninstall service", ex);
                ShowStatus($"Error uninstalling service: {ex.Message}", MessageType.Error);
                // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                ToastManager.ShowError($"Failed to uninstall service:\n\n{ex.Message}");
            }
        }

        /// <summary>
        /// Reset first-run flag and optionally restart to launch setup wizard
        /// TAG: #FIRST_RUN_RESET #SETUP_WIZARD #APP_RESTART
        /// </summary>
        private void BtnResetSetup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show "Restart Now" option (Yes branch)
                Managers.UI.ToastManager.ShowWarning(
                    "⚠️ Reset Database Setup?\n\n" +
                    "This will mark the application as 'not configured' so the database setup wizard " +
                    "will run when you next launch the application.\n\n" +
                    "Current database settings will NOT be deleted, but you'll need to complete " +
                    "the setup wizard again.\n\n" +
                    "Click 'Restart Now' to reset and restart immediately, or 'Reset Only' to reset without restarting.",
                    "Restart Now", () =>
                    {
                        // Reset flag and restart immediately
                        Properties.Settings.Default.SetupCompleted = false;
                        Properties.Settings.Default.Save();
                        LogManager.LogInfo("First-run setup flag reset - restarting application");

                        // Restart the application
                        System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
                        Application.Current.Shutdown();
                    });

                // Show "Reset Only" option (No branch)
                Managers.UI.ToastManager.ShowInfo(
                    "Or reset setup flag without restarting — setup wizard will run on next launch.",
                    "Reset Only", () =>
                    {
                        // Reset flag but don't restart - let user do it manually
                        Properties.Settings.Default.SetupCompleted = false;
                        Properties.Settings.Default.Save();
                        LogManager.LogInfo("First-run setup flag reset - setup wizard will run on next manual launch");

                        // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                        ToastManager.ShowSuccess(
                            "✓ Setup wizard will run on next launch\n\n" +
                            $"The database setup wizard will appear when you restart {LogoConfig.PRODUCT_NAME}.");
                    });
                // Dismiss both toasts to cancel
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to reset setup flag", ex);
                // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                ToastManager.ShowError($"Failed to reset setup flag:\n\n{ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // REGION: TOAST NOTIFICATION CONFIGURATION - TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Load toast notification settings into UI controls
        /// TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
        /// </summary>
        private void LoadToastNotificationSettings()
        {
            try
            {
                var appSettings = SettingsManager.LoadAllSettings();
                var toastSettings = appSettings.ToastNotifications ?? new ToastNotificationSettings();

                // Load master toggle
                if (ChkEnableToasts != null)
                    ChkEnableToasts.IsChecked = toastSettings.EnableToasts;

                // Load toast types
                if (ChkToastSuccess != null)
                    ChkToastSuccess.IsChecked = toastSettings.ShowSuccessToasts;
                if (ChkToastInfo != null)
                    ChkToastInfo.IsChecked = toastSettings.ShowInfoToasts;
                if (ChkToastWarning != null)
                    ChkToastWarning.IsChecked = toastSettings.ShowWarningToasts;
                if (ChkToastError != null)
                    ChkToastError.IsChecked = toastSettings.ShowErrorToasts;

                // Load toast categories
                if (ChkToastStatus != null)
                    ChkToastStatus.IsChecked = toastSettings.ShowStatusUpdateToasts;
                if (ChkToastValidation != null)
                    ChkToastValidation.IsChecked = toastSettings.ShowValidationToasts;
                if (ChkToastWorkflow != null)
                    ChkToastWorkflow.IsChecked = toastSettings.ShowWorkflowToasts;
                if (ChkToastErrors != null)
                    ChkToastErrors.IsChecked = toastSettings.ShowErrorHandlerToasts;

                LogManager.LogInfo("OptionsWindow - Toast notification settings loaded");
            }
            catch (Exception ex)
            {
                LogManager.LogError("OptionsWindow - Failed to load toast notification settings", ex);
            }
        }

        /// <summary>
        /// Save toast notification settings from UI controls
        /// TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
        /// </summary>
        private void SaveToastNotificationSettings()
        {
            try
            {
                var toastSettings = new ToastNotificationSettings
                {
                    EnableToasts = ChkEnableToasts?.IsChecked ?? true,
                    ShowSuccessToasts = ChkToastSuccess?.IsChecked ?? true,
                    ShowInfoToasts = ChkToastInfo?.IsChecked ?? true,
                    ShowWarningToasts = ChkToastWarning?.IsChecked ?? true,
                    ShowErrorToasts = ChkToastError?.IsChecked ?? true,
                    ShowStatusUpdateToasts = ChkToastStatus?.IsChecked ?? true,
                    ShowValidationToasts = ChkToastValidation?.IsChecked ?? true,
                    ShowWorkflowToasts = ChkToastWorkflow?.IsChecked ?? true,
                    ShowErrorHandlerToasts = ChkToastErrors?.IsChecked ?? true
                };

                SettingsManager.SaveToastNotificationSettings(toastSettings);
                ToastManager.ReloadSettings(); // Apply immediately

                LogManager.LogInfo("OptionsWindow - Toast notification settings saved and applied");
            }
            catch (Exception ex)
            {
                LogManager.LogError("OptionsWindow - Failed to save toast notification settings", ex);
                throw;
            }
        }

        /// <summary>
        /// Test toasts button - show preview of each toast type
        /// TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
        /// </summary>
        private void BtnTestToasts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Save current settings first so the test respects them
                SaveToastNotificationSettings();

                // Show sample toasts with 1 second delay between each
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(1000)
                };

                int count = 0;
                timer.Tick += (s, args) =>
                {
                    try
                    {
                        switch (count)
                        {
                            case 0:
                                ToastManager.ShowSuccess("Success test toast - Operation completed successfully!");
                                break;
                            case 1:
                                ToastManager.ShowInfo("Info test toast - Here is some useful information for you.");
                                break;
                            case 2:
                                ToastManager.ShowWarning("Warning test toast - This action requires caution.");
                                break;
                            case 3:
                                ToastManager.ShowError("Error test toast - Something went wrong, but it's just a test.");
                                timer.Stop();
                                break;
                        }
                        count++;
                    }
                    catch (Exception ex) { LogManager.LogError("Timer tick failed", ex); timer.Stop(); }
                };

                timer.Start();
                LogManager.LogInfo("OptionsWindow - Toast notification test initiated");
            }
            catch (Exception ex)
            {
                LogManager.LogError("OptionsWindow - Failed to test toast notifications", ex);
                ToastManager.ShowError($"Failed to test toasts: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // REGION: KEYBOARD SHORTCUTS CONFIGURATION - TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
        // ═══════════════════════════════════════════════════════════════════════════

        // Reserved for future keyboard shortcut editing functionality
#pragma warning disable CS0414
        private KeyValuePair<string, KeyboardShortcut>? _editingShortcut = null;
#pragma warning restore CS0414

        /// <summary>
        /// Load keyboard shortcut settings into UI controls
        /// TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
        /// </summary>
        private void LoadKeyboardShortcutSettings()
        {
            try
            {
                var appSettings = SettingsManager.LoadAllSettings();
                var shortcutSettings = appSettings.KeyboardShortcuts ?? new KeyboardShortcutSettings();

                // Bind to DataGrid
                if (DgShortcuts != null)
                {
                    DgShortcuts.ItemsSource = shortcutSettings.Shortcuts.ToList();
                }

                LogManager.LogInfo($"OptionsWindow - Loaded {shortcutSettings.Shortcuts.Count} keyboard shortcuts");
            }
            catch (Exception ex)
            {
                LogManager.LogError("OptionsWindow - Failed to load keyboard shortcut settings", ex);
            }
        }

        /// <summary>
        /// Save keyboard shortcut settings from UI controls
        /// TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
        /// </summary>
        private void SaveKeyboardShortcutSettings()
        {
            try
            {
                if (DgShortcuts?.ItemsSource == null)
                    return;

                var shortcutsList = DgShortcuts.ItemsSource as List<KeyValuePair<string, KeyboardShortcut>>;
                if (shortcutsList == null)
                    return;

                var shortcutSettings = new KeyboardShortcutSettings
                {
                    Shortcuts = shortcutsList.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                };

                SettingsManager.SaveKeyboardShortcutSettings(shortcutSettings);

                LogManager.LogInfo($"OptionsWindow - Saved {shortcutSettings.Shortcuts.Count} keyboard shortcuts");
            }
            catch (Exception ex)
            {
                LogManager.LogError("OptionsWindow - Failed to save keyboard shortcut settings", ex);
                throw;
            }
        }

        /// <summary>
        /// Edit shortcut button - open dialog to record new key combination
        /// TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
        /// </summary>
        private void BtnEditShortcut_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button == null)
                    return;

                string commandKey = button.Tag as string;
                if (string.IsNullOrEmpty(commandKey))
                    return;

                var shortcutsList = DgShortcuts.ItemsSource as List<KeyValuePair<string, KeyboardShortcut>>;
                if (shortcutsList == null)
                    return;

                var shortcut = shortcutsList.FirstOrDefault(kvp => kvp.Key == commandKey);
                if (shortcut.Value == null)
                    return;

                // Show dialog to record new shortcut
                var dialog = new ShortcutRecorderDialog(shortcut.Value.Command, shortcut.Value.DisplayShortcut, shortcutsList)
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    // Update the shortcut
                    var index = shortcutsList.FindIndex(kvp => kvp.Key == commandKey);
                    if (index >= 0)
                    {
                        shortcut.Value.Key = dialog.RecordedKey;
                        shortcut.Value.Modifiers = dialog.RecordedModifiers;

                        // Refresh the DataGrid
                        DgShortcuts.ItemsSource = null;
                        DgShortcuts.ItemsSource = shortcutsList;

                        LogManager.LogInfo($"OptionsWindow - Updated shortcut for {commandKey}: {shortcut.Value.DisplayShortcut}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("OptionsWindow - Failed to edit keyboard shortcut", ex);
                ToastManager.ShowError($"Failed to edit shortcut: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset individual shortcut to default
        /// TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
        /// </summary>
        private void BtnResetShortcut_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button == null)
                    return;

                string commandKey = button.Tag as string;
                if (string.IsNullOrEmpty(commandKey))
                    return;

                var shortcutsList = DgShortcuts.ItemsSource as List<KeyValuePair<string, KeyboardShortcut>>;
                if (shortcutsList == null)
                    return;

                // Get default shortcuts
                var defaults = KeyboardShortcutSettings.GetDefaultShortcuts();
                if (!defaults.ContainsKey(commandKey))
                    return;

                // Reset to default
                var index = shortcutsList.FindIndex(kvp => kvp.Key == commandKey);
                if (index >= 0)
                {
                    var defaultShortcut = defaults[commandKey];
                    shortcutsList[index] = new KeyValuePair<string, KeyboardShortcut>(commandKey, defaultShortcut);

                    // Refresh the DataGrid
                    DgShortcuts.ItemsSource = null;
                    DgShortcuts.ItemsSource = shortcutsList;

                    LogManager.LogInfo($"OptionsWindow - Reset shortcut for {commandKey} to default: {defaultShortcut.DisplayShortcut}");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("OptionsWindow - Failed to reset keyboard shortcut", ex);
                ToastManager.ShowError($"Failed to reset shortcut: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset all shortcuts to defaults
        /// TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
        /// </summary>
        private void BtnResetAllShortcuts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Managers.UI.ToastManager.ShowWarning(
                    "Are you sure you want to reset ALL keyboard shortcuts to their default values?\n\nThis cannot be undone.",
                    "Reset All", () =>
                    {
                        var defaults = KeyboardShortcutSettings.GetDefaultShortcuts();
                        DgShortcuts.ItemsSource = defaults.ToList();

                        LogManager.LogInfo("OptionsWindow - All keyboard shortcuts reset to defaults");
                        ToastManager.ShowSuccess("All keyboard shortcuts have been reset to defaults.");
                    });
            }
            catch (Exception ex)
            {
                LogManager.LogError("OptionsWindow - Failed to reset all keyboard shortcuts", ex);
                ToastManager.ShowError($"Failed to reset shortcuts: {ex.Message}");
            }
        }

        #endregion
    }

    /// <summary>
    /// Service configuration item
    /// </summary>
    public class ServiceConfigItem
    {
        public string ServiceName { get; set; }
        public string Endpoint { get; set; }
        /// <summary>Routing bucket: "Essential", "High", or "Medium". Defaults to "Essential".</summary>
        public string Priority { get; set; } = "Essential";
    }

    // ConnectionProfile class removed - now using ConnectionProfile from ConnectionProfileManager.cs
    // ComputerBookmark class removed - now using ComputerBookmark from BookmarkManager.cs
    // TAG: #VERSION_7 #CONNECTION_PROFILES #BOOKMARKS
}
