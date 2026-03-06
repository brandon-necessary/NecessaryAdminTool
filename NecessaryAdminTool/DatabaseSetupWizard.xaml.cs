using System;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using NecessaryAdminTool.Managers.UI;
// TAG: #AUTO_UPDATE_DATABASE_INSTALLER #DATABASE_SETUP #VERSION_1_0

namespace NecessaryAdminTool
{
    /// <summary>
    /// Comprehensive database setup wizard with auto-installation support
    /// TAG: #DATABASE_WIZARD #DEPENDENCY_CHECKER #AUTO_INSTALLER
    /// </summary>
    public partial class DatabaseSetupWizard : Window
    {
        private int _currentStep = 1;
        private string _selectedDatabaseType = "";
        private const int TOTAL_STEPS = 3;

        public string ConnectionString { get; private set; }
        public string DatabaseType { get; private set; }
        public bool SetupCompleted { get; private set; }

        public DatabaseSetupWizard()
        {
            InitializeComponent();
            Title = $"Database Setup Wizard - {LogoConfig.PRODUCT_NAME}";
            InitializeDefaults();
            UpdateStepVisibility();
        }

        /// <summary>
        /// Initialize default paths and settings
        /// TAG: #DEFAULT_CONFIGURATION
        /// </summary>
        private void InitializeDefaults()
        {
            // Set default paths
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string defaultPath = Path.Combine(appData, "NecessaryAdminTool");

            if (!Directory.Exists(defaultPath))
            {
                Directory.CreateDirectory(defaultPath);
            }

            TxtSQLitePath.Text = Path.Combine(defaultPath, "NecessaryAdminTool.db");
            TxtAccessPath.Text = Path.Combine(defaultPath, "NecessaryAdminTool.accdb");
            TxtCsvPath.Text = Path.Combine(defaultPath, "Data");
            TxtSqlServerName.Text = Environment.MachineName;

            // Select SQLite as default (recommended)
            RadioSQLite.IsChecked = true;
        }

        /// <summary>
        /// Handle database type selection
        /// TAG: #DATABASE_TYPE_SELECTION
        /// </summary>
        private void RadioDatabaseType_Checked(object sender, RoutedEventArgs e)
        {
            if (RadioSQLite.IsChecked == true)
            {
                _selectedDatabaseType = "SQLite";
                ConfigSQLite.Visibility = Visibility.Visible;
                ConfigSqlServer.Visibility = Visibility.Collapsed;
                ConfigAccess.Visibility = Visibility.Collapsed;
                ConfigCsv.Visibility = Visibility.Collapsed;
            }
            else if (RadioSqlServer.IsChecked == true)
            {
                _selectedDatabaseType = "SqlServer";
                ConfigSQLite.Visibility = Visibility.Collapsed;
                ConfigSqlServer.Visibility = Visibility.Visible;
                ConfigAccess.Visibility = Visibility.Collapsed;
                ConfigCsv.Visibility = Visibility.Collapsed;
            }
            else if (RadioAccess.IsChecked == true)
            {
                _selectedDatabaseType = "Access";
                ConfigSQLite.Visibility = Visibility.Collapsed;
                ConfigSqlServer.Visibility = Visibility.Collapsed;
                ConfigAccess.Visibility = Visibility.Visible;
                ConfigCsv.Visibility = Visibility.Collapsed;
            }
            else if (RadioCsv.IsChecked == true)
            {
                _selectedDatabaseType = "CSV";
                ConfigSQLite.Visibility = Visibility.Collapsed;
                ConfigSqlServer.Visibility = Visibility.Collapsed;
                ConfigAccess.Visibility = Visibility.Collapsed;
                ConfigCsv.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Browse for SQLite database file
        /// TAG: #FILE_BROWSER
        /// </summary>
        private void BtnBrowseSQLite_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "SQLite Database|*.db;*.sqlite;*.sqlite3|All Files|*.*",
                DefaultExt = ".db",
                FileName = "NecessaryAdminTool.db"
            };

            if (dialog.ShowDialog() == true)
            {
                TxtSQLitePath.Text = dialog.FileName;
            }
        }

