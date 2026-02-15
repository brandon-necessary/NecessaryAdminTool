using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using NecessaryAdminTool.Security;
// TAG: #AUTO_UPDATE_INSTALLER #SQUIRREL #UPDATE_CONTROL #VERSION_1_0 #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION

namespace NecessaryAdminTool
{
    /// <summary>
    /// Manages application updates with granular enterprise control
    /// TAG: #AUTO_UPDATE #ENTERPRISE_CONTROL #REGISTRY_SETTINGS
    /// </summary>
    public static class UpdateManager
    {
        private const string REGISTRY_KEY = @"Software\NecessaryAdminTool\Updates";
        private const string UPDATE_URL = "https://github.com/brandon-necessary/NecessaryAdminTool/releases";

        // Update channels
        public enum UpdateChannel
        {
            Stable,      // Production releases only
            Beta,        // Pre-release builds
            Disabled     // No updates
        }

        /// <summary>
        /// Check if auto-updates are enabled via registry or config
        /// TAG: #UPDATE_CONTROL #ENTERPRISE_POLICY
        /// </summary>
        public static bool AreUpdatesEnabled()
        {
            try
            {
                // Check registry first (set by GPO or installer)
                using (var key = Registry.LocalMachine.OpenSubKey(REGISTRY_KEY, false))
                {
                    if (key != null)
                    {
                        var enabled = key.GetValue("EnableAutoUpdates", 1);
                        if (enabled is int intValue && intValue == 0)
                        {
                            LogManager.LogInfo("Auto-updates disabled via registry (HKLM)");
                            return false;
                        }
                    }
                }

                // Check current user registry (user override)
                using (var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, false))
                {
                    if (key != null)
                    {
                        var enabled = key.GetValue("EnableAutoUpdates", 1);
                        if (enabled is int intValue && intValue == 0)
                        {
                            LogManager.LogInfo("Auto-updates disabled via registry (HKCU)");
                            return false;
                        }
                    }
                }

                // Check app settings
                if (Properties.Settings.Default.DisableAutoUpdates)
                {
                    LogManager.LogInfo("Auto-updates disabled via application settings");
                    return false;
                }

                // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                // Check for marker file (for air-gapped environments)
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string markerFile = Path.Combine(baseDirectory, ".no-updates");
                string fullMarkerPath = Path.GetFullPath(markerFile);

                // Validate marker file is within application directory
                if (SecurityValidator.IsValidFilePath(fullMarkerPath, baseDirectory) && File.Exists(markerFile))
                {
                    LogManager.LogInfo("Auto-updates disabled via marker file (.no-updates)");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to check update settings", ex);
                return true; // Default to enabled if check fails
            }
        }

        /// <summary>
        /// Get update check frequency in hours
        /// TAG: #UPDATE_FREQUENCY
        /// </summary>
        public static int GetUpdateFrequencyHours()
        {
            try
            {
                // Check registry (GPO setting)
                using (var key = Registry.LocalMachine.OpenSubKey(REGISTRY_KEY, false))
                {
                    if (key != null)
                    {
                        var frequency = key.GetValue("CheckFrequencyHours", 24);
                        if (frequency is int intValue)
                        {
                            return intValue;
                        }
                    }
                }

                // Default: Check every 24 hours
                return 24;
            }
            catch
            {
                return 24;
            }
        }

        /// <summary>
        /// Get configured update channel
        /// TAG: #UPDATE_CHANNEL
        /// </summary>
        public static UpdateChannel GetUpdateChannel()
        {
            try
            {
                // Check registry
                using (var key = Registry.LocalMachine.OpenSubKey(REGISTRY_KEY, false))
                {
                    if (key != null)
                    {
                        var channel = key.GetValue("UpdateChannel", "stable") as string;
                        if (Enum.TryParse<UpdateChannel>(channel, true, out var result))
                        {
                            return result;
                        }
                    }
                }

                return UpdateChannel.Stable;
            }
            catch
            {
                return UpdateChannel.Stable;
            }
        }

        /// <summary>
        /// Perform automatic update check (background, silent)
        /// Called automatically on app startup
        /// TAG: #AUTO_UPDATE #AUTOMATIC_CHECK
        /// </summary>
        public static async Task PerformAutomaticUpdateCheckAsync()
        {
            // Check if enough time has elapsed since last check
            if (!ShouldCheckForUpdates())
            {
                LogManager.LogInfo("Update check skipped - frequency throttle active");
                return;
            }

            // Perform silent check
            await CheckForUpdatesAsync(silent: true);

            // Record check time
            RecordUpdateCheck();
        }

