using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Web.Script.Serialization;
using System.IO;

namespace ArtaznIT
{
    // TAG: #REMOTE_CONTROL #RMM_TAB #UI
    /// <summary>
    /// Remote Control tab - centralized RMM tool management
    /// </summary>
    public partial class RemoteControlTab : UserControl
    {
        private ObservableCollection<ToolConfigViewModel> _toolConfigs;
        private ObservableCollection<ConnectionHistoryItem> _connectionHistory;
        public string CurrentTarget { get; set; }

        public RemoteControlTab()
        {
            InitializeComponent();
            DataContext = this;
            Initialize();
        }

        private void Initialize()
        {
            RemoteControlManager.Initialize();
            LoadToolConfigurations();
            LoadConnectionHistory();
            LoadGlobalSettings();
            UpdateQuickLaunchButtons();
        }

        /// <summary>
        /// Load tool configurations into grid
        /// </summary>
        private void LoadToolConfigurations()
        {
            var config = RemoteControlManager.GetConfiguration();
            _toolConfigs = new ObservableCollection<ToolConfigViewModel>();

            foreach (var tool in config.Tools)
            {
                _toolConfigs.Add(new ToolConfigViewModel
                {
                    ToolName = tool.ToolName,
                    Enabled = tool.Enabled,
                    IsConfigured = tool.IsConfigured,
                    ToolType = tool.ToolType,
                    LastTested = tool.LastTested,
                    Config = tool
                });
            }

            GridToolConfig.ItemsSource = _toolConfigs;
        }

        /// <summary>
        /// Update quick launch buttons based on enabled tools
        /// </summary>
        private void UpdateQuickLaunchButtons()
        {
            var enabledTools = _toolConfigs.Where(t => t.Enabled && t.IsConfigured).ToList();
            QuickLaunchButtons.ItemsSource = enabledTools.Select(t => new
            {
                DisplayName = $"🚀 {t.ToolName}",
                ToolType = t.ToolType,
                IsConfigured = t.IsConfigured
            }).ToList();
        }

        /// <summary>
        /// Quick launch button clicked
        /// </summary>
        private void QuickLaunch_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var toolType = (RmmToolType)button.Tag;
            string target = TxtRemoteTarget.Text.Trim();

