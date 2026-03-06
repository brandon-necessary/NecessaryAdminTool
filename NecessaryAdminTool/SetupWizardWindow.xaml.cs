using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using NecessaryAdminTool.Managers.UI;
// TAG: #SETUP_WIZARD #VERSION_1_0 #DATABASE_TESTING

namespace NecessaryAdminTool
{
    /// <summary>
    /// Setup Wizard for first-run configuration with integrated database testing
    /// TAG: #FIRST_RUN #DATABASE_CONFIG #DATABASE_TESTING
    /// </summary>
    public partial class SetupWizardWindow : Window
    {
        public string SelectedDatabaseType { get; private set; }
        public string DatabasePath { get; private set; }
        public bool InstallService { get; private set; }
        public int ScanIntervalHours { get; private set; }

#if DEBUG
        // TAG: #DEBUG_BYPASS - Track rapid clicks for debug bypass (5 clicks within 2 seconds)
        private int _debugBypassClickCount = 0;
        private DateTime _debugBypassFirstClick = DateTime.MinValue;
#endif

        public SetupWizardWindow()
        {
            InitializeComponent();
            Title = $"{LogoConfig.PRODUCT_NAME} - Initial Setup";
            LogManager.LogInfo("Setup Wizard opened");

            // TAG: #VERSION_DISPLAY #VERSION_ENGINE - Version info pulled from LogoConfig (assembly-based)
            TxtVersionBadge.Text = $"{LogoConfig.VERSION} ({LogoConfig.FULL_VERSION.TrimStart('v')})";
            TxtBuildDate.Text = $"Built: {LogoConfig.COMPILED_DATE_SHORT}";

            #if DEBUG
            // Show debug bypass button in DEBUG builds only
            BtnDebugBypassTrigger.Visibility = Visibility.Visible;
            LogManager.LogInfo("DEBUG MODE: Debug bypass button enabled (click version badge 5x to skip setup)");
            #endif
        }

        /// <summary>
        /// Test database connectivity and all provider methods
        /// TAG: #DATABASE_TESTING
        /// </summary>
        private async void BtnTestDatabase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Determine database type
                string dbType = "SQLite";
                if (RbSqlServer.IsChecked == true)
                    dbType = "SqlServer";
                else if (RbAccess.IsChecked == true)
                    dbType = "Access";
                else if (RbCsv.IsChecked == true)
                    dbType = "CSV";

                string dbPath = TxtDatabasePath.Text;

                LogManager.LogInfo($"Starting database test: {dbType} at {dbPath}");

                // Create test directory if it doesn't exist
                if (!Directory.Exists(dbPath))
                {
                    Directory.CreateDirectory(dbPath);
                }

                // Temporarily save settings for test
                var originalDbType = Properties.Settings.Default.DatabaseType;
                var originalDbPath = Properties.Settings.Default.DatabasePath;

                Properties.Settings.Default.DatabaseType = dbType;
                Properties.Settings.Default.DatabasePath = dbPath;
                Properties.Settings.Default.Save();

