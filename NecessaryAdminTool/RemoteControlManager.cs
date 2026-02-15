using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Web.Script.Serialization;
using NecessaryAdminTool.Properties;
using SecValidator = NecessaryAdminTool.Security.SecurityValidator;

namespace NecessaryAdminTool
{
    // TAG: #RMM_INTEGRATION #REMOTE_CONTROL #MANAGER
    /// <summary>
    /// Centralized manager for remote control tool integrations
    /// Security-first: All integrations disabled by default
    /// </summary>
    public static class RemoteControlManager
    {
        private static RemoteControlConfig _config;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// Initialize the remote control manager
        /// </summary>
        public static void Initialize()
        {
            lock (_lockObject)
            {
                LoadConfiguration();
            }
        }

        /// <summary>
        /// Get current configuration
        /// </summary>
        public static RemoteControlConfig GetConfiguration()
        {
            lock (_lockObject)
            {
                if (_config == null)
                    LoadConfiguration();

                return _config;
            }
        }

        /// <summary>
        /// Load configuration from settings
        /// </summary>
        private static void LoadConfiguration()
        {
            try
            {
                string configJson = Settings.Default.RemoteControlConfigJson ?? "";

                if (string.IsNullOrWhiteSpace(configJson))
                {
                    // Create default config (all disabled)
                    _config = CreateDefaultConfiguration();
                }
                else
                {
                    var serializer = new JavaScriptSerializer();
                    _config = serializer.Deserialize<RemoteControlConfig>(configJson);

                    // Ensure config is valid
                    if (_config == null || _config.Tools == null)
                        _config = CreateDefaultConfiguration();
                }
            }
            catch
            {
                _config = CreateDefaultConfiguration();
            }
        }

        /// <summary>
        /// Save configuration to settings
        /// </summary>
        public static void SaveConfiguration(RemoteControlConfig config)
        {
            lock (_lockObject)
            {
                _config = config;

                var serializer = new JavaScriptSerializer();
                string configJson = serializer.Serialize(config);

                Settings.Default.RemoteControlConfigJson = configJson;
                Settings.Default.Save();
            }
        }

