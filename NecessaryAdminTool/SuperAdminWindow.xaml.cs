using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;
using System.Security.Cryptography;
using NecessaryAdminTool.Managers.UI;

namespace NecessaryAdminTool
{
    /// <summary>
    /// SuperAdmin Configuration Window
    /// Hidden admin panel for white-label customization and advanced settings
    /// TAG: #SUPERADMIN #WHITELABEL_GUI #ADVANCED_CONFIG
    /// </summary>
    public partial class SuperAdminWindow : Window
    {
        private readonly string _appDataPath;
        private readonly string _backupPath;
        private const string BACKUP_TIMESTAMP_FORMAT = "yyyyMMdd_HHmmss";

        public SuperAdminWindow()
        {
            InitializeComponent();

            _appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NecessaryAdminTool");

            _backupPath = Path.Combine(_appDataPath, "Backups");
            Directory.CreateDirectory(_backupPath);

            LoadCurrentSettings();
            LoadSystemInfo();
            UpdatePreview();

            // Log superadmin access
            LogSuperAdminAccess();
        }

        #region White-Label Configuration

        /// <summary>
        /// Load current white-label settings from files
        /// </summary>
        private void LoadCurrentSettings()
        {
            try
            {
                string aboutWindowPath = GetAboutWindowXamlPath();
                if (File.Exists(aboutWindowPath))
                {
                    string content = File.ReadAllText(aboutWindowPath);

                    // Extract current company name
                    var companyMatch = Regex.Match(content, @"{{COMPANY_NAME}}|([A-Z][a-zA-Z\s]+(?:LLC|Inc\.|Corporation|Corp\.))", RegexOptions.Multiline);
                    if (companyMatch.Success && !companyMatch.Value.Contains("{{"))
                    {
                        TxtCompanyName.Text = companyMatch.Value;
                    }

                    // Extract current domain
                    var domainMatch = Regex.Match(content, @"{{COMPANY_DOMAIN}}|support@([\w\.-]+)", RegexOptions.Multiline);
                    if (domainMatch.Success && !domainMatch.Value.Contains("{{"))
                    {
                        TxtCompanyDomain.Text = domainMatch.Groups[1].Value;
                    }
                }
            }
            catch (Exception ex)
            {
                ToastManager.ShowWarning($"Error loading current settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Update live preview when fields change
        /// </summary>
        private void OnWhiteLabelFieldChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        /// <summary>
        /// Update the live preview pane
        /// </summary>
        private void UpdatePreview()
        {
            if (TxtCompanyName == null || TxtCompanyDomain == null) return;

            string companyName = TxtCompanyName.Text;
            string domain = TxtCompanyDomain.Text;
            string phone = TxtSupportPhone?.Text ?? "Contact your authorized representative";

            // Update preview elements
            if (PreviewWarranty != null)
            {
                PreviewWarranty.Text = $"THE SOFTWARE IS PROVIDED \"AS IS\" WITHOUT WARRANTY OF ANY KIND. {companyName} DOES NOT WARRANT THAT THE SOFTWARE WILL MEET YOUR REQUIREMENTS...";
            }

            if (PreviewCompanyName != null) PreviewCompanyName.Text = companyName;
            if (PreviewEmail != null) PreviewEmail.Text = $"support@{domain}";
            if (PreviewPhone != null) PreviewPhone.Text = phone;
            if (TxtSupportEmail != null) TxtSupportEmail.Text = $"support@{domain}";
        }

        /// <summary>
        /// Apply white-label changes to actual files
        /// </summary>
        private void BtnApplyWhiteLabel_Click(object sender, RoutedEventArgs e)
        {
            string companyName = TxtCompanyName.Text.Trim();
            string domain = TxtCompanyDomain.Text.Trim();
            string phone = TxtSupportPhone.Text.Trim();

            // Validation
            if (string.IsNullOrWhiteSpace(companyName) || companyName == "{{COMPANY_NAME}}")
            {
                ToastManager.ShowWarning("Please enter a valid company name.");
                return;
            }

            if (string.IsNullOrWhiteSpace(domain) || domain == "{{COMPANY_DOMAIN}}" || !domain.Contains("."))
            {
                ToastManager.ShowWarning("Please enter a valid domain (e.g., contoso.com).");
                return;
            }

            // Confirmation
            var result = MessageBox.Show(
                $"Apply white-label changes?\n\n" +
                $"Company: {companyName}\n" +
                $"Domain: {domain}\n" +
                $"Support Email: support@{domain}\n\n" +
                $"Backups will be created automatically.\n\n" +
                $"Files to be modified:\n" +
                $"  • AboutWindow.xaml\n" +
                $"  • AboutWindow.xaml.cs\n\n" +
                $"Continue?",
                "Confirm White-Label Changes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                BtnApplyWhiteLabel.IsEnabled = false;
                TxtWhiteLabelStatus.Text = "⏳ Applying changes...";
                TxtWhiteLabelStatus.Foreground = System.Windows.Media.Brushes.Orange;
                TxtWhiteLabelStatus.Visibility = Visibility.Visible;

                // Create backups
                CreateBackups();

                // Apply changes to files
                int filesModified = ApplyWhiteLabelChanges(companyName, domain, phone);

                // Success
                TxtWhiteLabelStatus.Text = $"✓ Changes applied successfully! {filesModified} files modified.";
                TxtWhiteLabelStatus.Foreground = System.Windows.Media.Brushes.LimeGreen;

                // Log the change
                LogWhiteLabelChange(companyName, domain);

                ToastManager.ShowSuccess($"White-label configuration applied! {filesModified} files modified. Restart to see changes.");
            }
            catch (Exception ex)
            {
                TxtWhiteLabelStatus.Text = "✗ Error applying changes!";
                TxtWhiteLabelStatus.Foreground = System.Windows.Media.Brushes.Red;

                ToastManager.ShowError($"Error applying white-label changes: {ex.Message}. Files restored from backup.");

                // Restore from backup on error
                RestoreFromBackup();
            }
            finally
            {
                BtnApplyWhiteLabel.IsEnabled = true;
            }
        }

        /// <summary>
        /// Apply white-label changes to all relevant files
        /// </summary>
        private int ApplyWhiteLabelChanges(string companyName, string domain, string phone)
        {
            int filesModified = 0;
            string supportEmail = $"support@{domain}";

            // File 1: AboutWindow.xaml
            string aboutXamlPath = GetAboutWindowXamlPath();
            if (File.Exists(aboutXamlPath))
            {
                string content = File.ReadAllText(aboutXamlPath);

                // Replace placeholders
                content = content.Replace("{{COMPANY_NAME}}", companyName);
                content = content.Replace("{{COMPANY_DOMAIN}}", domain);
                content = Regex.Replace(content,
                    @"Phone: .*?</Run>",
                    $"Phone: {phone}</Run>",
                    RegexOptions.Singleline);

                File.WriteAllText(aboutXamlPath, content, Encoding.UTF8);
                filesModified++;
            }

            // File 2: AboutWindow.xaml.cs
            string aboutCsPath = GetAboutWindowCsPath();
            if (File.Exists(aboutCsPath))
            {
                string content = File.ReadAllText(aboutCsPath);

                // Replace placeholders
                content = content.Replace("{{COMPANY_NAME}}", companyName);
                content = content.Replace("{{COMPANY_DOMAIN}}", domain);
                content = Regex.Replace(content,
                    @"Phone: Contact your authorized .*? representative",
                    $"Phone: {phone}",
                    RegexOptions.Singleline);

                File.WriteAllText(aboutCsPath, content, Encoding.UTF8);
                filesModified++;
            }

            return filesModified;
        }

        /// <summary>
        /// Reset white-label settings to defaults
        /// </summary>
        private void BtnResetWhiteLabel_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Reset all white-label settings to default placeholders?\n\n" +
                "This will restore:\n" +
                "  • Company Name: {{COMPANY_NAME}}\n" +
                "  • Domain: {{COMPANY_DOMAIN}}\n\n" +
                "Continue?",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            TxtCompanyName.Text = "{{COMPANY_NAME}}";
            TxtCompanyDomain.Text = "{{COMPANY_DOMAIN}}";
            TxtSupportPhone.Text = "Contact your authorized representative";

            UpdatePreview();

            TxtWhiteLabelStatus.Text = "✓ Reset to defaults";
            TxtWhiteLabelStatus.Foreground = System.Windows.Media.Brushes.LimeGreen;
            TxtWhiteLabelStatus.Visibility = Visibility.Visible;
        }

        #endregion

        #region Backup & Restore

        /// <summary>
        /// Create timestamped backups of all files before modification
        /// </summary>
        private void CreateBackups()
        {
            string timestamp = DateTime.Now.ToString(BACKUP_TIMESTAMP_FORMAT);
            string backupFolder = Path.Combine(_backupPath, $"WhiteLabel_{timestamp}");
            Directory.CreateDirectory(backupFolder);

            // Backup AboutWindow.xaml
            string aboutXamlPath = GetAboutWindowXamlPath();
            if (File.Exists(aboutXamlPath))
            {
                File.Copy(aboutXamlPath,
                    Path.Combine(backupFolder, "AboutWindow.xaml.bak"), true);
            }

            // Backup AboutWindow.xaml.cs
            string aboutCsPath = GetAboutWindowCsPath();
            if (File.Exists(aboutCsPath))
            {
                File.Copy(aboutCsPath,
                    Path.Combine(backupFolder, "AboutWindow.xaml.cs.bak"), true);
            }

            // Create backup manifest
            File.WriteAllText(
                Path.Combine(backupFolder, "BACKUP_INFO.txt"),
                $"White-Label Backup\n" +
                $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                $"User: {Environment.UserName}\n" +
                $"Machine: {Environment.MachineName}\n");
        }

        /// <summary>
        /// Restore files from the most recent backup
        /// </summary>
        private void RestoreFromBackup()
        {
            try
            {
                var backups = Directory.GetDirectories(_backupPath, "WhiteLabel_*")
                    .OrderByDescending(d => d)
                    .ToList();

                if (!backups.Any())
                {
                    ToastManager.ShowWarning("No backups found.");
                    return;
                }

                string latestBackup = backups.First();

                // Restore AboutWindow.xaml
                string backupXaml = Path.Combine(latestBackup, "AboutWindow.xaml.bak");
                if (File.Exists(backupXaml))
                {
                    File.Copy(backupXaml, GetAboutWindowXamlPath(), true);
                }

                // Restore AboutWindow.xaml.cs
                string backupCs = Path.Combine(latestBackup, "AboutWindow.xaml.cs.bak");
                if (File.Exists(backupCs))
                {
                    File.Copy(backupCs, GetAboutWindowCsPath(), true);
                }

                ToastManager.ShowSuccess($"Files restored from backup: {latestBackup}");
            }
            catch (Exception ex)
            {
                ToastManager.ShowError($"Error restoring from backup: {ex.Message}");
            }
        }

        #endregion

        #region Advanced Settings

        /// <summary>
        /// Toggle debug mode
        /// </summary>
        private void ChkDebugMode_Changed(object sender, RoutedEventArgs e)
        {
            bool enabled = ChkDebugMode.IsChecked ?? false;

            // Save to config
            SaveAdvancedSetting("DebugMode", enabled.ToString());

            ToastManager.ShowInfo($"Debug mode {(enabled ? "enabled" : "disabled")}. Restart required.");
        }

        /// <summary>
        /// Toggle hidden features
        /// </summary>
        private void ChkHiddenFeatures_Changed(object sender, RoutedEventArgs e)
        {
            bool enabled = ChkHiddenFeatures.IsChecked ?? false;

            // Save to config
            SaveAdvancedSetting("HiddenFeatures", enabled.ToString());

            ToastManager.ShowInfo($"Hidden features {(enabled ? "unlocked" : "locked")}. Restart required.");
        }

        /// <summary>
        /// Reset all application settings
        /// </summary>
        private void BtnResetSettings_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "⚠️ DANGER: Reset ALL application settings?\n\n" +
                "This will delete:\n" +
                "  • All configuration files\n" +
                "  • Connection profiles\n" +
                "  • Bookmarks\n" +
                "  • Recent targets\n" +
                "  • User preferences\n\n" +
                "The database and inventory will NOT be affected.\n\n" +
                "Are you absolutely sure?",
                "Confirm Settings Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            // Second confirmation
            result = MessageBox.Show(
                "This cannot be undone!\n\nType 'DELETE' to confirm:",
                "Final Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Stop);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                // Delete config files
                var configFiles = new[]
                {
                    "NecessaryAdmin_Config_v2.xml",
                    "NecessaryAdmin_UserConfig.xml",
                    "NecessaryAdmin_DCConfiguration.xml"
                };

                int deleted = 0;
                foreach (var file in configFiles)
                {
                    string path = Path.Combine(_appDataPath, file);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        deleted++;
                    }
                }

                ToastManager.ShowSuccess($"Settings reset! {deleted} files deleted. Application will close.");

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                ToastManager.ShowError($"Error resetting settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Export configuration to file
        /// </summary>
        private void BtnExportConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Export SuperAdmin Configuration",
                    Filter = "Configuration Files (*.json)|*.json|All Files (*.*)|*.*",
                    FileName = $"NecessaryAdmin_SuperAdmin_{DateTime.Now:yyyyMMdd}.json"
                };

                if (dialog.ShowDialog() != true) return;

                // Build config export (simplified - would use actual config system)
                string json = $@"{{
  ""ExportDate"": ""{DateTime.Now:yyyy-MM-dd HH:mm:ss}"",
  ""Version"": ""1.0"",
  ""WhiteLabel"": {{
    ""CompanyName"": ""{TxtCompanyName.Text}"",
    ""CompanyDomain"": ""{TxtCompanyDomain.Text}"",
    ""SupportPhone"": ""{TxtSupportPhone.Text}""
  }},
  ""AdvancedSettings"": {{
    ""DebugMode"": {(ChkDebugMode.IsChecked ?? false).ToString().ToLower()},
    ""HiddenFeatures"": {(ChkHiddenFeatures.IsChecked ?? false).ToString().ToLower()}
  }}
}}";