                try
                {
                    // Create provider and run tests
                    using (var provider = await Data.DataProviderFactory.CreateProviderAsync())
                    {
                        var tester = new Data.DatabaseTester(provider);

                        // Show progress message
                        var progressMsg = new System.Windows.Controls.TextBlock
                        {
                            Text = "Running database tests...\nThis may take 10-30 seconds.",
                            Foreground = System.Windows.Media.Brushes.White,
                            TextAlignment = TextAlignment.Center,
                            FontSize = 14,
                            Margin = new Thickness(20)
                        };

                        // Run tests (this will take some time)
                        var result = await Task.Run(() => tester.RunAllTestsAsync());

                        // Show results
                        var resultWindow = new Window
                        {
                            Title = "Database Test Results",
                            Width = 800,
                            Height = 600,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            Owner = this,
                            Background = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["BgDarkBrush"]
                        };

                        var scrollViewer = new System.Windows.Controls.ScrollViewer
                        {
                            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                            Margin = new Thickness(20)
                        };

                        var resultText = new System.Windows.Controls.TextBlock
                        {
                            Text = result.Log,
                            Foreground = result.Success
                                ? (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["AccentGreenBrush"]
                                : (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["DangerBrush"],
                            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap
                        };

                        scrollViewer.Content = resultText;
                        resultWindow.Content = scrollViewer;
                        resultWindow.ShowDialog();

                        // Log summary
                        LogManager.LogInfo($"Database test completed: {result.Summary}");

                        if (result.Success)
                        {
                            ToastManager.ShowSuccess(
                                $"All database tests passed! Tests: {result.PassedTests}/{result.TotalTests} | " +
                                $"Duration: {result.Duration.TotalSeconds:F2}s | The {dbType} provider is working correctly.");
                        }
                        else
                        {
                            ToastManager.ShowWarning(
                                $"Some database tests failed! Passed: {result.PassedTests}/{result.TotalTests}, " +
                                $"Failed: {result.FailedTests}/{result.TotalTests}. Review the test log for details.");
                        }
                    }
                }
                finally
                {
                    // Restore original settings
                    Properties.Settings.Default.DatabaseType = originalDbType;
                    Properties.Settings.Default.DatabasePath = originalDbPath;
                    Properties.Settings.Default.Save();
                }
            }
            catch (NotImplementedException ex) when (ex.Message.Contains("SQLite"))
            {
                LogManager.LogWarning($"SQLite provider not enabled: {ex.Message}");
                ToastManager.ShowWarning(
                    "SQLite Provider Not Configured. SQLite requires System.Data.SQLite NuGet package. Switch to CSV/JSON?",
                    "Switch to CSV/JSON",
                    () => RbCsv.IsChecked = true);
            }
            catch (System.Data.OleDb.OleDbException ex) when (ex.Message.Contains("Syntax error"))
            {
                LogManager.LogWarning($"Access database schema error (expected for new databases): {ex.Message}");
                ToastManager.ShowInfo(
                    "Access Database Test - Partial Success. Schema error is NORMAL for new databases. " +
                    "ACE Engine is working and schema will be created automatically on first use.");
            }
            catch (System.Data.OleDb.OleDbException ex) when (ex.Message.Contains("not registered"))
            {
                LogManager.LogError("ACE Database Engine not installed", ex);
                ToastManager.ShowError(
                    "Microsoft Access Database Engine Not Found. Install ACE Database Engine 2016 (64-bit) from microsoft.com, or switch to CSV/JSON.",
                    "Switch to CSV/JSON",
                    () => RbCsv.IsChecked = true);
            }
            catch (System.Data.SqlClient.SqlException ex)
            {
                LogManager.LogError("SQL Server connection failed", ex);
                string hint = "";
                if (ex.Number == -1 || ex.Number == 53)
                {
                    hint = "\n\n💡 Hint: SQL Server may not be running or network issues.";
                }
                else if (ex.Number == 18456)
                {
                    hint = "\n\n💡 Hint: Authentication failed. Check credentials.";
                }

                ToastManager.ShowError(
                    $"SQL Server Connection Failed: {ex.Message}{hint} — Check server name, network, and credentials.");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Database test failed", ex);
                ToastManager.ShowError(
                    $"Database Test Error: {ex.Message} ({ex.GetType().Name}). Check the log for details.");
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select database location";
                    dialog.SelectedPath = TxtDatabasePath.Text;

                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        TxtDatabasePath.Text = dialog.SelectedPath;
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Folder browser error", ex);
                ToastManager.ShowError($"Error selecting folder: {ex.Message}");
            }
        }

        private void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Determine database type
                if (RbSqlite.IsChecked == true)
                    SelectedDatabaseType = "SQLite";
                else if (RbSqlServer.IsChecked == true)
                    SelectedDatabaseType = "SqlServer";
                else if (RbAccess.IsChecked == true)
                    SelectedDatabaseType = "Access";
                else if (RbCsv.IsChecked == true)
                    SelectedDatabaseType = "CSV";

                DatabasePath = TxtDatabasePath.Text;
                InstallService = ChkInstallService.IsChecked == true;

                // Get scan interval
                ScanIntervalHours = CmbScanInterval.SelectedIndex switch
                {
                    0 => 1,  // Every hour
                    1 => 2,  // Every 2 hours (default)
                    2 => 4,  // Every 4 hours
                    3 => 24, // Daily
                    4 => 0,  // Manual only
                    _ => 2
                };

                // Save settings
                Properties.Settings.Default.DatabaseType = SelectedDatabaseType;
                Properties.Settings.Default.DatabasePath = DatabasePath;
                Properties.Settings.Default.ServiceEnabled = InstallService;
                Properties.Settings.Default.ScanIntervalHours = ScanIntervalHours;
                Properties.Settings.Default.SetupCompleted = true;
                Properties.Settings.Default.Save();

