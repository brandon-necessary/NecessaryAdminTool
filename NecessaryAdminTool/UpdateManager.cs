using System;
using System.Threading.Tasks;
using System.Windows;
using System.Reflection;
// TAG: #AUTO_UPDATE #SQUIRREL

namespace NecessaryAdminTool
{
    /// <summary>
    /// Manages application updates using Squirrel.Windows
    /// TAG: #AUTO_UPDATE #VERSION_1_1
    /// </summary>
    public static class UpdateManager
    {
        private const string GitHubRepoUrl = "https://github.com/brandon-necessary/NecessaryAdminTool";

        /// <summary>
        /// Check for updates and prompt user to install
        /// </summary>
        /// <param name="silent">If true, only show UI if update is available</param>
        /// <returns>True if update was installed</returns>
        public static async Task<bool> CheckForUpdatesAsync(bool silent = false)
        {
            try
            {
                // NOTE: Squirrel.Windows package must be installed via NuGet first
                // Install-Package Squirrel.Windows

                #if SQUIRREL_ENABLED
                using (var mgr = new Squirrel.UpdateManager(GitHubRepoUrl))
                {
                    var updateInfo = await mgr.CheckForUpdate();

                    if (updateInfo.ReleasesToApply.Count > 0)
                    {
                        var newVersion = updateInfo.FutureReleaseEntry.Version;
                        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

                        var result = MessageBox.Show(
                            $"New version available: v{newVersion}\n" +
                            $"Current version: v{currentVersion}\n\n" +
                            $"Download and install now?\n\n" +
                            $"The application will restart after the update.",
                            "Update Available - NecessaryAdminTool",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            // Download and apply update
                            await mgr.UpdateApp();

                            MessageBox.Show(
                                "Update installed successfully!\n\n" +
                                "Please restart the application to use the new version.",
                                "Update Complete",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                            // Log the update
                            LogManager.LogInfo($"Updated from v{currentVersion} to v{newVersion}");

                            return true;
                        }
                    }
                    else if (!silent)
                    {
                        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                        MessageBox.Show(
                            $"You are running the latest version!\n\n" +
                            $"Current version: v{currentVersion}",
                            "No Updates Available",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                #else
                // Squirrel not yet installed - show placeholder
                if (!silent)
                {
                    var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                    MessageBox.Show(
                        $"Auto-update system is being configured.\n\n" +
                        $"Current version: v{currentVersion}\n\n" +
                        $"To enable auto-updates:\n" +
                        $"1. Install Squirrel.Windows NuGet package\n" +
                        $"2. Rebuild the solution\n" +
                        $"3. Define SQUIRREL_ENABLED in project properties",
                        "Update System - Configuration Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                #endif

                return false;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Update check failed", ex);

                if (!silent)
                {
                    MessageBox.Show(
                        $"Failed to check for updates:\n\n{ex.Message}\n\n" +
                        $"Please check your internet connection and try again.",
                        "Update Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                return false;
            }
        }

        /// <summary>
        /// Check if this is the first run after an update
        /// </summary>
        public static void HandleSquirrelEvents()
        {
            try
            {
                #if SQUIRREL_ENABLED
                using (var mgr = new Squirrel.UpdateManager(GitHubRepoUrl))
                {
                    Squirrel.SquirrelAwareApp.HandleEvents(
                        onInitialInstall: v => OnAppInstall(v),
                        onAppUpdate: v => OnAppUpdate(v),
                        onAppUninstall: v => OnAppUninstall(v),
                        onFirstRun: () => OnFirstRun()
                    );
                }
                #endif
            }
            catch (Exception ex)
            {
                LogManager.LogError("Squirrel event handling failed", ex);
            }
        }

        /// <summary>
        /// Called on initial installation
        /// </summary>
        private static void OnAppInstall(Version version)
        {
            LogManager.LogInfo($"NecessaryAdminTool v{version} installed");
            // Create desktop shortcut, start menu entry, etc.
        }

        /// <summary>
        /// Called when app is updated
        /// </summary>
        private static void OnAppUpdate(Version version)
        {
            LogManager.LogInfo($"NecessaryAdminTool updated to v{version}");
            // Update shortcuts, registry entries, etc.
        }

        /// <summary>
        /// Called when app is uninstalled
        /// </summary>
        private static void OnAppUninstall(Version version)
        {
            LogManager.LogInfo($"NecessaryAdminTool v{version} uninstalled");
            // Clean up settings, shortcuts, etc.
        }

        /// <summary>
        /// Called on first run after install/update
        /// </summary>
        private static void OnFirstRun()
        {
            LogManager.LogInfo("First run detected");
        }

        /// <summary>
        /// Get the last time update check was performed
        /// </summary>
        public static DateTime GetLastUpdateCheck()
        {
            try
            {
                var lastCheck = Properties.Settings.Default.LastUpdateCheck;
                if (string.IsNullOrEmpty(lastCheck))
                    return DateTime.MinValue;

                return DateTime.Parse(lastCheck);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Save the last update check timestamp
        /// </summary>
        public static void SaveLastUpdateCheck()
        {
            try
            {
                Properties.Settings.Default.LastUpdateCheck = DateTime.Now.ToString("O");
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to save last update check time", ex);
            }
        }

        /// <summary>
        /// Check if it's time for an automatic update check (weekly)
        /// </summary>
        public static bool ShouldCheckForUpdates()
        {
            var lastCheck = GetLastUpdateCheck();
            var daysSinceLastCheck = (DateTime.Now - lastCheck).TotalDays;

            // Check weekly (7 days)
            return daysSinceLastCheck >= 7;
        }

        /// <summary>
        /// Perform automatic update check (called on startup)
        /// </summary>
        public static async Task PerformAutomaticUpdateCheckAsync()
        {
            try
            {
                if (ShouldCheckForUpdates())
                {
                    LogManager.LogInfo("Performing automatic weekly update check");
                    await CheckForUpdatesAsync(silent: true);
                    SaveLastUpdateCheck();
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Automatic update check failed", ex);
            }
        }
    }
}
