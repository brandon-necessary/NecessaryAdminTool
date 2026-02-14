using System;
using System.Windows;
using System.Windows.Forms;
// TAG: #SETUP_WIZARD #VERSION_1_2

namespace NecessaryAdminTool
{
    /// <summary>
    /// Setup Wizard for first-run configuration
    /// TAG: #FIRST_RUN #DATABASE_CONFIG
    /// </summary>
    public partial class SetupWizardWindow : Window
    {
        public string SelectedDatabaseType { get; private set; }
        public string DatabasePath { get; private set; }
        public bool InstallService { get; private set; }
        public int ScanIntervalHours { get; private set; }

        public SetupWizardWindow()
        {
            InitializeComponent();
            LogManager.LogInfo("Setup Wizard opened");
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
                if (InstallService && ScanIntervalHours > 0)
                {
                    try
                    {
                        bool isAdmin = ScheduledTaskManager.IsAdministrator();
                        bool taskCreated = ScheduledTaskManager.CreateTask(ScanIntervalHours, runAsAdmin: isAdmin);

                        if (taskCreated)
                        {
                            LogManager.LogInfo($"Scheduled task created successfully for {ScanIntervalHours}h interval");
                            System.Windows.MessageBox.Show(
                                $"Scheduled task created successfully!\n\n" +
                                $"NecessaryAdminTool will automatically scan every {ScanIntervalHours} hour(s).\n\n" +
                                $"Running as: {(isAdmin ? "Administrator" : "Current User")}",
                                "Service Configured",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        else
                        {
                            LogManager.LogWarning("Failed to create scheduled task during setup");
                            System.Windows.MessageBox.Show(
                                "Failed to create scheduled task.\n\n" +
                                "You can manually configure it later from Options > Database Management.",
                                "Service Warning",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception taskEx)
                    {
                        LogManager.LogError("Exception creating scheduled task during setup", taskEx);
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
    }
}
