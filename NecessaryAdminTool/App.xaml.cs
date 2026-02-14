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

            // Check if first-run setup is needed
            if (!Properties.Settings.Default.SetupCompleted)
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
    }
}
