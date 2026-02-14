using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using ArtaznIT.Properties;

namespace ArtaznIT
{
    // TAG: #SETTINGS #SETTINGS_MANAGER #CENTRALIZED_CONFIG
    /// <summary>
    /// Centralized settings management for ArtaznIT Suite
    /// Handles loading, saving, validation, export/import, and reset operations
    /// </summary>
    public static class SettingsManager
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ArtaznIT"
        );

        private static readonly string SecureConfigPath = Path.Combine(AppDataPath, "SecureConfig.xml");
        private static readonly string UserConfigPath = Path.Combine(AppDataPath, "UserConfig.xml");
        private static readonly string DCManagerPath = Path.Combine(AppDataPath, "DCManager.xml");

        // TAG: #LOAD_SETTINGS
        /// <summary>
        /// Load all settings from various sources into unified structure
        /// </summary>
        public static AppSettings LoadAllSettings()
        {
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
                    Appearance = LoadAppearanceSettings()
                };

                return settings;
            }
            catch (Exception ex)
            {
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

        // TAG: #SAVE_SETTINGS
        /// <summary>
        /// Save specific settings category
        /// </summary>
        public static void SaveGeneralSettings(GeneralSettings settings)
        {
            Settings.Default.LastUser = settings.LastUser;
            SaveTargetHistory(settings.TargetHistory);
            Settings.Default.Save();
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
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save global services: {ex.Message}");
            }
        }

        public static void SavePinnedDevices(List<PinnedDevice> devices)
        {
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
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save pinned devices: {ex.Message}");
            }
        }

        public static void SaveAppearanceSettings(AppearanceSettings settings)
        {
            // Appearance settings could be saved to custom config or registry
            // For now, stored in-memory during runtime
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

        // TAG: #RESET_ALL
        /// <summary>
        /// Factory reset - clear all settings and restore defaults
        /// </summary>
        public static void ResetAllSettings()
        {
            try
            {
                // Clear all Properties.Settings
                Settings.Default.LastUser = string.Empty;
                Settings.Default.PinnedDevices = string.Empty;
                Settings.Default.GlobalServicesConfig = string.Empty;
                Settings.Default.Save();

                // Delete XML config files (optional - recreated on next launch)
                DeleteConfigFiles();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to reset all settings: {ex.Message}");
            }
        }

        // TAG: #EXPORT_IMPORT
        /// <summary>
        /// Export all settings to JSON file
        /// </summary>
        public static void ExportToFile(string filePath)
        {
            try
            {
                var settings = LoadAllSettings();
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(settings);

                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Import settings from JSON file
        /// </summary>
        public static void ImportFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("Settings file not found.");

                string json = File.ReadAllText(filePath);
                var serializer = new JavaScriptSerializer();
                var settings = serializer.Deserialize<AppSettings>(json);

                // Apply imported settings
                if (settings.General != null)
                    SaveGeneralSettings(settings.General);

                if (settings.GlobalServices != null)
                    SaveGlobalServices(settings.GlobalServices);

                if (settings.PinnedDevices != null)
                    SavePinnedDevices(settings.PinnedDevices);

                // Other categories can be added here
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to import settings: {ex.Message}");
            }
        }

        // TAG: #VALIDATION
        /// <summary>
        /// Validate path settings
        /// </summary>
        public static bool ValidatePaths(PathSettings settings)
        {
            try
            {
                // Check if parent directories exist or can be created
                if (!string.IsNullOrEmpty(settings.SharedLogPath))
                {
                    string parentDir = Path.GetDirectoryName(settings.SharedLogPath);
                    if (!Directory.Exists(parentDir))
                        return false;
                }

                if (!string.IsNullOrEmpty(settings.InventoryDBPath))
                {
                    string parentDir = Path.GetDirectoryName(settings.InventoryDBPath);
                    if (!Directory.Exists(parentDir))
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validate performance settings
        /// </summary>
        public static bool ValidatePerformance(PerformanceSettings settings)
        {
            return settings.MaxParallelScans >= 1 && settings.MaxParallelScans <= 100 &&
                   settings.WmiTimeout >= 1000 && settings.WmiTimeout <= 60000 &&
                   settings.PingTimeout >= 100 && settings.PingTimeout <= 10000 &&
                   settings.MaxRetryAttempts >= 1 && settings.MaxRetryAttempts <= 5 &&
                   settings.ThreadPoolSize >= 1;
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
                if (File.Exists(SecureConfigPath))
                    File.Delete(SecureConfigPath);

                if (File.Exists(UserConfigPath))
                    File.Delete(UserConfigPath);

                if (File.Exists(DCManagerPath))
                    File.Delete(DCManagerPath);
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
                }
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

    #endregion
}
