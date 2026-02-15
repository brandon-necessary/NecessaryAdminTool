using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using NecessaryAdminTool.Properties;
using NecessaryAdminTool.Security;

namespace NecessaryAdminTool
{
    // TAG: #SETTINGS #SETTINGS_MANAGER #CENTRALIZED_CONFIG #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
    /// <summary>
    /// Centralized settings management for NecessaryAdminTool Suite
    /// Handles loading, saving, validation, export/import, and reset operations
    /// </summary>
    public static class SettingsManager
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NecessaryAdminTool"
        );

        private static readonly string SecureConfigPath = Path.Combine(AppDataPath, "SecureConfig.xml");
        private static readonly string UserConfigPath = Path.Combine(AppDataPath, "UserConfig.xml");
        private static readonly string DCManagerPath = Path.Combine(AppDataPath, "DCManager.xml");

        // TAG: #LOAD_SETTINGS #LOGGING
        /// <summary>
        /// Load all settings from various sources into unified structure
        /// </summary>
        public static AppSettings LoadAllSettings()
        {
            LogManager.LogInfo("SettingsManager.LoadAllSettings() - START - Loading all application settings");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var settings = new AppSettings
                {
                    General = LoadGeneralSettings(),
                    Performance = LoadPerformanceSettings(),
                    Paths = LoadPathSettings(),
                    Logging = LoadLoggingSettings(),
                    GlobalServices = LoadGlobalServices(),
                    PinnedDevices = LoadPinnedDevices(),
                    Appearance = LoadAppearanceSettings(),
                    ToastNotifications = LoadToastNotificationSettings(), // TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG
                    KeyboardShortcuts = LoadKeyboardShortcutSettings() // TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG
                };

                LogManager.LogInfo($"SettingsManager.LoadAllSettings() - SUCCESS - All settings loaded - Elapsed: {sw.ElapsedMilliseconds}ms");
                return settings;
            }
            catch (Exception ex)
            {
                LogManager.LogError("SettingsManager.LoadAllSettings() - FAILED - Error loading settings, returning defaults", ex);
                MessageBox.Show($"Error loading settings: {ex.Message}", "Settings Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return GetDefaultSettings();
            }
        }

        // TAG: #GENERAL_SETTINGS
        private static GeneralSettings LoadGeneralSettings()
        {
            return new GeneralSettings
            {
                LastUser = Settings.Default.LastUser ?? string.Empty,
                RememberDC = true, // Default to true
                TargetHistory = LoadTargetHistory(),
                AutoRefreshInterval = 300 // 5 minutes default
            };
        }

        // TAG: #PERFORMANCE_SETTINGS
        private static PerformanceSettings LoadPerformanceSettings()
        {
            return new PerformanceSettings
            {
                MaxParallelScans = 30,
                WmiTimeout = 15000,
                PingTimeout = 1200,
                MaxRetryAttempts = 3,
                ThreadPoolSize = Environment.ProcessorCount * 2
            };
        }

        // TAG: #PATH_SETTINGS
        private static PathSettings LoadPathSettings()
        {
            return new PathSettings
            {
                SharedLogPath = Path.Combine(AppDataPath, "Logs"),
                InventoryDBPath = Path.Combine(AppDataPath, "Inventory.db")
            };
        }

        // TAG: #LOGGING_SETTINGS
        private static LoggingSettings LoadLoggingSettings()
        {
            return new LoggingSettings
            {
                LogLevel = "Info",
                LogRetentionDays = 30,
                CurrentLogSize = GetLogSize()
            };
        }

        // TAG: #GLOBAL_SERVICES
        private static List<GlobalServiceStatus> LoadGlobalServices()
        {
            var services = new List<GlobalServiceStatus>();

            try
            {
                string configJson = Settings.Default.GlobalServicesConfig;

                if (!string.IsNullOrWhiteSpace(configJson))
                {
                    var serializer = new JavaScriptSerializer();
                    var serviceList = serializer.Deserialize<List<Dictionary<string, object>>>(configJson);

                    foreach (var svc in serviceList)
                    {
                        services.Add(new GlobalServiceStatus
                        {
                            ServiceName = svc["ServiceName"].ToString(),
                            Endpoint = svc["Endpoint"].ToString(),
                            Status = "Checking..."
                        });
                    }
                }
                else
                {
                    services = GetDefaultGlobalServices();
                }
            }
            catch
            {
                services = GetDefaultGlobalServices();
            }

            return services;
        }

        // TAG: #PINNED_DEVICES
        private static List<PinnedDevice> LoadPinnedDevices()
        {
            var devices = new List<PinnedDevice>();

            try
            {
                string pinnedJson = Settings.Default.PinnedDevices;

                if (!string.IsNullOrWhiteSpace(pinnedJson))
                {
                    var serializer = new JavaScriptSerializer();
                    var deviceList = serializer.Deserialize<List<Dictionary<string, object>>>(pinnedJson);

                    foreach (var dev in deviceList)
                    {
                        devices.Add(new PinnedDevice
                        {
                            Input = dev.ContainsKey("Input") ? dev["Input"].ToString() : string.Empty,
                            Status = dev.ContainsKey("Status") ? dev["Status"].ToString() : "Unknown",
                            LastChecked = dev.ContainsKey("LastChecked") ? dev["LastChecked"].ToString() : string.Empty
                        });
                    }
                }
            }
            catch
            {
                // Return empty list on error
            }

            return devices;
        }

        // TAG: #APPEARANCE_SETTINGS
        private static AppearanceSettings LoadAppearanceSettings()
        {
            return new AppearanceSettings
            {
                LogoPath = string.Empty, // No custom logo by default
                PrimaryColor = "#0078D4", // Microsoft Blue
                SecondaryColor = "#00BCF2"  // Light Blue
            };
        }

        // TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
        /// <summary>
        /// Load toast notification settings from config
        /// </summary>
        private static ToastNotificationSettings LoadToastNotificationSettings()
        {
            try
            {
                string configJson = Settings.Default.ToastNotificationSettings;
                if (!string.IsNullOrWhiteSpace(configJson))
                {
                    var serializer = new JavaScriptSerializer();
                    return serializer.Deserialize<ToastNotificationSettings>(configJson);
                }
            }
            catch
            {
                LogManager.LogWarning("Failed to load toast notification settings, using defaults");
            }

            return new ToastNotificationSettings(); // Return defaults
        }

        // TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
        /// <summary>
        /// Load keyboard shortcut settings from config
        /// </summary>
        private static KeyboardShortcutSettings LoadKeyboardShortcutSettings()
        {
            try
            {
                string configJson = Settings.Default.KeyboardShortcutSettings;
                if (!string.IsNullOrWhiteSpace(configJson))
                {
                    var serializer = new JavaScriptSerializer();
                    return serializer.Deserialize<KeyboardShortcutSettings>(configJson);
                }
            }
            catch
            {
                LogManager.LogWarning("Failed to load keyboard shortcut settings, using defaults");
            }

            return new KeyboardShortcutSettings(); // Return defaults
        }

        // TAG: #SAVE_SETTINGS #LOGGING
        /// <summary>
        /// Save specific settings category
        /// </summary>
        public static void SaveGeneralSettings(GeneralSettings settings)
        {
            LogManager.LogInfo($"SettingsManager.SaveGeneralSettings() - Saving general settings - LastUser: {settings.LastUser}");

            try
            {
                Settings.Default.LastUser = settings.LastUser;
                SaveTargetHistory(settings.TargetHistory);
                Settings.Default.Save();

                LogManager.LogInfo("SettingsManager.SaveGeneralSettings() - SUCCESS - General settings saved");
            }
            catch (Exception ex)
            {
                LogManager.LogError("SettingsManager.SaveGeneralSettings() - FAILED", ex);
                throw;
            }
        }

        public static void SavePerformanceSettings(PerformanceSettings settings)
        {
            // Performance settings would be saved to SecureConfig.xml or custom config
            // For now, they're stored in-memory during runtime
        }

        public static void SavePathSettings(PathSettings settings)
        {
            // Path settings would be saved to SecureConfig.xml
            EnsureDirectoryExists(settings.SharedLogPath);
        }

        public static void SaveLoggingSettings(LoggingSettings settings)
        {
            // Logging settings would be saved to custom config
        }

        public static void SaveGlobalServices(List<GlobalServiceStatus> services)
        {
            LogManager.LogInfo($"SettingsManager.SaveGlobalServices() - Saving {services.Count} global service configurations");

            try
            {
                var serializer = new JavaScriptSerializer();
                var serviceList = services.Select(s => new Dictionary<string, object>
                {
                    { "ServiceName", s.ServiceName },
                    { "Endpoint", s.Endpoint }
                }).ToList();

                string json = serializer.Serialize(serviceList);
                Settings.Default.GlobalServicesConfig = json;
                Settings.Default.Save();

                LogManager.LogInfo($"SettingsManager.SaveGlobalServices() - SUCCESS - Saved {services.Count} services");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"SettingsManager.SaveGlobalServices() - FAILED - Service count: {services.Count}", ex);
                throw new Exception($"Failed to save global services: {ex.Message}");
            }
        }

        public static void SavePinnedDevices(List<PinnedDevice> devices)
        {
            LogManager.LogInfo($"SettingsManager.SavePinnedDevices() - Saving {devices.Count} pinned devices");

            try
            {
                var serializer = new JavaScriptSerializer();
                var deviceList = devices.Select(d => new Dictionary<string, object>
                {
                    { "Input", d.Input },
                    { "Status", d.Status },
                    { "LastChecked", d.LastChecked }
                }).ToList();

                string json = serializer.Serialize(deviceList);
                Settings.Default.PinnedDevices = json;
                Settings.Default.Save();

                LogManager.LogInfo($"SettingsManager.SavePinnedDevices() - SUCCESS - Saved {devices.Count} devices");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"SettingsManager.SavePinnedDevices() - FAILED - Device count: {devices.Count}", ex);
                throw new Exception($"Failed to save pinned devices: {ex.Message}");
            }
        }

        public static void SaveAppearanceSettings(AppearanceSettings settings)
        {
            // Appearance settings could be saved to custom config or registry
            // For now, stored in-memory during runtime
        }

        // TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
        /// <summary>
        /// Save toast notification settings
        /// </summary>
        public static void SaveToastNotificationSettings(ToastNotificationSettings settings)
        {
            LogManager.LogInfo($"SettingsManager.SaveToastNotificationSettings() - Saving toast notification preferences - EnableToasts: {settings.EnableToasts}");

            try
            {
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(settings);
                Settings.Default.ToastNotificationSettings = json;
                Settings.Default.Save();

                LogManager.LogInfo("SettingsManager.SaveToastNotificationSettings() - SUCCESS - Toast notification settings saved");
            }
            catch (Exception ex)
            {
                LogManager.LogError("SettingsManager.SaveToastNotificationSettings() - FAILED", ex);
                throw new Exception($"Failed to save toast notification settings: {ex.Message}");
            }
        }

        // TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
        /// <summary>
        /// Save keyboard shortcut settings
        /// </summary>
        public static void SaveKeyboardShortcutSettings(KeyboardShortcutSettings settings)
        {
            LogManager.LogInfo($"SettingsManager.SaveKeyboardShortcutSettings() - Saving {settings.Shortcuts.Count} keyboard shortcuts");

            try
            {
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(settings);
                Settings.Default.KeyboardShortcutSettings = json;
                Settings.Default.Save();

                LogManager.LogInfo("SettingsManager.SaveKeyboardShortcutSettings() - SUCCESS - Keyboard shortcuts saved");
            }
            catch (Exception ex)
            {
                LogManager.LogError("SettingsManager.SaveKeyboardShortcutSettings() - FAILED", ex);
                throw new Exception($"Failed to save keyboard shortcut settings: {ex.Message}");
            }
        }

        // TAG: #RESET_SETTINGS
        /// <summary>
        /// Reset specific settings category to defaults
        /// </summary>
        public static void ResetGeneralSettings()
        {
            Settings.Default.LastUser = string.Empty;
            Settings.Default.Save();
        }

        public static void ResetPerformanceSettings()
        {
            // Reset to defaults (handled in LoadPerformanceSettings)
        }

        public static void ResetPathSettings()
        {
            // Reset to default paths
        }

        public static void ResetGlobalServices()
        {
            var defaults = GetDefaultGlobalServices();
            SaveGlobalServices(defaults);
        }

        public static void ResetPinnedDevices()
        {
            Settings.Default.PinnedDevices = string.Empty;
            Settings.Default.Save();
        }

        // TAG: #RESET_ALL #LOGGING
        /// <summary>
        /// Factory reset - clear all settings and restore defaults
        /// </summary>
        public static void ResetAllSettings()
        {
            LogManager.LogWarning("SettingsManager.ResetAllSettings() - START - Factory reset initiated - All settings will be cleared");

            try
            {
                // Clear all Properties.Settings
                Settings.Default.LastUser = string.Empty;
                Settings.Default.PinnedDevices = string.Empty;
                Settings.Default.GlobalServicesConfig = string.Empty;
                Settings.Default.Save();

                // Delete XML config files (optional - recreated on next launch)
                DeleteConfigFiles();

                LogManager.LogWarning("SettingsManager.ResetAllSettings() - SUCCESS - All settings cleared, defaults restored");
            }
            catch (Exception ex)
            {
                LogManager.LogError("SettingsManager.ResetAllSettings() - FAILED", ex);
                throw new Exception($"Failed to reset all settings: {ex.Message}");
            }
        }

        // TAG: #EXPORT_IMPORT #LOGGING #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
        /// <summary>
        /// Export all settings to JSON file
        /// </summary>
        public static void ExportToFile(string filePath)
        {
            LogManager.LogInfo($"SettingsManager.ExportToFile() - START - Exporting settings to: {filePath}");

            try
            {
                // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                // Validate file path and filename
                string filename = Path.GetFileName(filePath);
                if (!SecurityValidator.IsValidFilename(filename))
                {
                    LogManager.LogWarning($"SettingsManager.ExportToFile() - BLOCKED - Invalid filename: {filename}");
                    throw new ArgumentException("Invalid filename detected");
                }

                // Ensure the file has a safe extension
                string extension = Path.GetExtension(filePath);
                if (!extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    LogManager.LogWarning($"SettingsManager.ExportToFile() - BLOCKED - Invalid file extension: {extension}");
                    throw new ArgumentException("File must have .json extension");
                }

                // Validate parent directory exists
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    LogManager.LogWarning($"SettingsManager.ExportToFile() - BLOCKED - Directory does not exist: {directory}");
                    throw new DirectoryNotFoundException($"Directory does not exist: {directory}");
                }

                var settings = LoadAllSettings();
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(settings);

                File.WriteAllText(filePath, json);

                var fileInfo = new FileInfo(filePath);
                LogManager.LogInfo($"SettingsManager.ExportToFile() - SUCCESS - Settings exported - Size: {fileInfo.Length} bytes - Path: {filePath}");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"SettingsManager.ExportToFile() - FAILED - Path: {filePath}", ex);
                throw new Exception($"Failed to export settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Import settings from JSON file
        /// </summary>
        public static void ImportFromFile(string filePath)
        {
            LogManager.LogInfo($"SettingsManager.ImportFromFile() - START - Importing settings from: {filePath}");

            try
            {
                // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                // Validate file path and filename
                string filename = Path.GetFileName(filePath);
                if (!SecurityValidator.IsValidFilename(filename))
                {
                    LogManager.LogWarning($"SettingsManager.ImportFromFile() - BLOCKED - Invalid filename: {filename}");
                    throw new ArgumentException("Invalid filename detected");
                }

                // Ensure the file has a safe extension
                string extension = Path.GetExtension(filePath);
                if (!extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    LogManager.LogWarning($"SettingsManager.ImportFromFile() - BLOCKED - Invalid file extension: {extension}");
                    throw new ArgumentException("File must have .json extension");
                }

                if (!File.Exists(filePath))
                {
                    LogManager.LogError($"SettingsManager.ImportFromFile() - FAILED - File not found: {filePath}");
                    throw new FileNotFoundException("Settings file not found.");
                }

                var fileInfo = new FileInfo(filePath);

                // TAG: #SECURITY_CRITICAL #FILE_SIZE_VALIDATION
                // Prevent reading excessively large files (DoS protection)
                const long MAX_FILE_SIZE = 10 * 1024 * 1024; // 10 MB
                if (fileInfo.Length > MAX_FILE_SIZE)
                {
                    LogManager.LogWarning($"SettingsManager.ImportFromFile() - BLOCKED - File too large: {fileInfo.Length} bytes");
                    throw new ArgumentException("Settings file exceeds maximum allowed size (10 MB)");
                }

                LogManager.LogInfo($"SettingsManager.ImportFromFile() - Reading file - Size: {fileInfo.Length} bytes");

                string json = File.ReadAllText(filePath);
                var serializer = new JavaScriptSerializer();
                var settings = serializer.Deserialize<AppSettings>(json);

                int categoriesImported = 0;

                // Apply imported settings
                if (settings.General != null)
                {
                    SaveGeneralSettings(settings.General);
                    categoriesImported++;
                    LogManager.LogInfo("SettingsManager.ImportFromFile() - General settings imported");
                }

                if (settings.GlobalServices != null)
                {
                    SaveGlobalServices(settings.GlobalServices);
                    categoriesImported++;
                    LogManager.LogInfo($"SettingsManager.ImportFromFile() - Global services imported ({settings.GlobalServices.Count} services)");
                }

                if (settings.PinnedDevices != null)
                {
                    SavePinnedDevices(settings.PinnedDevices);
                    categoriesImported++;
                    LogManager.LogInfo($"SettingsManager.ImportFromFile() - Pinned devices imported ({settings.PinnedDevices.Count} devices)");
                }

                LogManager.LogInfo($"SettingsManager.ImportFromFile() - SUCCESS - {categoriesImported} categories imported from: {filePath}");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"SettingsManager.ImportFromFile() - FAILED - Path: {filePath}", ex);
                throw new Exception($"Failed to import settings: {ex.Message}");
            }
        }

        // TAG: #VALIDATION #LOGGING
        /// <summary>
        /// Validate path settings
        /// </summary>
        public static bool ValidatePaths(PathSettings settings)
        {
            LogManager.LogInfo($"SettingsManager.ValidatePaths() - Validating paths - SharedLogPath: {settings.SharedLogPath}, InventoryDBPath: {settings.InventoryDBPath}");

            try
            {
                // Check if parent directories exist or can be created
                if (!string.IsNullOrEmpty(settings.SharedLogPath))
                {
                    string parentDir = Path.GetDirectoryName(settings.SharedLogPath);
                    if (!Directory.Exists(parentDir))
                    {
                        LogManager.LogWarning($"SettingsManager.ValidatePaths() - INVALID - SharedLogPath parent directory does not exist: {parentDir}");
                        return false;
                    }
                }

                if (!string.IsNullOrEmpty(settings.InventoryDBPath))
                {
                    string parentDir = Path.GetDirectoryName(settings.InventoryDBPath);
                    if (!Directory.Exists(parentDir))
                    {
                        LogManager.LogWarning($"SettingsManager.ValidatePaths() - INVALID - InventoryDBPath parent directory does not exist: {parentDir}");
                        return false;
                    }
                }

                LogManager.LogInfo("SettingsManager.ValidatePaths() - VALID - All paths validated successfully");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError("SettingsManager.ValidatePaths() - FAILED - Exception during validation", ex);
                return false;
            }
        }

        /// <summary>
        /// Validate performance settings
        /// </summary>
        public static bool ValidatePerformance(PerformanceSettings settings)
        {
            LogManager.LogInfo($"SettingsManager.ValidatePerformance() - Validating performance settings - MaxParallelScans: {settings.MaxParallelScans}, WmiTimeout: {settings.WmiTimeout}ms, PingTimeout: {settings.PingTimeout}ms, MaxRetryAttempts: {settings.MaxRetryAttempts}, ThreadPoolSize: {settings.ThreadPoolSize}");

            bool isValid = settings.MaxParallelScans >= 1 && settings.MaxParallelScans <= 100 &&
                   settings.WmiTimeout >= 1000 && settings.WmiTimeout <= 60000 &&
                   settings.PingTimeout >= 100 && settings.PingTimeout <= 10000 &&
                   settings.MaxRetryAttempts >= 1 && settings.MaxRetryAttempts <= 5 &&
                   settings.ThreadPoolSize >= 1;

            if (isValid)
            {
                LogManager.LogInfo("SettingsManager.ValidatePerformance() - VALID - All performance settings within acceptable ranges");
            }
            else
            {
                LogManager.LogWarning("SettingsManager.ValidatePerformance() - INVALID - One or more performance settings out of range");
            }

            return isValid;
        }

        // TAG: #HELPERS
        private static List<string> LoadTargetHistory()
        {
            // Load from UserConfig.xml or custom storage
            return new List<string>();
        }

        private static void SaveTargetHistory(List<string> history)
        {
            // Save to UserConfig.xml or custom storage
        }

        private static long GetLogSize()
        {
            try
            {
                string logPath = Path.Combine(AppDataPath, "Logs");
                if (Directory.Exists(logPath))
                {
                    var files = Directory.GetFiles(logPath, "*.log");
                    return files.Sum(f => new FileInfo(f).Length);
                }
            }
            catch { }

            return 0;
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static void DeleteConfigFiles()
        {
            try
            {
                // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                // Validate all config file paths before deletion
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string allowedBasePath = Path.Combine(appDataPath, "NecessaryAdminTool");

                if (File.Exists(SecureConfigPath))
                {
                    string fullPath = Path.GetFullPath(SecureConfigPath);
                    if (SecurityValidator.IsValidFilePath(fullPath, allowedBasePath))
                    {
                        File.Delete(SecureConfigPath);
                    }
                    else
                    {
                        LogManager.LogWarning($"[SettingsManager] Blocked deletion of SecureConfigPath outside allowed directory: {SecureConfigPath}");
                    }
                }

                if (File.Exists(UserConfigPath))
                {
                    string fullPath = Path.GetFullPath(UserConfigPath);
                    if (SecurityValidator.IsValidFilePath(fullPath, allowedBasePath))
                    {
                        File.Delete(UserConfigPath);
                    }
                    else
                    {
                        LogManager.LogWarning($"[SettingsManager] Blocked deletion of UserConfigPath outside allowed directory: {UserConfigPath}");
                    }
                }

                if (File.Exists(DCManagerPath))
                {
                    string fullPath = Path.GetFullPath(DCManagerPath);
                    if (SecurityValidator.IsValidFilePath(fullPath, allowedBasePath))
                    {
                        File.Delete(DCManagerPath);
                    }
                    else
                    {
                        LogManager.LogWarning($"[SettingsManager] Blocked deletion of DCManagerPath outside allowed directory: {DCManagerPath}");
                    }
                }
            }
            catch
            {
                // Ignore errors - files will be recreated
            }
        }

        // TAG: #DEFAULTS
        private static AppSettings GetDefaultSettings()
        {
            return new AppSettings
            {
                General = new GeneralSettings
                {
                    LastUser = string.Empty,
                    RememberDC = true,
                    TargetHistory = new List<string>(),
                    AutoRefreshInterval = 300
                },
                Performance = new PerformanceSettings
                {
                    MaxParallelScans = 30,
                    WmiTimeout = 15000,
                    PingTimeout = 1200,
                    MaxRetryAttempts = 3,
                    ThreadPoolSize = Environment.ProcessorCount * 2
                },
                Paths = new PathSettings
                {
                    SharedLogPath = Path.Combine(AppDataPath, "Logs"),
                    InventoryDBPath = Path.Combine(AppDataPath, "Inventory.db")
                },
                Logging = new LoggingSettings
                {
                    LogLevel = "Info",
                    LogRetentionDays = 30,
                    CurrentLogSize = 0
                },
                GlobalServices = GetDefaultGlobalServices(),
                PinnedDevices = new List<PinnedDevice>(),
                Appearance = new AppearanceSettings
                {
                    LogoPath = string.Empty,
                    PrimaryColor = "#0078D4",
                    SecondaryColor = "#00BCF2"
                },
                ToastNotifications = new ToastNotificationSettings(), // TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG
                KeyboardShortcuts = new KeyboardShortcutSettings() // TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG
            };
        }

        private static List<GlobalServiceStatus> GetDefaultGlobalServices()
        {
            return new List<GlobalServiceStatus>
            {
                new GlobalServiceStatus { ServiceName = "Azure", Endpoint = "https://status.azure.com/en-us/status", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "Microsoft 365", Endpoint = "https://status.office.com/", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "Azure DevOps", Endpoint = "https://status.dev.azure.com/", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "GitHub", Endpoint = "https://www.githubstatus.com/", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "AWS", Endpoint = "https://health.aws.amazon.com/health/status", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "Google Cloud", Endpoint = "https://status.cloud.google.com/", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "Cloudflare", Endpoint = "https://www.cloudflarestatus.com/", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "Atlassian", Endpoint = "https://status.atlassian.com/", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "Slack", Endpoint = "https://status.slack.com/", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "Zoom", Endpoint = "https://status.zoom.us/", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "Microsoft Teams", Endpoint = "https://status.office.com/", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "Salesforce", Endpoint = "https://status.salesforce.com/", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "Dropbox", Endpoint = "https://status.dropbox.com/", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "Adobe Creative Cloud", Endpoint = "https://status.adobe.com/", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "Docker Hub", Endpoint = "https://status.docker.com/", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "npm", Endpoint = "https://status.npmjs.org/", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "PyPI", Endpoint = "https://status.python.org/", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "NuGet", Endpoint = "https://status.nuget.org/", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "Google DNS", Endpoint = "ping:8.8.8.8", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "Cloudflare DNS", Endpoint = "ping:1.1.1.1", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "OpenDNS", Endpoint = "ping:208.67.222.222", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "Quad9 DNS", Endpoint = "ping:9.9.9.9", Status = "Checking..." },
                new GlobalServiceStatus { ServiceName = "Internet Connection", Endpoint = "ping:1.1.1.1", Status = "Checking..." }
            };
        }
    }

    // TAG: #DATA_MODELS
    #region Data Models

    public class AppSettings
    {
        public GeneralSettings General { get; set; }
        public PerformanceSettings Performance { get; set; }
        public PathSettings Paths { get; set; }
        public LoggingSettings Logging { get; set; }
        public List<GlobalServiceStatus> GlobalServices { get; set; }
        public List<PinnedDevice> PinnedDevices { get; set; }
        public AppearanceSettings Appearance { get; set; }
        public ToastNotificationSettings ToastNotifications { get; set; } // TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
        public KeyboardShortcutSettings KeyboardShortcuts { get; set; } // TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
    }

    public class GeneralSettings
    {
        public string LastUser { get; set; }
        public bool RememberDC { get; set; }
        public List<string> TargetHistory { get; set; }
        public int AutoRefreshInterval { get; set; }
    }

    public class PerformanceSettings
    {
        public int MaxParallelScans { get; set; }
        public int WmiTimeout { get; set; }
        public int PingTimeout { get; set; }
        public int MaxRetryAttempts { get; set; }
        public int ThreadPoolSize { get; set; }
    }

    public class PathSettings
    {
        public string SharedLogPath { get; set; }
        public string InventoryDBPath { get; set; }
    }

    public class LoggingSettings
    {
        public string LogLevel { get; set; }
        public int LogRetentionDays { get; set; }
        public long CurrentLogSize { get; set; }
    }

    // NOTE: GlobalServiceStatus and PinnedDevice classes are defined in MainWindow.xaml.cs

    public class AppearanceSettings
    {
        public string LogoPath { get; set; }
        public string PrimaryColor { get; set; }
        public string SecondaryColor { get; set; }
    }

    // TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
    /// <summary>
    /// Toast notification user preferences
    /// </summary>
    public class ToastNotificationSettings
    {
        public bool EnableToasts { get; set; }
        public bool ShowSuccessToasts { get; set; }
        public bool ShowInfoToasts { get; set; }
        public bool ShowWarningToasts { get; set; }
        public bool ShowErrorToasts { get; set; }
        public bool ShowStatusUpdateToasts { get; set; }
        public bool ShowValidationToasts { get; set; }
        public bool ShowWorkflowToasts { get; set; }
        public bool ShowErrorHandlerToasts { get; set; }

        public ToastNotificationSettings()
        {
            // All enabled by default
            EnableToasts = true;
            ShowSuccessToasts = true;
            ShowInfoToasts = true;
            ShowWarningToasts = true;
            ShowErrorToasts = true;
            ShowStatusUpdateToasts = true;
            ShowValidationToasts = true;
            ShowWorkflowToasts = true;
            ShowErrorHandlerToasts = true;
        }
    }

    // TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
    /// <summary>
    /// Keyboard shortcut customization
    /// </summary>
    public class KeyboardShortcutSettings
    {
        public Dictionary<string, KeyboardShortcut> Shortcuts { get; set; }

        public KeyboardShortcutSettings()
        {
            Shortcuts = GetDefaultShortcuts();
        }

        public static Dictionary<string, KeyboardShortcut> GetDefaultShortcuts()
        {
            return new Dictionary<string, KeyboardShortcut>
            {
                { "CommandPalette", new KeyboardShortcut { Command = "Open Command Palette", Key = "K", Modifiers = "Control" } },
                { "ScanDomain", new KeyboardShortcut { Command = "Scan Domain (Fleet)", Key = "F", Modifiers = "Control+Shift" } },
                { "ScanSingle", new KeyboardShortcut { Command = "Scan Single Computer", Key = "S", Modifiers = "Control" } },
                { "LoadADObjects", new KeyboardShortcut { Command = "Load AD Objects", Key = "L", Modifiers = "Control" } },
                { "Authenticate", new KeyboardShortcut { Command = "Authenticate", Key = "A", Modifiers = "Control+Alt" } },
                { "RDP", new KeyboardShortcut { Command = "RDP", Key = "R", Modifiers = "Control" } },
                { "PowerShell", new KeyboardShortcut { Command = "PowerShell", Key = "P", Modifiers = "Control" } },
                { "ToggleView", new KeyboardShortcut { Command = "Toggle View", Key = "T", Modifiers = "Control" } },
                { "ToggleTerminal", new KeyboardShortcut { Command = "Toggle Terminal", Key = "OemTilde", Modifiers = "Control" } },
                { "Settings", new KeyboardShortcut { Command = "Settings", Key = "OemComma", Modifiers = "Control" } },
                { "Refresh", new KeyboardShortcut { Command = "Refresh", Key = "F5", Modifiers = "None" } }
            };
        }
    }

    /// <summary>
    /// Individual keyboard shortcut definition
    /// </summary>
    public class KeyboardShortcut
    {
        public string Command { get; set; }
        public string Key { get; set; }
        public string Modifiers { get; set; }

        public string DisplayShortcut
        {
            get
            {
                if (Modifiers == "None")
                    return FormatKey(Key);
                return $"{Modifiers}+{FormatKey(Key)}";
            }
        }

        private string FormatKey(string key)
        {
            // Format special keys
            return key switch
            {
                "OemTilde" => "`",
                "OemComma" => ",",
                _ => key
            };
        }
    }

    #endregion
}
