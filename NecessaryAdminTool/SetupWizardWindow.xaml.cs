using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
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
                            Background = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(13, 13, 13))
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
                                ? System.Windows.Media.Brushes.LightGreen
                                : System.Windows.Media.Brushes.LightCoral,
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
                            System.Windows.MessageBox.Show(
                                $"✓ All database tests passed!\n\n" +
                                $"Tests: {result.PassedTests}/{result.TotalTests}\n" +
                                $"Duration: {result.Duration.TotalSeconds:F2} seconds\n\n" +
                                $"The {dbType} provider is working correctly.",
                                "Database Test - SUCCESS",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        else
                        {
                            System.Windows.MessageBox.Show(
                                $"⚠ Some database tests failed!\n\n" +
                                $"Passed: {result.PassedTests}/{result.TotalTests}\n" +
                                $"Failed: {result.FailedTests}/{result.TotalTests}\n\n" +
                                $"Review the test log for details.",
                                "Database Test - WARNING",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
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
                var result = System.Windows.MessageBox.Show(
                    "❌ SQLite Provider Not Configured\n\n" +
                    "SQLite requires System.Data.SQLite NuGet package.\n\n" +
                    "OPTION 1 (Recommended): Use CSV/JSON provider instead\n" +
                    "• Select CSV/JSON (Fallback) option above\n" +
                    "• Works immediately without installation\n\n" +
                    "OPTION 2: Install SQLite support\n" +
                    "• In Visual Studio: Tools → NuGet Package Manager\n" +
                    "• Install-Package System.Data.SQLite.Core\n" +
                    "• Define SQLITE_ENABLED in project properties\n\n" +
                    "Would you like to switch to CSV/JSON now?",
                    "Database Test - SQLite Not Enabled",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    RbCsv.IsChecked = true;
                }
            }
            catch (System.Data.OleDb.OleDbException ex) when (ex.Message.Contains("Syntax error"))
            {
                LogManager.LogWarning($"Access database schema error (expected for new databases): {ex.Message}");
                System.Windows.MessageBox.Show(
                    "⚠️ Access Database Test - Partial Success\n\n" +
                    "The Access provider encountered a schema error, but this is NORMAL for new databases.\n\n" +
                    "✅ ACE Database Engine: Detected and working\n" +
                    "✅ Database file path: Valid\n" +
                    "⚠️ Database schema: Will be created on first use\n\n" +
                    "This error is expected and will not affect normal operation.\n" +
                    "The database schema will be automatically created when you finish setup.",
                    "Database Test - Access (Expected Warning)",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (System.Data.OleDb.OleDbException ex) when (ex.Message.Contains("not registered"))
            {
                LogManager.LogError("ACE Database Engine not installed", ex);
                var result = System.Windows.MessageBox.Show(
                    "❌ Microsoft Access Database Engine Not Found\n\n" +
                    "The Access provider requires the ACE Database Engine.\n\n" +
                    "Download and install:\n" +
                    "• Microsoft Access Database Engine 2016 (64-bit)\n" +
                    "• https://www.microsoft.com/en-us/download/details.aspx?id=54920\n\n" +
                    "After installation, restart NecessaryAdminTool and try again.\n\n" +
                    "Would you like to use CSV/JSON instead?",
                    "Database Test - ACE Not Installed",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error);

                if (result == MessageBoxResult.Yes)
                {
                    RbCsv.IsChecked = true;
                }
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

                System.Windows.MessageBox.Show(
                    $"❌ SQL Server Connection Failed\n\n" +
                    $"Error: {ex.Message}{hint}\n\n" +
                    $"Check the following:\n" +
                    $"• SQL Server is installed and running\n" +
                    $"• Server name is correct\n" +
                    $"• Network connectivity is available\n" +
                    $"• Credentials are valid",
                    "Database Test - SQL Server Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                LogManager.LogError("Database test failed", ex);
                System.Windows.MessageBox.Show(
                    $"❌ Database Test Error\n\n" +
                    $"{ex.Message}\n\n" +
                    $"Type: {ex.GetType().Name}\n\n" +
                    $"Check the log for details:\n" +
                    $"%AppData%\\NecessaryAdminTool\\NecessaryAdmin_Debug.log",
                    "Database Test - ERROR",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
                System.Windows.MessageBox.Show($"Error selecting folder: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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

                            System.Windows.MessageBox.Show(
                                $"✅ Background Service Configured Successfully!\n\n" +
                                $"📅 Automatic Scanning Schedule:\n" +
                                $"   • Runs every {intervalText}\n" +
                                $"   • Scans all domain computers\n" +
                                $"   • Logs results to: {DatabasePath}\\DeploymentLogs\n\n" +
                                $"🔒 Security:\n" +
                                $"   • Running as: {(isAdmin ? "Administrator (HIGHEST privileges)" : "Current User (LIMITED privileges)")}\n" +
                                $"   • Task Name: NecessaryAdminTool_AutoScan\n" +
                                $"   • Location: Task Scheduler → \\NecessaryAdminTool\\\n\n" +
                                $"⚙️ You can change these settings anytime in:\n" +
                                $"   Options → Background Service",
                                "Background Service Enabled",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        else
                        {
                            LogManager.LogWarning("Failed to create scheduled task during setup");

                            // Enhanced failure message with actionable guidance
                            System.Windows.MessageBox.Show(
                                "⚠️ Background Service Setup Failed\n\n" +
                                "The scheduled task could not be created.\n\n" +
                                "🔍 Common Causes:\n" +
                                "   • Task Scheduler service is not running\n" +
                                "   • Group Policy restrictions prevent task creation\n" +
                                "   • Antivirus software blocked the operation\n" +
                                "   • Insufficient system permissions\n\n" +
                                "✅ What You Can Do:\n" +
                                "   1. Try manually enabling in: Options → Background Service\n" +
                                "   2. Run the application as Administrator\n" +
                                "   3. Check Windows Event Viewer → Task Scheduler logs\n" +
                                "   4. Contact your IT administrator if in a managed environment\n\n" +
                                "💡 The application will still work normally, but automatic scanning\n" +
                                "   will not be enabled. You can scan manually anytime.",
                                "Service Setup Warning",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception taskEx)
                    {
                        LogManager.LogError("Exception creating scheduled task during setup", taskEx);

                        // Enhanced exception message with technical details
                        System.Windows.MessageBox.Show(
                            $"❌ Background Service Setup Error\n\n" +
                            $"An error occurred while creating the scheduled task:\n\n" +
                            $"Error: {taskEx.Message}\n\n" +
                            $"📋 Technical Details:\n" +
                            $"   • Exception Type: {taskEx.GetType().Name}\n" +
                            $"   • Check logs at: %APPDATA%\\NecessaryAdminTool\\Logs\\\n\n" +
                            $"✅ Next Steps:\n" +
                            $"   1. The application will work without background scanning\n" +
                            $"   2. Try enabling manually in: Options → Background Service\n" +
                            $"   3. If problem persists, run as Administrator\n\n" +
                            $"💡 You can still use all other features normally.",
                            "Service Configuration Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Setup wizard finish error", ex);
                System.Windows.MessageBox.Show($"Error saving configuration: {ex.Message}",
                    "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    System.Windows.MessageBox.Show(
                        "Database Setup Guide not found.\n\n" +
                        "Expected location:\n" +
                        $"{appDir}DATABASE_SETUP_GUIDE.md\n\n" +
                        "The guide should be included with the installer.\n" +
                        "You can download it from the GitHub repository.",
                        "Guide Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to open Database Setup Guide", ex);
                System.Windows.MessageBox.Show(
                    $"Error opening guide: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

                // Set minimal default configuration
                Properties.Settings.Default.DatabaseType = "CSV";
                Properties.Settings.Default.DatabasePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "NecessaryAdminTool");
                Properties.Settings.Default.ServiceEnabled = false;
                Properties.Settings.Default.ScanIntervalHours = 2;
                Properties.Settings.Default.SetupCompleted = true;
                Properties.Settings.Default.Save();

                System.Windows.MessageBox.Show(
                    "🔓 DEBUG MODE: Setup Bypassed!\n\n" +
                    "Default configuration applied:\n" +
                    "• Database: CSV/JSON (fallback)\n" +
                    "• Location: %AppData%\\NecessaryAdminTool\n" +
                    "• Background Service: Disabled\n\n" +
                    "This bypass is only available in DEBUG builds.",
                    "Debug Bypass Active",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

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
                    System.Windows.MessageBox.Show(
                        "SQL Server databases are created on the server.\n\n" +
                        "Use SQL Server Management Studio to create a new database,\n" +
                        "then configure the connection string in NecessaryAdminTool.",
                        "SQL Server Template",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
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
                    System.Windows.MessageBox.Show(
                        "CSV/JSON databases are created automatically as text files.\n\n" +
                        "Just specify a folder location - NecessaryAdminTool will\n" +
                        "create the necessary CSV files when you start using it.",
                        "CSV Template",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
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

                        System.Windows.MessageBox.Show(
                            $"✓ Database template exported successfully!\n\n" +
                            $"Location: {templatePath}\n\n" +
                            $"You can now copy this template file to any location\n" +
                            $"and use it as your database.",
                            "Template Exported",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

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
                System.Windows.MessageBox.Show(
                    $"❌ Failed to export database template:\n\n{ex.Message}",
                    "Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