                LogManager.LogInfo($"Setup completed: {SelectedDatabaseType} at {DatabasePath}, " +
                    $"Service={InstallService}, Interval={ScanIntervalHours}h");

                // Create scheduled task if service is enabled
                // TAG: #BACKGROUND_SERVICE #SCHEDULED_TASK #ERROR_HANDLING
                if (InstallService && ScanIntervalHours > 0)
                {
                    try
                    {
                        bool isAdmin = ScheduledTaskManager.IsAdministrator();
                        bool taskCreated = ScheduledTaskManager.CreateTask(ScanIntervalHours, runAsAdmin: isAdmin);

                        if (taskCreated)
                        {
                            LogManager.LogInfo($"Scheduled task created successfully for {ScanIntervalHours}h interval");

                            // Enhanced success message with clear next steps
                            string intervalText = ScanIntervalHours == 1 ? "hour" :
                                                 ScanIntervalHours == 24 ? "day" :
                                                 $"{ScanIntervalHours} hours";

                            ToastManager.ShowSuccess(
                                $"Background Service configured! Runs every {intervalText} as " +
                                $"{(isAdmin ? "Administrator" : "Current User")}. Manage in Options → Background Service.");
                        }
                        else
                        {
                            LogManager.LogWarning("Failed to create scheduled task during setup");

                            // Enhanced failure message with actionable guidance
                            ToastManager.ShowWarning(
                                "Background Service Setup Failed. The scheduled task could not be created. " +
                                "Try enabling manually in Options → Background Service, or run as Administrator.");
                        }
                    }
                    catch (Exception taskEx)
                    {
                        LogManager.LogError("Exception creating scheduled task during setup", taskEx);

                        // Enhanced exception message with technical details
                        ToastManager.ShowError(
                            $"Background Service Setup Error: {taskEx.Message} ({taskEx.GetType().Name}). " +
                            $"Enable manually in Options → Background Service or run as Administrator.");
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Setup wizard finish error", ex);
                ToastManager.ShowError($"Error saving configuration: {ex.Message}");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Open DATABASE_SETUP_GUIDE.md in default markdown viewer or text editor
        /// TAG: #DATABASE_SETUP #USER_GUIDE
        /// </summary>
        private void BtnDatabaseGuide_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get path to DATABASE_SETUP_GUIDE.md (should be in application directory or repo root)
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string guidePath = Path.Combine(appDir, "DATABASE_SETUP_GUIDE.md");

                // If not found in app directory, check parent directories (for development)
                if (!File.Exists(guidePath))
                {
                    var parentDir = Directory.GetParent(appDir);
                    while (parentDir != null && !File.Exists(guidePath))
                    {
                        guidePath = Path.Combine(parentDir.FullName, "DATABASE_SETUP_GUIDE.md");
                        if (!File.Exists(guidePath))
                        {
                            parentDir = parentDir.Parent;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (File.Exists(guidePath))
                {
                    LogManager.LogInfo($"Opening Database Setup Guide: {guidePath}");
                    System.Diagnostics.Process.Start(guidePath);
                }
                else
                {
                    LogManager.LogWarning($"DATABASE_SETUP_GUIDE.md not found in application directory");
                    ToastManager.ShowWarning(
                        $"Database Setup Guide not found at {appDir}DATABASE_SETUP_GUIDE.md. " +
                        "It should be included with the installer, or download from the GitHub repository.");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to open Database Setup Guide", ex);
                ToastManager.ShowError($"Error opening guide: {ex.Message}");
            }
        }

        /// <summary>
        /// Debug bypass trigger - 5 rapid clicks to bypass setup (DEBUG builds only)
        /// TAG: #DEBUG_BYPASS #SUPERADMIN
        /// </summary>
        private void BtnDebugBypassTrigger_Click(object sender, RoutedEventArgs e)
        {
            #if DEBUG
            var now = DateTime.Now;

            // Reset if more than 2 seconds since first click
            if ((now - _debugBypassFirstClick).TotalSeconds > 2)
            {
                _debugBypassClickCount = 0;
                _debugBypassFirstClick = now;
            }

            _debugBypassClickCount++;
            LogManager.LogInfo($"DEBUG: Debug bypass click {_debugBypassClickCount}/5");

            if (_debugBypassClickCount >= 5)
            {
                LogManager.LogWarning("DEBUG MODE: Setup wizard bypassed via 5 rapid clicks - marking setup as complete");

                // Set minimal default configuration (path must match SetupWizard XAML default)
                Properties.Settings.Default.DatabaseType = "CSV";
                Properties.Settings.Default.DatabasePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "NecessaryAdminTool");
                Properties.Settings.Default.ServiceEnabled = false;
                Properties.Settings.Default.ScanIntervalHours = 2;
                Properties.Settings.Default.SetupCompleted = true;
                Properties.Settings.Default.Save();

                ToastManager.ShowWarning(
                    "DEBUG MODE: Setup Bypassed! Default CSV/JSON config applied. This bypass is only available in DEBUG builds.");

                DialogResult = true;
                Close();
            }
            #endif
        }

        /// <summary>
        /// Export empty database template file
        /// TAG: #DATABASE_TEMPLATES #SETUP_WIZARD
        /// </summary>
        private async void BtnExportTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Determine database type
                string dbType = "SQLite";
                string fileExtension = ".db";
                string fileFilter = "SQLite Database|*.db";

                if (RbSqlServer.IsChecked == true)
                {
                    ToastManager.ShowInfo(
                        "SQL Server databases are created on the server. Use SQL Server Management Studio to create a new database, then configure the connection string in NecessaryAdminTool.");
                    return;
                }
                else if (RbAccess.IsChecked == true)
                {
                    dbType = "Access";
                    fileExtension = ".accdb";
                    fileFilter = "Access Database|*.accdb";
                }
                else if (RbCsv.IsChecked == true)
                {
                    ToastManager.ShowInfo(
                        "CSV/JSON databases are created automatically as text files. Just specify a folder location and NecessaryAdminTool will create the necessary files on first use.");
                    return;
                }

                // Show save file dialog
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = $"Export {dbType} Database Template",
                    Filter = fileFilter,
                    FileName = $"NecessaryAdminTool_Template{fileExtension}",
                    DefaultExt = fileExtension
                };

                if (saveDialog.ShowDialog() == true)
                {
                    string templatePath = saveDialog.FileName;

                    LogManager.LogInfo($"Exporting {dbType} database template to: {templatePath}");

                    // Create empty database using the provider
                    var originalDbType = Properties.Settings.Default.DatabaseType;
                    var originalDbPath = Properties.Settings.Default.DatabasePath;

                    try
                    {
                        // Set temporary configuration
                        Properties.Settings.Default.DatabaseType = dbType;
                        Properties.Settings.Default.DatabasePath = Path.GetDirectoryName(templatePath);
                        Properties.Settings.Default.Save();

                        // Create empty database
                        using (var provider = await Data.DataProviderFactory.CreateProviderAsync())
                        {
                            // Initialize database (creates schema)
                            var stats = await provider.GetDatabaseStatsAsync();

                            LogManager.LogInfo($"Template database created: {stats.TotalComputers} computers");
                        }

                        // For SQLite, rename the created file to the user's chosen name
                        if (dbType == "SQLite")
                        {
                            var createdDbPath = Path.Combine(
                                Properties.Settings.Default.DatabasePath,
                                "NecessaryAdminTool.db");

                            if (File.Exists(createdDbPath))
                            {
                                // Copy to template location
                                File.Copy(createdDbPath, templatePath, true);
                                // Delete temporary database
                                File.Delete(createdDbPath);
                            }
                        }
                        else if (dbType == "Access")
                        {
                            var createdDbPath = Path.Combine(
                                Properties.Settings.Default.DatabasePath,
                                "NecessaryAdminTool.accdb");

                            if (File.Exists(createdDbPath))
                            {
                                // Copy to template location
                                File.Copy(createdDbPath, templatePath, true);
                                // Delete temporary database
                                File.Delete(createdDbPath);
                            }
                        }

                        LogManager.LogInfo($"Template exported successfully: {templatePath}");

                        ToastManager.ShowSuccess(
                            $"Database template exported successfully to: {templatePath}");

                        // Open folder containing template
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{templatePath}\"");
                    }
                    finally
                    {
                        // Restore original settings
                        Properties.Settings.Default.DatabaseType = originalDbType;
                        Properties.Settings.Default.DatabasePath = originalDbPath;
                        Properties.Settings.Default.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to export database template", ex);
                ToastManager.ShowError($"Failed to export database template: {ex.Message}");
            }
        }
    }
}
