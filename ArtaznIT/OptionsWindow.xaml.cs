using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.IO;
using Microsoft.Win32;
using System.Web.Script.Serialization;

namespace ArtaznIT
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
            LoadAllSettings();
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

                // Load target history from UserConfig
                _targetHistory = new List<string>();
                // TODO: Load from UserConfig.TargetHistory

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

                // TAG: #VERSION_7 #CONNECTION_PROFILES - Load connection profiles
                LoadConnectionProfiles();

                // TAG: #VERSION_7 #BOOKMARKS - Load bookmarks
                LoadBookmarks();

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

                Properties.Settings.Default.Save();

                // TODO: Save other settings (target history, etc.)

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
        /// Clear cached username
        /// </summary>
        private void BtnClearLastUser_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Clear the cached username?\n\nYou will need to enter your username again at next login.",
                "Clear Username",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Properties.Settings.Default.LastUser = "";
                Properties.Settings.Default.Save();
                TxtLastUser.Text = "";
                _hasUnsavedChanges = true;
                ShowStatus("Cached username cleared", MessageType.Success);
            }
        }

        /// <summary>
        /// Clear target history
        /// </summary>
        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Clear all recent target history?",
                "Clear History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _targetHistory.Clear();
                ListTargetHistory.Items.Refresh();
                _hasUnsavedChanges = true;
                ShowStatus("Target history cleared", MessageType.Success);
            }
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
            var result = MessageBox.Show(
                $"Remove all {_pinnedDevices.Count} pinned devices?",
                "Clear All Pinned Devices",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _pinnedDevices.Clear();
                SavePinnedDevices();
                ShowStatus("All pinned devices removed", MessageType.Success);
            }
        }

        /// <summary>
        /// Export pinned devices to CSV
        /// </summary>
        private void BtnExportPinned_Click(object sender, RoutedEventArgs e)
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

                    File.WriteAllText(dialog.FileName, csv);
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
                        "ArtaznIT",
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

                    MessageBox.Show(
                        $"Settings reset to factory defaults!\n\n" +
                        $"Backup created at:\n{backupPath}",
                        "Reset Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

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
                var result = MessageBox.Show(
                    "You have unsaved changes. Discard them?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
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
                    StatusMessage.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 133, 51));
                    TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(255, 133, 51));
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
                StatusMessage.Visibility = Visibility.Collapsed;
                timer.Stop();
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
echo  ArtaznIT Suite - Admin Launcher
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
                string shortcutPath = Path.Combine(desktopPath, "ArtaznIT Suite (Admin).lnk");

                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                var shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = batPath;
                shortcut.WorkingDirectory = exeDir;
                shortcut.Description = $"Launch ArtaznIT Suite as {adminUsername}";
                shortcut.IconLocation = exePath + ",0";
                shortcut.Save();

                System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);

                MessageBox.Show(
                    "✅ Admin launcher created successfully!\n\n" +
                    $"Desktop shortcut: \"ArtaznIT Suite (Admin)\"\n" +
                    $"Batch file: {batPath}\n\n" +
                    "📌 FIRST TIME USE:\n" +
                    "1. Double-click the desktop shortcut\n" +
                    "2. Enter your admin password when prompted\n" +
                    "3. Windows will remember your password\n\n" +
                    "📌 FUTURE USE:\n" +
                    "Just double-click the shortcut - no password needed!",
                    "Shortcut Created",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                ShowStatus("Admin launcher created successfully!", MessageType.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to create admin launcher: {ex.Message}", MessageType.Error);
                MessageBox.Show(
                    $"Failed to create admin launcher:\n\n{ex.Message}",
                    "Creation Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
                    // TODO: Load and display logo in preview
                    // TODO: Copy logo to AppData for persistence
                    ShowStatus($"Logo selected: {Path.GetFileName(dialog.FileName)}", MessageType.Success);
                    _hasUnsavedChanges = true;
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error loading logo: {ex.Message}", MessageType.Error);
            }
        }

        /// <summary>
        /// Primary color changed - update preview
        /// </summary>
        private void TxtPrimaryColor_TextChanged(object sender, TextChangedEventArgs e)
        {
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
            var result = MessageBox.Show(
                "Reset all appearance settings to default Artazn branding?\n\n" +
                "• Logo: Artazn \"A\" icon\n" +
                "• Primary Color: Orange (#FFFF8533)\n" +
                "• Secondary Color: Zinc (#FFA1A1AA)\n" +
                "• Font: Segoe UI",
                "Reset Appearance",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                TxtLogoPath.Text = "(Using default Artazn logo)";
                TxtPrimaryColor.Text = "#FFFF8533";
                TxtSecondaryColor.Text = "#FFA1A1AA";
                SliderLogoSize.Value = 64;
                TxtLogoSize.Text = "64";
                CmbFontFamily.SelectedIndex = 0;

                // TODO: Clear custom logo from AppData
                // TODO: Apply default theme immediately

                ShowStatus("Appearance reset to defaults", MessageType.Success);
                _hasUnsavedChanges = true;
            }
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
                timer.Tick += (s, args) => { TxtConfigStatus.Visibility = Visibility.Collapsed; timer.Stop(); };
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
            var result = MessageBox.Show(
                "This will reset your global services configuration to the default settings.\n\n" +
                "All custom services and API endpoints will be lost.\n\n" +
                "Do you want to continue?",
                "Reset to Defaults",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                LoadDefaultServices();
                TxtConfigStatus.Text = "🔄 Configuration reset to defaults";
                TxtConfigStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 133, 51));
                TxtConfigStatus.Visibility = Visibility.Visible;
                ShowStatus("Reset to default services", MessageType.Success);
            }
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
                    client.DefaultRequestHeaders.Add("User-Agent", "ArtaznIT-Monitor/6.0");

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
        /// </summary>
        private void BtnImportPinnedCsv_Click(object sender, RoutedEventArgs e)
        {
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
                    string[] lines = File.ReadAllLines(openFileDialog.FileName);
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
                    "ArtaznIT", "AutoSave");

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

                ShowStatus($"✅ Profile '{profile.Name}' loaded. Restart or re-login to apply.", MessageType.Success);

                // TODO: Trigger mainwindow to apply the profile immediately if possible
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

                var result = MessageBox.Show(
                    $"Delete connection profile '{profile.Name}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _connectionProfiles.Remove(profile);
                    SaveConnectionProfiles();
                    ShowStatus($"✅ Profile '{profile.Name}' deleted", MessageType.Success);
                    _hasUnsavedChanges = true;
                }
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

                var result = MessageBox.Show(
                    $"Delete bookmark '{bookmark.DisplayName}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _bookmarks.Remove(bookmark);
                    SaveBookmarks();
                    ShowStatus($"✅ Bookmark '{bookmark.DisplayName}' deleted", MessageType.Success);
                    _hasUnsavedChanges = true;
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to delete bookmark: {ex.Message}", MessageType.Error);
            }
        }

        private void BtnImportBookmarks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    Title = "Import Bookmarks from CSV"
                };

                if (dialog.ShowDialog() == true)
                {
                    var lines = File.ReadAllLines(dialog.FileName);
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
        }

        private void BtnExportBookmarks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"ArtaznIT_Bookmarks_{DateTime.Now:yyyyMMdd}.csv",
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

                    File.WriteAllText(dialog.FileName, sb.ToString());
                    ShowStatus($"✅ Exported {_bookmarks.Count} bookmarks to {dialog.FileName}", MessageType.Success);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to export bookmarks: {ex.Message}", MessageType.Error);
            }
        }

        // ═══════════════════════════════════════════════════════
        // EXPORT/IMPORT ALL SETTINGS - TAG: #VERSION_7 #EXPORT_IMPORT
        // ═══════════════════════════════════════════════════════

        private void BtnExportAllSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    FileName = $"ArtaznIT_Settings_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json",
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

                    // Pretty print the JSON
                    File.WriteAllText(dialog.FileName, json);

                    ShowStatus($"✅ All settings exported to {Path.GetFileName(dialog.FileName)}", MessageType.Success);
                    MessageBox.Show(
                        $"Settings backup created successfully!\n\n" +
                        $"File: {Path.GetFileName(dialog.FileName)}\n" +
                        $"Location: {Path.GetDirectoryName(dialog.FileName)}\n\n" +
                        $"This backup includes:\n" +
                        $"• Connection Profiles ({_connectionProfiles.Count})\n" +
                        $"• Bookmarks ({_bookmarks.Count})\n" +
                        $"• All preferences and settings\n\n" +
                        $"Keep this file safe for easy restoration!",
                        "Export Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to export settings: {ex.Message}", MessageType.Error);
                MessageBox.Show($"Export failed:\n\n{ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnImportAllSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Import settings from a backup file?\n\n" +
                    "⚠️ WARNING: This will REPLACE your current settings!\n\n" +
                    "Current settings will be overwritten. Make sure you have\n" +
                    "a backup of your current configuration if needed.\n\n" +
                    "Continue with import?",
                    "Confirm Import",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                var dialog = new OpenFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    Title = "Import Settings Backup"
                };

                if (dialog.ShowDialog() == true)
                {
                    string json = File.ReadAllText(dialog.FileName);
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

                    MessageBox.Show(
                        $"Settings imported successfully!\n\n" +
                        $"Imported from: {Path.GetFileName(dialog.FileName)}\n" +
                        $"Date: {exportDate}\n\n" +
                        $"Restart the application to apply all changes.",
                        "Import Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to import settings: {ex.Message}", MessageType.Error);
                MessageBox.Show($"Import failed:\n\n{ex.Message}", "Import Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
    }

    // ConnectionProfile class removed - now using ConnectionProfile from ConnectionProfileManager.cs
    // ComputerBookmark class removed - now using ComputerBookmark from BookmarkManager.cs
    // TAG: #VERSION_7 #CONNECTION_PROFILES #BOOKMARKS
}
