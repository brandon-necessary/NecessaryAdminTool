using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace NecessaryAdminTool
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// TAG: #APPLICATION_STARTUP #FIRST_RUN_SETUP
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check for command-line arguments
            if (e.Args.Length > 0)
            {
                foreach (var arg in e.Args)
                {
                    if (arg.Equals("/autoscan", StringComparison.OrdinalIgnoreCase))
                    {
                        // Run automatic scan in background (scheduled task mode)
                        LogManager.LogInfo("Auto-scan triggered by scheduled task");
                        RunAutomaticScan();
                        Shutdown(0);
                        return;
                    }
                }
            }

            // Check if first-run setup is needed
            if (!NecessaryAdminTool.Properties.Settings.Default.SetupCompleted)
            {
                LogManager.LogInfo("First run detected - launching Setup Wizard");

                var setupWizard = new SetupWizardWindow();
                var result = setupWizard.ShowDialog();

                if (result != true)
                {
                    // User cancelled setup - exit application
                    LogManager.LogWarning("Setup wizard cancelled by user - exiting application");
                    Shutdown();
                    return;
                }

                LogManager.LogInfo("Setup wizard completed successfully");
            }

            // Continue with normal application startup
            // MainWindow will be shown automatically via StartupUri in App.xaml
        }

        /// <summary>
        /// Run automatic scan in background (called by scheduled task)
        /// </summary>
        private async void RunAutomaticScan()
        {
            try
            {
                LogManager.LogInfo("=== AUTOMATIC SCAN STARTED ===");

                // Initialize database provider
                using (var provider = await Data.DataProviderFactory.CreateProviderAsync())
                {
                    // TODO: Implement automatic scanning logic here
                    // This would typically:
                    // 1. Query Active Directory for all computers
                    // 2. Ping/WMI query each computer for status
                    // 3. Update database with results
                    // 4. Log scan statistics

                    // For now, just log that we would scan
                    LogManager.LogInfo("Auto-scan completed (scanning logic not yet implemented)");

                    // Save scan history
                    var scanHistory = new Data.ScanHistory
                    {
                        StartTime = DateTime.Now,
                        EndTime = DateTime.Now,
                        ComputersScanned = 0,
                        SuccessCount = 0,
                        FailureCount = 0,
                        DurationSeconds = 0
                    };

                    // await provider.SaveScanHistoryAsync(scanHistory);
                }

                LogManager.LogInfo("=== AUTOMATIC SCAN COMPLETED ===");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Automatic scan failed", ex);
            }
        }
    }
}