        /// <summary>
        /// Create default configuration with all tools disabled
        /// </summary>
        private static RemoteControlConfig CreateDefaultConfiguration()
        {
            return new RemoteControlConfig
            {
                MasterEnabled = false, // SECURITY: Disabled by default
                ConnectionTimeoutSeconds = 30,
                RetryAttempts = 2,
                ShowConfirmationDialog = true,
                Tools = new List<RmmToolConfig>
                {
                    new RmmToolConfig
                    {
                        ToolName = "AnyDesk",
                        ToolType = RmmToolType.AnyDesk,
                        Enabled = false,
                        IsConfigured = false,
                        Settings = new Dictionary<string, string>
                        {
                            { "ExePath", @"C:\Program Files (x86)\AnyDesk\AnyDesk.exe" },
                            { "ConnectionMode", "attended" }
                        }
                    },
                    new RmmToolConfig
                    {
                        ToolName = "ScreenConnect",
                        ToolType = RmmToolType.ScreenConnect,
                        Enabled = false,
                        IsConfigured = false,
                        Settings = new Dictionary<string, string>
                        {
                            { "ServerUrl", "" },
                            { "Port", "443" },
                            { "AuthMethod", "url" },
                            { "SessionType", "support" }
                        }
                    },
                    new RmmToolConfig
                    {
                        ToolName = "TeamViewer",
                        ToolType = RmmToolType.TeamViewer,
                        Enabled = false,
                        IsConfigured = false,
                        Settings = new Dictionary<string, string>
                        {
                            { "ExePath", @"C:\Program Files\TeamViewer\TeamViewer.exe" },
                            { "AuthMethod", "cli" }
                        }
                    },
                    new RmmToolConfig
                    {
                        ToolName = "RemotePC",
                        ToolType = RmmToolType.RemotePC,
                        Enabled = false,
                        IsConfigured = false,
                        Settings = new Dictionary<string, string>
                        {
                            { "ApiUrl", "https://api.remotepc.com" },
                            { "TeamId", "" }
                        }
                    },
                    new RmmToolConfig
                    {
                        ToolName = "Dameware",
                        ToolType = RmmToolType.Dameware,
                        Enabled = false,
                        IsConfigured = false,
                        Settings = new Dictionary<string, string>
                        {
                            { "ServerUrl", "" },
                            { "Department", "IT Support" }
                        }
                    },
                    new RmmToolConfig
                    {
                        ToolName = "ManageEngine",
                        ToolType = RmmToolType.ManageEngine,
                        Enabled = false,
                        IsConfigured = false,
                        Settings = new Dictionary<string, string>
                        {
                            { "ServerUrl", "" },
                            { "Port", "8383" }
                        }
                    },
                    new RmmToolConfig
                    {
                        ToolName = "NinjaOne",
                        ToolType = RmmToolType.NinjaOne,
                        Enabled = false,
                        IsConfigured = false,
                        Settings = new Dictionary<string, string>
                        {
                            { "ApiUrl", "https://app.ninjarmm.com" },
                            { "RegionCode", "us" }
                        }
                    },
                    new RmmToolConfig
                    {
                        ToolName = "LogMeIn",
                        ToolType = RmmToolType.LogMeIn,
                        Enabled = false,
                        IsConfigured = false,
                        Settings = new Dictionary<string, string>
                        {
                            { "ExePath", @"C:\Program Files (x86)\LogMeIn\x64\LogMeIn.exe" },
                            { "Mode", "rescue" }
                        }
                    },
                    new RmmToolConfig
                    {
                        ToolName = "Kaseya VSA",
                        ToolType = RmmToolType.KaseyaVSA,
                        Enabled = false,
                        IsConfigured = false,
                        Settings = new Dictionary<string, string>
                        {
                            { "ServerUrl", "" },
                            { "Port", "5721" }
                        }
                    },
                    new RmmToolConfig
                    {
                        ToolName = "Atera",
                        ToolType = RmmToolType.Atera,
                        Enabled = false,
                        IsConfigured = false,
                        Settings = new Dictionary<string, string>
                        {
                            { "ApiUrl", "https://app.atera.com/api" },
                            { "AccountName", "" }
                        }
                    },
                    new RmmToolConfig
                    {
                        ToolName = "SolarWinds RMM",
                        ToolType = RmmToolType.SolarWindsRMM,
                        Enabled = false,
                        IsConfigured = false,
                        Settings = new Dictionary<string, string>
                        {
                            { "ServerUrl", "" },
                            { "DashboardUrl", "" }
                        }
                    },
                    new RmmToolConfig
                    {
                        ToolName = "BeyondTrust",
                        ToolType = RmmToolType.BeyondTrust,
                        Enabled = false,
                        IsConfigured = false,
                        Settings = new Dictionary<string, string>
                        {
                            { "ServerUrl", "" },
                            { "SiteId", "" }
                        }
                    },
                    new RmmToolConfig
                    {
                        ToolName = "GoToAssist",
                        ToolType = RmmToolType.GoToAssist,
                        Enabled = false,
                        IsConfigured = false,
                        Settings = new Dictionary<string, string>
                        {
                            { "ExePath", @"C:\Program Files (x86)\Citrix\GoToAssist\g2ax_customer.exe" },
                            { "Mode", "attended" }
                        }
                    },
                    new RmmToolConfig
                    {
                        ToolName = "Splashtop",
                        ToolType = RmmToolType.Splashtop,
                        Enabled = false,
                        IsConfigured = false,
                        Settings = new Dictionary<string, string>
                        {
                            { "TeamId", "" },
                            { "DeploymentKey", "" }
                        }
                    },
                    new RmmToolConfig
                    {
                        ToolName = "RealVNC",
                        ToolType = RmmToolType.RealVNC,
                        Enabled = false,
                        IsConfigured = false,
                        Settings = new Dictionary<string, string>
                        {
                            { "ExePath", @"C:\Program Files\RealVNC\VNC Viewer\vncviewer.exe" },
                            { "CloudAccount", "" }
                        }
                    },
                    new RmmToolConfig
                    {
                        ToolName = "ConnectWise Automate",
                        ToolType = RmmToolType.ConnectWiseAutomate,
                        Enabled = false,
                        IsConfigured = false,
                        Settings = new Dictionary<string, string>
                        {
                            { "ServerUrl", "" },
                            { "Port", "443" }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Get enabled tools for display
        /// </summary>
        public static List<RmmToolConfig> GetEnabledTools()
        {
            lock (_lockObject)
            {
                if (_config == null)
                    LoadConfiguration();

                if (!_config.MasterEnabled)
                    return new List<RmmToolConfig>();

                return _config.Tools.Where(t => t.Enabled && t.IsConfigured).ToList();
            }
        }

        /// <summary>
        /// Check if a specific tool is enabled and configured
        /// </summary>
        public static bool IsToolAvailable(RmmToolType toolType)
        {
            lock (_lockObject)
            {
                if (_config == null)
                    LoadConfiguration();

                if (!_config.MasterEnabled)
                    return false;

                var tool = _config.Tools.FirstOrDefault(t => t.ToolType == toolType);
                return tool != null && tool.Enabled && tool.IsConfigured;
            }
        }

        /// <summary>
        /// Get configuration for a specific tool
        /// </summary>
        public static RmmToolConfig GetToolConfig(RmmToolType toolType)
        {
            lock (_lockObject)
            {
                if (_config == null)
                    LoadConfiguration();

                return _config.Tools.FirstOrDefault(t => t.ToolType == toolType);
            }
        }

        /// <summary>
        /// Launch remote session for a target host
        /// </summary>
        public static void LaunchSession(RmmToolType toolType, string targetHost)
        {
            // TAG: #SECURITY_CRITICAL #COMMAND_INJECTION_PREVENTION
            // Validate target host before any operations
            if (string.IsNullOrWhiteSpace(targetHost))
            {
                LogManager.LogWarning("[RemoteControlManager] Blocked: null/empty target host");
                throw new ArgumentException("Target host cannot be null or empty");
            }

            // Validate hostname or IP address format
            bool isValidHostname = SecValidator.IsValidHostname(targetHost);
            bool isValidIP = SecValidator.IsValidIPAddress(targetHost);

            if (!isValidHostname && !isValidIP)
            {
                LogManager.LogWarning($"[RemoteControlManager] Blocked invalid target: {targetHost}");
                throw new ArgumentException($"Invalid target host format: {targetHost}");
            }

            // Security validation
            if (!_config.MasterEnabled)
                throw new InvalidOperationException("Remote control integrations are disabled. Enable in Options menu.");

            var toolConfig = GetToolConfig(toolType);
            if (toolConfig == null || !toolConfig.Enabled)
                throw new InvalidOperationException($"{toolType} integration is disabled. Enable in Options menu.");

            if (!toolConfig.IsConfigured)
                throw new InvalidOperationException($"{toolType} is not configured. Configure in Options menu.");

            // Audit logging
            LogManager.LogInfo($"[AUDIT] Remote session initiated: {toolType} → {targetHost} by {Environment.UserName}");

            // Launch appropriate integration
            switch (toolType)
            {
                case RmmToolType.AnyDesk:
                    Integrations.AnyDeskIntegration.LaunchSession(targetHost, toolConfig);
                    break;
                case RmmToolType.ScreenConnect:
                    Integrations.ScreenConnectIntegration.LaunchSession(targetHost, toolConfig);
                    break;
                case RmmToolType.TeamViewer:
                    Integrations.TeamViewerIntegration.LaunchSession(targetHost, toolConfig);
                    break;
                case RmmToolType.RemotePC:
                    Integrations.RemotePCIntegration.LaunchSession(targetHost, toolConfig);
                    break;
                case RmmToolType.Dameware:
                    Integrations.DamewareIntegration.LaunchSession(targetHost, toolConfig);
                    break;
                case RmmToolType.ManageEngine:
                    Integrations.ManageEngineIntegration.LaunchSession(targetHost, toolConfig);
                    break;
                default:
                    throw new NotImplementedException($"Integration for {toolType} not yet implemented.");
            }

            LogManager.LogInfo($"Remote session launched successfully: {toolType} → {targetHost}");
        }

        /// <summary>
        /// Test connection for a specific tool
        /// </summary>
        public static bool TestConnection(RmmToolType toolType)
        {
            try
            {
                var toolConfig = GetToolConfig(toolType);
                if (toolConfig == null)
                    return false;

                switch (toolType)
                {
                    case RmmToolType.AnyDesk:
                        return Integrations.AnyDeskIntegration.TestConnection(toolConfig);
                    case RmmToolType.ScreenConnect:
                        return Integrations.ScreenConnectIntegration.TestConnection(toolConfig);
                    case RmmToolType.TeamViewer:
                        return Integrations.TeamViewerIntegration.TestConnection(toolConfig);
                    case RmmToolType.RemotePC:
                        return Integrations.RemotePCIntegration.TestConnection(toolConfig);
                    case RmmToolType.Dameware:
                        return Integrations.DamewareIntegration.TestConnection(toolConfig);
                    case RmmToolType.ManageEngine:
                        return Integrations.ManageEngineIntegration.TestConnection(toolConfig);
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }
    }

    // TAG: #RMM_INTEGRATION #DATA_MODELS
    /// <summary>
    /// Remote control configuration
    /// </summary>
    public class RemoteControlConfig
    {
        public bool MasterEnabled { get; set; } = false;
        public int ConnectionTimeoutSeconds { get; set; } = 30;
        public int RetryAttempts { get; set; } = 2;
        public bool ShowConfirmationDialog { get; set; } = true;
        public List<RmmToolConfig> Tools { get; set; } = new List<RmmToolConfig>();
    }

    /// <summary>
    /// Individual RMM tool configuration
    /// </summary>
    public class RmmToolConfig
    {
        public string ToolName { get; set; }
        public bool Enabled { get; set; } = false;
        public RmmToolType ToolType { get; set; }
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
        public DateTime LastTested { get; set; } = DateTime.MinValue;
        public bool IsConfigured { get; set; } = false;
        public string CredentialKeyName { get; set; }
    }

    /// <summary>
    /// Supported RMM tool types
    /// </summary>
    public enum RmmToolType
    {
        AnyDesk,
        ScreenConnect,
        TeamViewer,
        RemotePC,
        Dameware,
        ManageEngine,
        NinjaOne,
        LogMeIn,
        KaseyaVSA,
        Atera,
        SolarWindsRMM,
        BeyondTrust,
        GoToAssist,
        Splashtop,
        RealVNC,
        ConnectWiseAutomate
    }
}