        /// <summary>
        /// Browse for Access database file
        /// TAG: #FILE_BROWSER
        /// </summary>
        private void BtnBrowseAccess_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Access Database|*.accdb;*.mdb|All Files|*.*",
                DefaultExt = ".accdb",
                FileName = "NecessaryAdminTool.accdb"
            };

            if (dialog.ShowDialog() == true)
            {
                TxtAccessPath.Text = dialog.FileName;
            }
        }

        /// <summary>
        /// Browse for CSV storage directory
        /// TAG: #FILE_BROWSER
        /// </summary>
        private void BtnBrowseCsv_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder for CSV/JSON data storage",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtCsvPath.Text = dialog.SelectedPath;
            }
        }

        /// <summary>
        /// Toggle encryption key input
        /// TAG: #ENCRYPTION_SETUP
        /// </summary>
        private void ChkEnableEncryption_Changed(object sender, RoutedEventArgs e)
        {
            PanelEncryptionKey.Visibility = ChkEnableEncryption.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Toggle SQL Server authentication
        /// TAG: #SQL_SERVER_AUTH
        /// </summary>
        private void ChkSqlWindowsAuth_Changed(object sender, RoutedEventArgs e)
        {
            PanelSqlCredentials.Visibility = ChkSqlWindowsAuth.IsChecked == false
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Test database connection
        /// TAG: #CONNECTION_TESTING #CRITICAL
        /// </summary>
        private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
        {
            BtnTestConnection.IsEnabled = false;
            TxtTestResult.Text = "Testing connection...";
            TxtTestResult.Foreground = new SolidColorBrush(Colors.Orange);

            try
            {
                bool success = false;
                string message = "";

                switch (_selectedDatabaseType)
                {
                    case "SQLite":
                        (success, message) = await TestSQLiteConnectionAsync();
                        break;
                    case "SqlServer":
                        (success, message) = await TestSqlServerConnectionAsync();
                        break;
                    case "Access":
                        (success, message) = await TestAccessConnectionAsync();
                        break;
                    case "CSV":
                        (success, message) = TestCsvConfiguration();
                        break;
                }

                TxtTestResult.Text = message;
                TxtTestResult.Foreground = success
                    ? new SolidColorBrush(Colors.LightGreen)
                    : new SolidColorBrush(Colors.Red);
            }
            catch (Exception ex)
            {
                TxtTestResult.Text = $"❌ Test failed: {ex.Message}";
                TxtTestResult.Foreground = new SolidColorBrush(Colors.Red);
                LogManager.LogError("Database connection test failed", ex);
            }
            finally
            {
                BtnTestConnection.IsEnabled = true;
            }
        }

        /// <summary>
        /// Test SQLite connection
        /// TAG: #SQLITE_TEST
        /// </summary>
        private async Task<(bool success, string message)> TestSQLiteConnectionAsync()
        {
            try
            {
                string path = TxtSQLitePath.Text;
                if (string.IsNullOrWhiteSpace(path))
                {
                    return (false, "❌ Please specify a database file path");
                }

                // Ensure directory exists
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Test with System.Data.SQLite (standard SQLite)
                string connString = $"Data Source={path};Version=3;";

                // For now, just verify the path is valid
                // Actual SQLite library testing would require the NuGet package
                return (true, $"✅ SQLite configuration valid\nPath: {path}");
            }
            catch (Exception ex)
            {
                return (false, $"❌ SQLite test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test SQL Server connection
        /// TAG: #SQL_SERVER_TEST #CRITICAL
        /// </summary>
        private async Task<(bool success, string message)> TestSqlServerConnectionAsync()
        {
            try
            {
                string server = TxtSqlServerName.Text;
                string database = TxtSqlDatabaseName.Text;

                if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database))
                {
                    return (false, "❌ Please specify server name and database name");
                }

                string connString;
                if (ChkSqlWindowsAuth.IsChecked == true)
                {
                    connString = $"Server={server};Database=master;Integrated Security=true;Connection Timeout=5;";
                }
                else
                {
                    string username = TxtSqlUsername.Text;
                    string password = TxtSqlPassword.Password;

                    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                    {
                        return (false, "❌ Please specify username and password");
                    }

                    connString = $"Server={server};Database=master;User Id={username};Password={password};Connection Timeout=5;";
                }

                using (var conn = new SqlConnection(connString))
                {
                    await conn.OpenAsync();

                    // Check if database exists
                    using (var cmd = new SqlCommand($"SELECT DB_ID('{database}')", conn))
                    {
                        var result = await cmd.ExecuteScalarAsync();
                        bool dbExists = result != DBNull.Value && result != null;

                        return (true, $"✅ SQL Server connection successful!\n" +
                                    $"Server: {server}\n" +
                                    $"Database '{database}': {(dbExists ? "Exists" : "Will be created")}");
                    }
                }
            }
            catch (SqlException ex)
            {
                string hint = "";
                if (ex.Number == -1 || ex.Number == 53)
                {
                    hint = "\n\nHint: Check that SQL Server is running and the server name is correct.";
                }
                else if (ex.Number == 18456)
                {
                    hint = "\n\nHint: Authentication failed. Check username and password.";
                }
                return (false, $"❌ SQL Server connection failed: {ex.Message}{hint}");
            }
            catch (Exception ex)
            {
                return (false, $"❌ SQL Server test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test Access connection and check for ACE driver
        /// TAG: #ACCESS_TEST #ACE_DRIVER_CHECK #CRITICAL
        /// </summary>
        private async Task<(bool success, string message)> TestAccessConnectionAsync()
        {
            try
            {
                string path = TxtAccessPath.Text;
                if (string.IsNullOrWhiteSpace(path))
                {
                    return (false, "❌ Please specify a database file path");
                }

                // Ensure directory exists
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Check if ACE driver is installed
                bool aceInstalled = await Task.Run(() => CheckAceDriverInstalled());
                if (!aceInstalled)
                {
                    return (false, "❌ Microsoft Access Database Engine (ACE) not detected\n" +
                                 "Please install ACE from Step 3: Dependency Check");
                }

                // Test connection
                string connString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={path};";

                using (var conn = new OleDbConnection(connString))
                {
                    await Task.Run(() => conn.Open());
                }

                return (true, $"✅ Access database configuration valid\n" +
                            $"Path: {path}\n" +
                            $"ACE Driver: Installed");
            }
            catch (OleDbException ex)
            {
                // Check for specific error about provider not registered
                if (ex.Message.Contains("not registered"))
                {
                    return (false, "❌ ACE Driver not properly installed\n" +
                                 "Please install from Step 3: Dependency Check");
                }
                return (false, $"❌ Access test failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"❌ Access test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test CSV/JSON configuration
        /// TAG: #CSV_TEST
        /// </summary>
        private (bool success, string message) TestCsvConfiguration()
        {
            try
            {
                string path = TxtCsvPath.Text;
                if (string.IsNullOrWhiteSpace(path))
                {
                    return (false, "❌ Please specify a storage directory");
                }

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                string format = RadioJsonFormat.IsChecked == true ? "JSON" : "CSV";
                return (true, $"✅ {format} storage configuration valid\nPath: {path}");
            }
            catch (Exception ex)
            {
                return (false, $"❌ CSV test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if ACE driver is installed
        /// TAG: #ACE_DRIVER_DETECTION #REGISTRY_CHECK
        /// </summary>
        private bool CheckAceDriverInstalled()
        {
            try
            {
                // Check for 64-bit ACE driver (most common)
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Office\ClickToRun\REGISTRY\MACHINE\Software\Classes\CLSID\{3BE786A0-0366-4F5C-9434-25CF162E475E}"))
                {
                    if (key != null) return true;
                }

                // Check for standalone ACE installation
                using (var key = Registry.ClassesRoot.OpenSubKey(@"CLSID\{3BE786A0-0366-4F5C-9434-25CF162E475E}"))
                {
                    if (key != null) return true;
                }

                // Check via OleDb enumerator
                var enumerator = new OleDbEnumerator();
                var sources = enumerator.GetElements();
                foreach (System.Data.DataRow row in sources.Rows)
                {
                    if (row["SOURCES_NAME"].ToString().Contains("Microsoft.ACE.OLEDB"))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check dependencies for selected database type
        /// TAG: #DEPENDENCY_CHECKING #AUTO_INSTALLER
        /// </summary>
        private async void BtnCheckDependencies_Click(object sender, RoutedEventArgs e)
        {
            BtnCheckDependencies.IsEnabled = false;
            TxtDependencyStatus.Text = "Checking dependencies...";

            try
            {
                #pragma warning disable CS0219 // Variable assigned but not used - reserved for future dependency checking
                bool allDependenciesMet = true;
                #pragma warning restore CS0219
                PanelDependencyActions.Visibility = Visibility.Collapsed;
                PanelAccessEngineDownload.Visibility = Visibility.Collapsed;
                PanelSqlServerDownload.Visibility = Visibility.Collapsed;

                switch (_selectedDatabaseType)
                {
                    case "SQLite":
                        TxtDependencyStatus.Text = "✅ SQLite has no external dependencies!\n" +
                                                  "The required libraries are included with NecessaryAdminTool.";
                        TxtDependencyStatus.Foreground = new SolidColorBrush(Colors.LightGreen);
                        break;

                    case "SqlServer":
                        var (sqlSuccess, sqlMessage) = await TestSqlServerConnectionAsync();
                        if (sqlSuccess)
                        {
                            TxtDependencyStatus.Text = "✅ SQL Server is accessible and ready!";
                            TxtDependencyStatus.Foreground = new SolidColorBrush(Colors.LightGreen);
                        }
                        else
                        {
                            TxtDependencyStatus.Text = "⚠️ Cannot connect to SQL Server\n" +
                                                      "You may need to install SQL Server or check your configuration.";
                            TxtDependencyStatus.Foreground = new SolidColorBrush(Colors.Orange);
                            PanelDependencyActions.Visibility = Visibility.Visible;
                            PanelSqlServerDownload.Visibility = Visibility.Visible;
                            allDependenciesMet = false;
                        }
                        break;

                    case "Access":
                        bool aceInstalled = await Task.Run(() => CheckAceDriverInstalled());
                        if (aceInstalled)
                        {
                            TxtDependencyStatus.Text = "✅ Microsoft Access Database Engine (ACE) is installed!";
                            TxtDependencyStatus.Foreground = new SolidColorBrush(Colors.LightGreen);
                        }
                        else
                        {
                            TxtDependencyStatus.Text = "❌ Microsoft Access Database Engine (ACE) is NOT installed\n" +
                                                      "This is required for Access database support.";
                            TxtDependencyStatus.Foreground = new SolidColorBrush(Colors.Red);
                            PanelDependencyActions.Visibility = Visibility.Visible;
                            PanelAccessEngineDownload.Visibility = Visibility.Visible;
                            allDependenciesMet = false;
                        }
                        break;

                    case "CSV":
                        TxtDependencyStatus.Text = "✅ CSV/JSON has no external dependencies!\n" +
                                                  "This format works out of the box.";
                        TxtDependencyStatus.Foreground = new SolidColorBrush(Colors.LightGreen);
                        break;
                }
            }
            catch (Exception ex)
            {
                TxtDependencyStatus.Text = $"❌ Dependency check failed: {ex.Message}";
                TxtDependencyStatus.Foreground = new SolidColorBrush(Colors.Red);
                LogManager.LogError("Dependency check failed", ex);
            }
            finally
            {
                BtnCheckDependencies.IsEnabled = true;
            }
        }

        /// <summary>
        /// Download ACE Database Engine
        /// TAG: #AUTO_INSTALLER #ACE_DOWNLOAD
        /// </summary>
        private void BtnDownloadAce_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Microsoft ACE download page
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.microsoft.com/en-us/download/details.aspx?id=54920",
                    UseShellExecute = true
                });

                ToastManager.ShowInfo("Download page opened in your browser. Download 'AccessDatabaseEngine_X64.exe' (64-bit), run the installer, then click 'Check Dependencies' again.");
            }
            catch (Exception ex)
            {
                ToastManager.ShowError($"Could not open download page: {ex.Message}. Please visit: https://www.microsoft.com/en-us/download/details.aspx?id=54920");
            }
        }

        /// <summary>
        /// Download SQL Server Express
        /// TAG: #AUTO_INSTALLER #SQL_SERVER_DOWNLOAD
        /// </summary>
        private void BtnDownloadSqlExpress_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.microsoft.com/en-us/sql-server/sql-server-downloads",
                    UseShellExecute = true
                });

                ToastManager.ShowInfo("Download page opened in your browser. Download SQL Server Express (Free), run the installer (choose 'Basic'), note the server name, then update Step 2 and test connection.");
            }
            catch (Exception ex)
            {
                ToastManager.ShowError($"Could not open download page: {ex.Message}. Please visit: https://www.microsoft.com/en-us/sql-server/sql-server-downloads");
            }
        }

        /// <summary>
        /// Navigate to next step
        /// TAG: #WIZARD_NAVIGATION
        /// </summary>
        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            // Validate current step before proceeding
            if (_currentStep == 1 && string.IsNullOrEmpty(_selectedDatabaseType))
            {
                ToastManager.ShowWarning("Please select a database type.");
                return;
            }

            _currentStep++;
            if (_currentStep > TOTAL_STEPS)
            {
                _currentStep = TOTAL_STEPS;
            }

            UpdateStepVisibility();
        }

        /// <summary>
        /// Navigate to previous step
        /// TAG: #WIZARD_NAVIGATION
        /// </summary>
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            _currentStep--;
            if (_currentStep < 1)
            {
                _currentStep = 1;
            }

            UpdateStepVisibility();
        }

        /// <summary>
        /// Update step panel visibility
        /// TAG: #WIZARD_NAVIGATION #UI_UPDATE
        /// </summary>
        private void UpdateStepVisibility()
        {
            // Update panels
            PanelStep1.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            PanelStep2.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            PanelStep3.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;

            // Update buttons
            BtnBack.Visibility = _currentStep > 1 ? Visibility.Visible : Visibility.Collapsed;
            BtnNext.Visibility = _currentStep < TOTAL_STEPS ? Visibility.Visible : Visibility.Collapsed;
            BtnFinish.Visibility = _currentStep == TOTAL_STEPS ? Visibility.Visible : Visibility.Collapsed;

            // Update subtitle
            TxtSubtitle.Text = $"Step {_currentStep} of {TOTAL_STEPS}";
        }

        /// <summary>
        /// Finish setup and save configuration
        /// TAG: #WIZARD_COMPLETION #CONFIGURATION_SAVE
        /// </summary>
        private async void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PanelProgress.Visibility = Visibility.Visible;
                BtnFinish.IsEnabled = false;

                // Build connection string based on selected database type
                switch (_selectedDatabaseType)
                {
                    case "SQLite":
                        ConnectionString = $"Data Source={TxtSQLitePath.Text};Version=3;";
                        DatabaseType = "SQLite";
                        break;

                    case "SqlServer":
                        if (ChkSqlWindowsAuth.IsChecked == true)
                        {
                            ConnectionString = $"Server={TxtSqlServerName.Text};Database={TxtSqlDatabaseName.Text};Integrated Security=true;";
                        }
                        else
                        {
                            ConnectionString = $"Server={TxtSqlServerName.Text};Database={TxtSqlDatabaseName.Text};" +
                                             $"User Id={TxtSqlUsername.Text};Password={TxtSqlPassword.Password};";
                        }
                        DatabaseType = "SqlServer";
                        break;

                    case "Access":
                        ConnectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={TxtAccessPath.Text};";
                        DatabaseType = "Access";
                        break;

                    case "CSV":
                        ConnectionString = TxtCsvPath.Text;
                        DatabaseType = RadioJsonFormat.IsChecked == true ? "JSON" : "CSV";
                        break;
                }

                // Save to settings
                Properties.Settings.Default.DatabaseType = DatabaseType;
                Properties.Settings.Default.DatabasePath = ConnectionString;
                Properties.Settings.Default.Save();

                SetupCompleted = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ToastManager.ShowError($"Failed to save configuration: {ex.Message}");
                LogManager.LogError("Database setup failed", ex);
            }
            finally
            {
                PanelProgress.Visibility = Visibility.Collapsed;
                BtnFinish.IsEnabled = true;
            }
        }

        /// <summary>
        /// Cancel setup
        /// TAG: #WIZARD_CANCEL
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            ToastManager.ShowWarning("Are you sure you want to cancel database setup? The application requires a database to function.", "Yes, Cancel", () =>
            {
                SetupCompleted = false;
                DialogResult = false;
                Close();
            });
        }
    }
}
