using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using NecessaryAdminTool.Managers.UI;
using Microsoft.Win32;

namespace NecessaryAdminTool
{
    // TAG: #TOOL_CONFIG #DIALOG
    /// <summary>
    /// Configuration dialog for individual RMM tools
    /// </summary>
    public partial class ToolConfigWindow : Window
    {
        private RmmToolConfig _config;
        private Dictionary<string, TextBox> _settingsControls = new Dictionary<string, TextBox>();
        private Dictionary<string, PasswordBox> _credentialControls = new Dictionary<string, PasswordBox>();

        public ToolConfigWindow(RmmToolConfig config)
        {
            InitializeComponent();
            _config = config;
            TxtTitle.Text = $"Configure {config.ToolName}";
            BuildConfigurationUI();
            LoadCurrentSettings();
        }

        /// <summary>
        /// Build configuration UI based on tool type
        /// </summary>
        private void BuildConfigurationUI()
        {
            ConfigPanel.Children.Clear();

            switch (_config.ToolType)
            {
                case RmmToolType.AnyDesk:
                    BuildAnyDeskUI();
                    break;
                case RmmToolType.ScreenConnect:
                    BuildScreenConnectUI();
                    break;
                case RmmToolType.TeamViewer:
                    BuildTeamViewerUI();
                    break;
                case RmmToolType.ManageEngine:
                    BuildManageEngineUI();
                    break;
                case RmmToolType.RemotePC:
                    BuildRemotePCUI();
                    break;
                case RmmToolType.Dameware:
                    BuildDamewareUI();
                    break;
            }
        }

        private void BuildAnyDeskUI()
        {
            AddFilePathControl("ExePath", "AnyDesk Executable Path:", @"C:\Program Files (x86)\AnyDesk\AnyDesk.exe");
            AddDropdownControl("ConnectionMode", "Connection Mode:",
                new[] { ("attended", "Attended (No password)"), ("unattended", "Unattended (With password)") });
            AddPasswordControl("Password", "Unattended Password (Optional):");
        }

        private void BuildScreenConnectUI()
        {
            AddTextControl("ServerUrl", "Server URL:", "yourserver.screenconnect.com");
            AddTextControl("Port", "Port:", "443");
            AddDropdownControl("AuthMethod", "Authentication Method:",
                new[] { ("url", "URL Launch (No API)"), ("api", "API (Requires token)") });
            AddPasswordControl("ApiToken", "API Token (Optional):");
        }

        private void BuildTeamViewerUI()
        {
            AddFilePathControl("ExePath", "TeamViewer Executable:", @"C:\Program Files\TeamViewer\TeamViewer.exe");
            AddDropdownControl("AuthMethod", "Authentication Method:",
                new[] { ("cli", "CLI (Password)"), ("api", "API (OAuth token)") });
            AddPasswordControl("Password", "Password (CLI mode):");
            AddPasswordControl("AccessToken", "Access Token (API mode):");
        }

        private void BuildManageEngineUI()
        {
            AddTextControl("ServerUrl", "Server URL:", "https://yourserver");
            AddTextControl("Port", "Port:", "8383");
            AddPasswordControl("ApiToken", "API Token:");
            AddTextControl("ApiUser", "API Username:", "administrator");
        }

        private void BuildRemotePCUI()
        {
            AddTextControl("ApiUrl", "API URL:", "https://api.remotepc.com");
            AddTextControl("TeamId", "Team ID:");
            AddPasswordControl("ApiKey", "API Key:");
        }

        private void BuildDamewareUI()
        {
            AddTextControl("ServerUrl", "Server URL:", "https://yourserver.dameware.com");
            AddTextControl("Department", "Department:", "IT Support");
            AddPasswordControl("ApiKey", "API Key:");
        }