            if (string.IsNullOrEmpty(target))
            {
                MessageBox.Show("Please enter a target hostname or IP address.",
                    "Target Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LaunchRemoteSession(toolType, target);
        }

        /// <summary>
        /// Launch remote control session
        /// </summary>
        private void LaunchRemoteSession(RmmToolType toolType, string target)
        {
            try
            {
                var config = RemoteControlManager.GetConfiguration();

                // Show confirmation if enabled
                if (config.ShowConfirmationDialog)
                {
                    var result = MessageBox.Show(
                        $"Launch {toolType} remote session to {target}?\n\n" +
                        $"This will initiate a remote control connection.",
                        "Confirm Remote Connection",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                        return;
                }

                // Launch session
                RemoteControlManager.LaunchSession(toolType, target);

                // Add to history
                AddToHistory(toolType, target, true, null);

                // Update last connection display
                TxtLastConnection.Text = $"Last: {toolType} → {target} (just now)";

                MessageBox.Show($"Remote session launched: {toolType} → {target}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddToHistory(toolType, target, false, ex.Message);
                MessageBox.Show($"Failed to launch remote session:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Tool enabled checkbox changed
        /// </summary>
        private void ToolEnabled_Changed(object sender, RoutedEventArgs e)
        {
            SaveConfiguration();
            UpdateQuickLaunchButtons();
        }

        /// <summary>
        /// Configure button clicked
        /// </summary>
        private void Configure_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var toolVM = button.Tag as ToolConfigViewModel;
            if (toolVM == null) return;

            // Open configuration dialog
            var configWindow = new ToolConfigWindow(toolVM.Config);
            if (configWindow.ShowDialog() == true)
            {
                toolVM.IsConfigured = toolVM.Config.IsConfigured;
                toolVM.UpdateStatus();
                SaveConfiguration();
                UpdateQuickLaunchButtons();
            }
        }

        /// <summary>
        /// Test button clicked
        /// </summary>
        private void Test_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var toolVM = button.Tag as ToolConfigViewModel;
            if (toolVM == null) return;

            button.IsEnabled = false;
            button.Content = "Testing...";

            try
            {
                bool success = RemoteControlManager.TestConnection(toolVM.ToolType);

                toolVM.LastTested = DateTime.Now;
                toolVM.Config.LastTested = DateTime.Now;

                if (success)
                {
                    toolVM.StatusDisplay = "✅ Test Passed";
                    toolVM.StatusColor = "Green";
                    MessageBox.Show($"{toolVM.ToolName} connection test successful!",
                        "Test Passed", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    toolVM.StatusDisplay = "❌ Test Failed";
                    toolVM.StatusColor = "Red";
                    MessageBox.Show($"{toolVM.ToolName} connection test failed.\n\nCheck configuration and try again.",
                        "Test Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                SaveConfiguration();
            }
            finally
            {
                button.IsEnabled = true;
                button.Content = "🧪 TEST";
            }
        }

        /// <summary>
        /// Save configuration
        /// </summary>
        private void SaveConfiguration()
        {
            var config = RemoteControlManager.GetConfiguration();

            // Update from view models
            foreach (var toolVM in _toolConfigs)
            {
                var toolConfig = config.Tools.FirstOrDefault(t => t.ToolType == toolVM.ToolType);
                if (toolConfig != null)
                {
                    toolConfig.Enabled = toolVM.Enabled;
                    toolConfig.IsConfigured = toolVM.IsConfigured;
                    toolConfig.LastTested = toolVM.LastTested;
                }
            }

            // Update global settings
            if (int.TryParse(TxtTimeout.Text, out int timeout))
                config.ConnectionTimeoutSeconds = timeout;

            if (int.TryParse(TxtRetries.Text, out int retries))
                config.RetryAttempts = retries;

            config.ShowConfirmationDialog = ChkShowConfirmation.IsChecked ?? true;

            RemoteControlManager.SaveConfiguration(config);
        }

        /// <summary>
        /// Load global settings
        /// </summary>
        private void LoadGlobalSettings()
        {
            var config = RemoteControlManager.GetConfiguration();
            TxtTimeout.Text = config.ConnectionTimeoutSeconds.ToString();
            TxtRetries.Text = config.RetryAttempts.ToString();
            ChkShowConfirmation.IsChecked = config.ShowConfirmationDialog;
            ChkLogSessions.IsChecked = true; // Always log for audit
        }

        /// <summary>
        /// Export configuration
        /// </summary>
        private void ExportConfig_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                FileName = $"RMM_Config_{DateTime.Now:yyyyMMdd}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var config = RemoteControlManager.GetConfiguration();
                    var serializer = new JavaScriptSerializer();
                    string json = serializer.Serialize(config);

                    File.WriteAllText(dialog.FileName, json);

                    MessageBox.Show("Configuration exported successfully!",
                        "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export configuration:\n\n{ex.Message}",
                        "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Import configuration
        /// </summary>
        private void ImportConfig_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(dialog.FileName);
                    var serializer = new JavaScriptSerializer();
                    var config = serializer.Deserialize<RemoteControlConfig>(json);

                    RemoteControlManager.SaveConfiguration(config);
                    Initialize(); // Reload UI

                    MessageBox.Show("Configuration imported successfully!",
                        "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to import configuration:\n\n{ex.Message}",
                        "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Reset all configuration
        /// </summary>
        private void ResetAll_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will reset ALL remote control configurations to defaults.\n\n" +
                "All tool settings and credentials will be cleared.\n\n" +
                "Continue?",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Clear credentials
                SecureCredentialManager.DeleteAllCredentials();

                // Reset configuration
                Properties.Settings.Default.RemoteControlConfigJson = "";
                Properties.Settings.Default.Save();

                // Reload
                RemoteControlManager.Initialize();
                Initialize();

                MessageBox.Show("All configurations have been reset.",
                    "Reset Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Recent targets button clicked - TAG: #VERSION_7 #QUICK_WINS
        /// </summary>
        private void BtnRecentTargets_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Load recent targets from settings
                var recentTargets = LoadRecentTargets();

                if (recentTargets.Count == 0)
                {
                    MessageBox.Show("No recent targets found.\n\nTargets will appear here after you launch remote sessions.",
                        "No Recent Targets", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Create context menu to show recent targets
                var contextMenu = new ContextMenu();

                foreach (var target in recentTargets)
                {
                    var menuItem = new MenuItem
                    {
                        Header = $"🖥️ {target}",
                        Tag = target
                    };
                    menuItem.Click += (s, args) =>
                    {
                        TxtRemoteTarget.Text = (s as MenuItem)?.Tag as string;
                    };
                    contextMenu.Items.Add(menuItem);
                }

                // Add separator and clear option
                contextMenu.Items.Add(new Separator());
                var clearItem = new MenuItem { Header = "🗑️ Clear Recent Targets" };
                clearItem.Click += (s, args) =>
                {
                    ClearRecentTargets();
                    MessageBox.Show("Recent targets cleared.", "Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
                };
                contextMenu.Items.Add(clearItem);

                // Show context menu
                contextMenu.PlacementTarget = sender as Button;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                contextMenu.IsOpen = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load recent targets:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Load recent targets from settings
        /// </summary>
        private System.Collections.Generic.List<string> LoadRecentTargets()
        {
            try
            {
                string json = Properties.Settings.Default.RecentTargets;
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var serializer = new JavaScriptSerializer();
                    return serializer.Deserialize<System.Collections.Generic.List<string>>(json)
                        ?? new System.Collections.Generic.List<string>();
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to load recent targets", ex);
            }
            return new System.Collections.Generic.List<string>();
        }

        /// <summary>
        /// Clear recent targets
        /// </summary>
        private void ClearRecentTargets()
        {
            Properties.Settings.Default.RecentTargets = "";
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Clear history
        /// </summary>
        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            _connectionHistory.Clear();
            MessageBox.Show("Connection history cleared.", "Cleared",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Load connection history
        /// </summary>
        private void LoadConnectionHistory()
        {
            _connectionHistory = new ObservableCollection<ConnectionHistoryItem>();
            ListConnectionHistory.ItemsSource = _connectionHistory;
        }

        /// <summary>
        /// Add item to connection history
        /// </summary>
        private void AddToHistory(RmmToolType toolType, string target, bool success, string error)
        {
            var item = new ConnectionHistoryItem
            {
                Timestamp = DateTime.Now,
                TimeAgo = "just now",
                ToolName = toolType.ToString(),
                Target = target,
                Success = success,
                Status = success ? "(Success)" : $"(Failed: {error})",
                StatusColor = success ? "Green" : "Red"
            };

            _connectionHistory.Insert(0, item);

            // Keep only last 10
            while (_connectionHistory.Count > 10)
                _connectionHistory.RemoveAt(_connectionHistory.Count - 1);
        }
    }

    // TAG: #RMM_TAB #VIEW_MODELS
    /// <summary>
    /// View model for tool configuration grid
    /// </summary>
    public class ToolConfigViewModel
    {
        public string ToolName { get; set; }
        public bool Enabled { get; set; }
        public bool IsConfigured { get; set; }
        public RmmToolType ToolType { get; set; }
        public DateTime LastTested { get; set; }
        public RmmToolConfig Config { get; set; }

        public string StatusDisplay { get; set; }
        public string StatusColor { get; set; }

        public ToolConfigViewModel()
        {
            UpdateStatus();
        }

        public void UpdateStatus()
        {
            if (!IsConfigured)
            {
                StatusDisplay = "⭕ Not configured";
                StatusColor = "#FF666666";
            }
            else if (LastTested == DateTime.MinValue)
            {
                StatusDisplay = "⚠️ Not tested";
                StatusColor = "#FFFFAA00";
            }
            else if ((DateTime.Now - LastTested).TotalDays > 7)
            {
                StatusDisplay = "⚠️ Test expired";
                StatusColor = "#FFFFAA00";
            }
            else
            {
                StatusDisplay = "✅ Ready";
                StatusColor = "#FF00AA00";
            }
        }
    }

    /// <summary>
    /// Connection history item
    /// </summary>
    public class ConnectionHistoryItem
    {
        public DateTime Timestamp { get; set; }
        public string TimeAgo { get; set; }
        public string ToolName { get; set; }
        public string Target { get; set; }
        public bool Success { get; set; }
        public string Status { get; set; }
        public string StatusColor { get; set; }
    }
}