        /// <summary>
        /// Check for and apply updates
        /// TAG: #AUTO_UPDATE #SQUIRREL_UPDATE
        /// </summary>
        public static async Task CheckForUpdatesAsync(bool silent = true)
        {
            try
            {
                // Respect update settings
                if (!AreUpdatesEnabled())
                {
                    LogManager.LogInfo("Update check skipped - updates disabled");
                    return;
                }

                var channel = GetUpdateChannel();
                if (channel == UpdateChannel.Disabled)
                {
                    LogManager.LogInfo("Update check skipped - channel is Disabled");
                    return;
                }

                // Check if enough time has elapsed since last check
                if (!ShouldCheckForUpdates())
                {
                    LogManager.LogInfo("Update check skipped - too soon since last check");
                    return;
                }

                LogManager.LogInfo($"Checking for updates (Channel: {channel}, Silent: {silent})");

                #if SQUIRREL_ENABLED
                // Use Squirrel for updates
                using (var manager = new Squirrel.UpdateManager(UPDATE_URL))
                {
                    var updateInfo = await manager.CheckForUpdate();

                    if (updateInfo.ReleasesToApply.Count > 0)
                    {
                        var latestVersion = updateInfo.FutureReleaseEntry.Version;
                        LogManager.LogInfo($"Update available: v{latestVersion}");

                        if (silent)
                        {
                            // Download in background, notify user
                            await manager.DownloadReleases(updateInfo.ReleasesToApply);

                            ShowUpdateNotification(latestVersion.ToString());
                        }
                        else
                        {
                            // Interactive update
                            var result = MessageBox.Show(
                                $"A new version is available!\n\n" +
                                $"Current: v{GetCurrentVersion()}\n" +
                                $"Latest:  v{latestVersion}\n\n" +
                                $"Download and install update?\n" +
                                $"(Application will restart)",
                                "Update Available",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Information);

                            if (result == MessageBoxResult.Yes)
                            {
                                await manager.UpdateApp();

                                MessageBox.Show(
                                    "Update downloaded successfully!\n\n" +
                                    "The application will now restart to apply the update.",
                                    "Update Ready",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);

                                // Restart application
                                Squirrel.UpdateManager.RestartApp();
                            }
                        }
                    }
                    else
                    {
                        LogManager.LogInfo("No updates available");

                        if (!silent)
                        {
                            MessageBox.Show(
                                $"You are running the latest version!\n\nVersion: {GetCurrentVersion()}",
                                "No Updates Available",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    }
                }
                #else
                LogManager.LogWarning("Squirrel not enabled - manual update checking not available");

                if (!silent)
                {
                    var result = MessageBox.Show(
                        "Auto-update system is not configured.\n\n" +
                        "Would you like to visit the download page to check for updates manually?",
                        "Manual Update Required",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "https://github.com/brandon-necessary/NecessaryAdminTool/releases",
                            UseShellExecute = true
                        });
                    }
                }
                #endif

                // Update last check timestamp
                RecordUpdateCheck();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Update check failed", ex);

                if (!silent)
                {
                    MessageBox.Show(
                        $"Failed to check for updates:\n\n{ex.Message}",
                        "Update Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }

        /// <summary>
        /// Check if enough time has elapsed since last update check
        /// TAG: #UPDATE_THROTTLE
        /// </summary>
        private static bool ShouldCheckForUpdates()
        {
            try
            {
                var lastCheckStr = Properties.Settings.Default.LastUpdateCheck;
                if (string.IsNullOrEmpty(lastCheckStr))
                {
                    return true;
                }

                var lastCheck = DateTime.Parse(lastCheckStr);
                var frequency = TimeSpan.FromHours(GetUpdateFrequencyHours());

                if (DateTime.Now - lastCheck < frequency)
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return true; // If check fails, allow update
            }
        }

        /// <summary>
        /// Record timestamp of update check
        /// TAG: #UPDATE_TRACKING
        /// </summary>
        private static void RecordUpdateCheck()
        {
            try
            {
                Properties.Settings.Default.LastUpdateCheck = DateTime.Now.ToString("O");
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to record update check time", ex);
            }
        }

        /// <summary>
        /// Show toast notification for available update
        /// TAG: #UPDATE_NOTIFICATION
        /// </summary>
        private static void ShowUpdateNotification(string version)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"A new version (v{version}) has been downloaded in the background.\n\n" +
                        $"Click 'Help → Check for Updates' to install and restart.",
                        "Update Ready",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to show update notification", ex);
            }
        }

        /// <summary>
        /// Get current application version
        /// TAG: #VERSION_INFO
        /// </summary>
        public static string GetCurrentVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Disable auto-updates (user preference)
        /// TAG: #UPDATE_CONTROL #USER_PREFERENCE
        /// </summary>
        public static void DisableAutoUpdates()
        {
            try
            {
                Properties.Settings.Default.DisableAutoUpdates = true;
                Properties.Settings.Default.Save();
                LogManager.LogInfo("Auto-updates disabled by user");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to disable auto-updates", ex);
            }
        }

        /// <summary>
        /// Enable auto-updates (user preference)
        /// TAG: #UPDATE_CONTROL #USER_PREFERENCE
        /// </summary>
        public static void EnableAutoUpdates()
        {
            try
            {
                Properties.Settings.Default.DisableAutoUpdates = false;
                Properties.Settings.Default.Save();
                LogManager.LogInfo("Auto-updates enabled by user");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to enable auto-updates", ex);
            }
        }

        /// <summary>
        /// Create marker file to disable updates (for deployment)
        /// TAG: #DEPLOYMENT #AIR_GAPPED #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
        /// </summary>
        public static void CreateNoUpdateMarker()
        {
            try
            {
                // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                // Validate marker file path
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string markerFile = Path.Combine(baseDirectory, ".no-updates");
                string fullMarkerPath = Path.GetFullPath(markerFile);

                // Validate filename
                if (!SecurityValidator.IsValidFilename(".no-updates"))
                {
                    LogManager.LogWarning("CreateNoUpdateMarker - invalid filename");
                    return;
                }

                // Ensure path is within application directory
                if (!SecurityValidator.IsValidFilePath(fullMarkerPath, baseDirectory))
                {
                    LogManager.LogWarning("CreateNoUpdateMarker - path traversal attempt blocked");
                    return;
                }

                File.WriteAllText(markerFile, $"Auto-updates disabled at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                LogManager.LogInfo("Created .no-updates marker file");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to create .no-updates marker", ex);
            }
        }

        /// <summary>
        /// Remove marker file to re-enable updates
        /// TAG: #DEPLOYMENT #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
        /// </summary>
        public static void RemoveNoUpdateMarker()
        {
            try
            {
                // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                // Validate marker file path
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string markerFile = Path.Combine(baseDirectory, ".no-updates");
                string fullMarkerPath = Path.GetFullPath(markerFile);

                // Ensure path is within application directory
                if (!SecurityValidator.IsValidFilePath(fullMarkerPath, baseDirectory))
                {
                    LogManager.LogWarning("RemoveNoUpdateMarker - path traversal attempt blocked");
                    return;
                }

                if (File.Exists(markerFile))
                {
                    File.Delete(markerFile);
                    LogManager.LogInfo("Removed .no-updates marker file");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to remove .no-updates marker", ex);
            }
        }

        /// <summary>
        /// Handle Squirrel lifecycle events
        /// TAG: #SQUIRREL_EVENTS
        /// </summary>
        public static void HandleSquirrelEvents()
        {
            try
            {
                #if SQUIRREL_ENABLED
                Squirrel.SquirrelAwareApp.HandleEvents(
                    onInitialInstall: v => OnAppInstall(v),
                    onAppUpdate: v => OnAppUpdate(v),
                    onAppUninstall: v => OnAppUninstall(v),
                    onFirstRun: () => OnFirstRun()
                );
                #endif
            }
            catch (Exception ex)
            {
                LogManager.LogError("Squirrel event handling failed", ex);
            }
        }

        private static void OnAppInstall(Version version)
        {
            LogManager.LogInfo($"NecessaryAdminTool v{version} installed");
        }

        private static void OnAppUpdate(Version version)
        {
            LogManager.LogInfo($"NecessaryAdminTool updated to v{version}");
        }

        private static void OnAppUninstall(Version version)
        {
            LogManager.LogInfo($"NecessaryAdminTool v{version} uninstalled");
        }

        private static void OnFirstRun()
        {
            LogManager.LogInfo("First run detected");
        }
    }
}