        private void AddTextControl(string key, string label, string placeholder = "")
        {
            var textBlock = new TextBlock
            {
                Text = label,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var textBox = new TextBox
            {
                Tag = key,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 26, 26)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42)),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 12)
            };

            _settingsControls[key] = textBox;

            ConfigPanel.Children.Add(textBlock);
            ConfigPanel.Children.Add(textBox);
        }

        private void AddPasswordControl(string key, string label)
        {
            var textBlock = new TextBlock
            {
                Text = label,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var passwordBox = new PasswordBox
            {
                Tag = key,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 26, 26)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42)),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 12)
            };

            _credentialControls[key] = passwordBox;

            ConfigPanel.Children.Add(textBlock);
            ConfigPanel.Children.Add(passwordBox);
        }

        private void AddFilePathControl(string key, string label, string defaultPath)
        {
            var textBlock = new TextBlock
            {
                Text = label,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textBox = new TextBox
            {
                Tag = key,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 26, 26)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42)),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 10
            };
            Grid.SetColumn(textBox, 0);

            var button = new Button
            {
                Content = "BROWSE",
                Tag = textBox,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 6, 12, 6),
                FontSize = 9,
                Margin = new Thickness(8, 0, 0, 0)
            };
            button.Click += Browse_Click;
            Grid.SetColumn(button, 1);

            grid.Children.Add(textBox);
            grid.Children.Add(button);
            grid.Margin = new Thickness(0, 0, 0, 12);

            _settingsControls[key] = textBox;

            ConfigPanel.Children.Add(textBlock);
            ConfigPanel.Children.Add(grid);
        }

        private void AddDropdownControl(string key, string label, (string value, string display)[] options)
        {
            var textBlock = new TextBlock
            {
                Text = label,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var comboBox = new ComboBox
            {
                Tag = key,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 26, 26)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42)),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 12)
            };

            foreach (var option in options)
            {
                comboBox.Items.Add(new ComboBoxItem
                {
                    Content = option.display,
                    Tag = option.value
                });
            }

            comboBox.SelectedIndex = 0;
            _settingsControls[key] = new TextBox { Tag = comboBox }; // Hack to store combobox reference

            ConfigPanel.Children.Add(textBlock);
            ConfigPanel.Children.Add(comboBox);
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var textBox = button?.Tag as TextBox;

            if (textBox != null)
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    textBox.Text = dialog.FileName;
                }
            }
        }

        private void LoadCurrentSettings()
        {
            foreach (var kvp in _settingsControls)
            {
                if (_config.Settings.ContainsKey(kvp.Key))
                {
                    if (kvp.Value.Tag is ComboBox combo)
                    {
                        string value = _config.Settings[kvp.Key];
                        foreach (ComboBoxItem item in combo.Items)
                        {
                            if (item.Tag.ToString() == value)
                            {
                                combo.SelectedItem = item;
                                break;
                            }
                        }
                    }
                    else
                    {
                        kvp.Value.Text = _config.Settings[kvp.Key];
                    }
                }
            }

            foreach (var kvp in _credentialControls)
            {
                string cred = SecureCredentialManager.RetrieveCredential(_config.ToolName, kvp.Key);
                if (!string.IsNullOrEmpty(cred))
                {
                    kvp.Value.Password = cred;
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Save settings
            foreach (var kvp in _settingsControls)
            {
                if (kvp.Value.Tag is ComboBox combo)
                {
                    var selected = combo.SelectedItem as ComboBoxItem;
                    _config.Settings[kvp.Key] = selected?.Tag?.ToString() ?? "";
                }
                else
                {
                    _config.Settings[kvp.Key] = kvp.Value.Text;
                }
            }

            // Save credentials
            bool credentialSaveFailed = false;
            foreach (var kvp in _credentialControls)
            {
                if (!string.IsNullOrEmpty(kvp.Value.Password))
                {
                    bool stored = SecureCredentialManager.StoreCredential(_config.ToolName, kvp.Key, kvp.Value.Password);
                    if (!stored)
                        credentialSaveFailed = true;
                }
            }

            _config.IsConfigured = true;
            if (credentialSaveFailed)
                ToastManager.ShowWarning($"{_config.ToolName} settings saved, but one or more credentials could not be stored in Windows Credential Manager.");
            else
                ToastManager.ShowSuccess($"{_config.ToolName} configuration saved.");
            DialogResult = true;
            Close();
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            BtnTest.IsEnabled = false;
            BtnTest.Content = "TESTING...";

            try
            {
                // Temporarily save settings for test
                Save_Click(sender, e);

                bool success = RemoteControlManager.TestConnection(_config.ToolType);

                if (success)
                {
                    ToastManager.ShowSuccess($"{_config.ToolName} connection test successful!");
                }
                else
                {
                    ToastManager.ShowError($"{_config.ToolName} connection test failed. Check settings and try again.");
                }
            }
            finally
            {
                BtnTest.IsEnabled = true;
                BtnTest.Content = "TEST CONNECTION";
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