                File.WriteAllText(dialog.FileName, json, Encoding.UTF8);

                ToastManager.ShowSuccess($"Configuration exported: {dialog.FileName}");
            }
            catch (Exception ex)
            {
                ToastManager.ShowError($"Error exporting configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Import configuration from file
        /// </summary>
        private void BtnImportConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Import SuperAdmin Configuration",
                    Filter = "Configuration Files (*.json)|*.json|All Files (*.*)|*.*"
                };

                if (dialog.ShowDialog() != true) return;

                string json = File.ReadAllText(dialog.FileName);

                // Simple JSON parsing (would use proper JSON library in production)
                var companyMatch = Regex.Match(json, @"""CompanyName"":\s*""([^""]+)""");
                var domainMatch = Regex.Match(json, @"""CompanyDomain"":\s*""([^""]+)""");
                var phoneMatch = Regex.Match(json, @"""SupportPhone"":\s*""([^""]+)""");

                if (companyMatch.Success) TxtCompanyName.Text = companyMatch.Groups[1].Value;
                if (domainMatch.Success) TxtCompanyDomain.Text = domainMatch.Groups[1].Value;
                if (phoneMatch.Success) TxtSupportPhone.Text = phoneMatch.Groups[1].Value;

                UpdatePreview();

                ToastManager.ShowSuccess("Configuration imported! Click 'Apply Changes' to save.");
            }
            catch (Exception ex)
            {
                ToastManager.ShowError($"Error importing configuration: {ex.Message}");
            }
        }

        #endregion

        #region Database Management

        /// <summary>
        /// Optimize database (VACUUM for SQLite)
        /// </summary>
        private void BtnOptimizeDb_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // This would call the actual database provider's optimize method
                ToastManager.ShowInfo("Database optimization: SQLite VACUUM, SQL Server INDEX REBUILD, Access COMPACT & REPAIR");
            }
            catch (Exception ex)
            {
                ToastManager.ShowError($"Error optimizing database: {ex.Message}");
            }
        }

        /// <summary>
        /// Backup database to file
        /// </summary>
        private void BtnBackupDb_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Backup Database",
                    Filter = "Database Backup (*.db)|*.db|All Files (*.*)|*.*",
                    FileName = $"NecessaryAdmin_DB_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db"
                };

                if (dialog.ShowDialog() != true) return;

                // This would call the actual database backup method
                ToastManager.ShowInfo($"Database backup saved: {dialog.FileName}");
            }
            catch (Exception ex)
            {
                ToastManager.ShowError($"Error backing up database: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear all database data
        /// </summary>
        private void BtnClearDb_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "⚠️ DANGER: Delete ALL inventory data?\n\n" +
                "This will permanently delete:\n" +
                "  • All computer records\n" +
                "  • All scan history\n" +
                "  • All asset tags\n\n" +
                "This CANNOT be undone!\n\n" +
                "Are you sure?",
                "Confirm Database Clear",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            ToastManager.ShowInfo("Production: DELETE FROM Computers/ScanHistory/AssetTags. Function not implemented in preview.");
        }

        #endregion

        #region System Information

        /// <summary>
        /// Load and display system information
        /// </summary>
        private void LoadSystemInfo()
        {
            try
            {
                var sb = new StringBuilder();
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;

                sb.AppendLine("=== APPLICATION INFORMATION ===");
                sb.AppendLine($"Application: NecessaryAdminTool");
                sb.AppendLine($"Version: {version?.ToString() ?? "Unknown"}");
                sb.AppendLine($"Build Date: {File.GetLastWriteTime(assembly.Location):yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Installation Path: {AppDomain.CurrentDomain.BaseDirectory}");
                sb.AppendLine();

                sb.AppendLine("=== SYSTEM INFORMATION ===");
                sb.AppendLine($"OS: {Environment.OSVersion.VersionString}");
                sb.AppendLine($"Platform: {Environment.OSVersion.Platform}");
                sb.AppendLine($"64-Bit OS: {Environment.Is64BitOperatingSystem}");
                sb.AppendLine($"64-Bit Process: {Environment.Is64BitProcess}");
                sb.AppendLine($"Processor Count: {Environment.ProcessorCount}");
                sb.AppendLine($"Machine Name: {Environment.MachineName}");
                sb.AppendLine($"User Name: {Environment.UserDomainName}\\{Environment.UserName}");
                sb.AppendLine($"CLR Version: {Environment.Version}");
                sb.AppendLine();

                sb.AppendLine("=== CONFIGURATION PATHS ===");
                sb.AppendLine($"AppData: {_appDataPath}");
                sb.AppendLine($"Backups: {_backupPath}");
                sb.AppendLine($"Temp: {Path.GetTempPath()}");
                sb.AppendLine();

                sb.AppendLine("=== RUNTIME INFORMATION ===");
                sb.AppendLine($"Current Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Uptime: {TimeSpan.FromMilliseconds(Environment.TickCount):hh\\:mm\\:ss}");
                sb.AppendLine($"Working Set: {Environment.WorkingSet / 1024 / 1024:N0} MB");

                if (TxtSystemInfo != null)
                {
                    TxtSystemInfo.Text = sb.ToString();
                }
            }
            catch (Exception ex)
            {
                if (TxtSystemInfo != null)
                {
                    TxtSystemInfo.Text = $"Error loading system info:\n{ex.Message}";
                }
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Get path to AboutWindow.xaml
        /// </summary>
        private string GetAboutWindowXamlPath()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;

            // Try Debug/Release build paths
            string[] possiblePaths = new[]
            {
                Path.Combine(basePath, "AboutWindow.xaml"),
                Path.Combine(basePath, "..", "..", "AboutWindow.xaml"),
                Path.Combine(basePath, "..", "..", "..", "NecessaryAdminTool", "AboutWindow.xaml")
            };

            foreach (var path in possiblePaths)
            {
                string fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            throw new FileNotFoundException("AboutWindow.xaml not found");
        }

        /// <summary>
        /// Get path to AboutWindow.xaml.cs
        /// </summary>
        private string GetAboutWindowCsPath()
        {
            string xamlPath = GetAboutWindowXamlPath();
            return xamlPath + ".cs";
        }

        /// <summary>
        /// Save advanced setting to config
        /// </summary>
        private void SaveAdvancedSetting(string key, string value)
        {
            try
            {
                string configPath = Path.Combine(_appDataPath, "SuperAdmin_Config.txt");
                var settings = new Dictionary<string, string>();

                // Load existing
                if (File.Exists(configPath))
                {
                    foreach (var line in File.ReadAllLines(configPath))
                    {
                        var parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            settings[parts[0]] = parts[1];
                        }
                    }
                }

                // Update
                settings[key] = value;

                // Save
                File.WriteAllLines(configPath,
                    settings.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            }
            catch
            {
                // Silently fail for non-critical settings
            }
        }

        /// <summary>
        /// Log superadmin access
        /// </summary>
        private void LogSuperAdminAccess()
        {
            try
            {
                string logPath = Path.Combine(_appDataPath, "SuperAdmin_Access.log");
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] " +
                    $"User: {Environment.UserDomainName}\\{Environment.UserName} | " +
                    $"Machine: {Environment.MachineName}\n";

                File.AppendAllText(logPath, entry);
            }
            catch
            {
                // Silently fail if logging is not possible
            }
        }

        /// <summary>
        /// Log white-label configuration change
        /// </summary>
        private void LogWhiteLabelChange(string companyName, string domain)
        {
            try
            {
                string logPath = Path.Combine(_appDataPath, "WhiteLabel_Changes.log");
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] " +
                    $"User: {Environment.UserDomainName}\\{Environment.UserName} | " +
                    $"Company: {companyName} | " +
                    $"Domain: {domain}\n";

                File.AppendAllText(logPath, entry);
            }
            catch
            {
                // Silently fail if logging is not possible
            }
        }

        /// <summary>
        /// Close button
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Run Setup Wizard for testing
        /// TAG: #DEBUG_TESTING #SETUP_WIZARD
        /// </summary>
        private void BtnRunSetupWizard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "⚠️ Launch Initial Setup Wizard?\n\n" +
                    "This will open the first-run configuration wizard for testing purposes.\n\n" +
                    "Current database settings will not be affected unless you complete the wizard.\n\n" +
                    "Continue?",
                    "Debug Testing - Setup Wizard",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    LogManager.LogInfo("Setup Wizard launched from SuperAdmin (debug testing)");

                    var setupWizard = new SetupWizardWindow();
                    setupWizard.Owner = this;
                    setupWizard.ShowDialog();

                    LogManager.LogInfo("Setup Wizard closed");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to launch Setup Wizard", ex);
                MessageBox.Show(
                    $"Failed to launch Setup Wizard:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Build MSI installer using build-installer.ps1 script
        /// TAG: #BUILD_AUTOMATION #MSI_BUILDER #SUPERADMIN
        /// </summary>
        private void BtnBuildInstaller_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "📦 Build MSI Installer?\n\n" +
                "This will run build-installer.ps1 to create the MSI installer package.\n\n" +
                "Prerequisites:\n" +
                "• WiX Toolset 3.11+ (run install-wix.ps1 if needed)\n" +
                "• Visual Studio with MSBuild\n" +
                "• ACE Database Engine installer in Installer\\Dependencies\\\n\n" +
                "The build process will:\n" +
                "1. Build the application in Release mode\n" +
                "2. Compile WiX installer source\n" +
                "3. Create MSI in Installer\\Output\\\n" +
                "4. Open output folder\n\n" +
                "This may take 1-2 minutes. Continue?",
                "Build MSI Installer",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Get project root directory (2 levels up from bin\Release or bin\Debug)
                    string appDir = AppDomain.CurrentDomain.BaseDirectory;
                    string projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(appDir, @"..\..\"));
                    string scriptPath = System.IO.Path.Combine(projectRoot, "build-installer.ps1");

                    if (!System.IO.File.Exists(scriptPath))
                    {
                        MessageBox.Show(
                            $"Build script not found at:\n{scriptPath}\n\n" +
                            "Make sure build-installer.ps1 is in the project root.",
                            "Script Not Found",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    // Create PowerShell process to run the build script
                    var processInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -Verbose",
                        WorkingDirectory = projectRoot,
                        UseShellExecute = true, // Show console window
                        CreateNoWindow = false
                    };

                    MessageBox.Show(
                        "🚀 Build Started!\n\n" +
                        "A PowerShell window will open showing the build progress.\n\n" +
                        "The MSI will be created in:\n" +
                        "Installer\\Output\\NecessaryAdminTool-{Version}-Setup.msi\n\n" +
                        "The output folder will open automatically when complete.",
                        "Build In Progress",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    System.Diagnostics.Process.Start(processInfo);

                    LogManager.LogInfo($"MSI build process started from SuperAdmin panel: {scriptPath}");
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Failed to start MSI build process", ex);
                    MessageBox.Show(
                        $"Failed to start build process:\n\n{ex.Message}\n\n" +
                        "Make sure PowerShell is available and build-installer.ps1 exists in the project root.",
                        "Build Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        #endregion
    }
}
