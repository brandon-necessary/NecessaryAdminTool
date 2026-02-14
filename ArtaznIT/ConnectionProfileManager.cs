using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace ArtaznIT
{
    // TAG: #VERSION_7 #CONNECTION_PROFILES #FEATURE
    /// <summary>
    /// Manages saved connection profiles for quick DC switching
    /// Allows users to save favorite DC configurations for different environments
    /// </summary>
    public class ConnectionProfileManager
    {
        private static List<ConnectionProfile> _profiles = new List<ConnectionProfile>();

        /// <summary>
        /// Load all connection profiles from settings
        /// </summary>
        public static void LoadProfiles()
        {
            try
            {
                string json = Properties.Settings.Default.ConnectionProfilesJson ?? "";
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var serializer = new JavaScriptSerializer();
                    _profiles = serializer.Deserialize<List<ConnectionProfile>>(json) ?? new List<ConnectionProfile>();
                    LogManager.LogInfo($"[Connection Profiles] Loaded {_profiles.Count} profiles");
                }
                else
                {
                    _profiles = new List<ConnectionProfile>();
                    LogManager.LogInfo("[Connection Profiles] No saved profiles found");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("[Connection Profiles] Failed to load profiles", ex);
                _profiles = new List<ConnectionProfile>();
            }
        }

        /// <summary>
        /// Save all connection profiles to settings
        /// </summary>
        public static void SaveProfiles()
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(_profiles);
                Properties.Settings.Default.ConnectionProfilesJson = json;
                Properties.Settings.Default.Save();
                LogManager.LogInfo($"[Connection Profiles] Saved {_profiles.Count} profiles");
            }
            catch (Exception ex)
            {
                LogManager.LogError("[Connection Profiles] Failed to save profiles", ex);
            }
        }

        /// <summary>
        /// Get all connection profiles
        /// </summary>
        public static List<ConnectionProfile> GetProfiles()
        {
            return new List<ConnectionProfile>(_profiles);
        }

        /// <summary>
        /// Add or update a connection profile
        /// </summary>
        public static void SaveProfile(ConnectionProfile profile)
        {
            // Remove existing profile with same name
            _profiles.RemoveAll(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));

            // Add new profile
            _profiles.Add(profile);

            // Save to settings
            SaveProfiles();

            LogManager.LogInfo($"[Connection Profiles] Saved profile: {profile.Name}");
        }

        /// <summary>
        /// Delete a connection profile by name
        /// </summary>
        public static bool DeleteProfile(string profileName)
        {
            int removed = _profiles.RemoveAll(p => p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));

            if (removed > 0)
            {
                SaveProfiles();
                LogManager.LogInfo($"[Connection Profiles] Deleted profile: {profileName}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get a profile by name
        /// </summary>
        public static ConnectionProfile GetProfile(string profileName)
        {
            return _profiles.FirstOrDefault(p => p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Check if a profile name already exists
        /// </summary>
        public static bool ProfileExists(string profileName)
        {
            return _profiles.Any(p => p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));
        }
    }

    // ══════════════════════════════════════════════════════════════
    // CONNECTION PROFILE DATA MODEL
    // TAG: #VERSION_7 #CONNECTION_PROFILES
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Represents a saved connection profile for a domain controller
    /// </summary>
    public class ConnectionProfile
    {
        /// <summary>Profile display name (e.g., "Production DC", "Test Environment")</summary>
        public string Name { get; set; }

        /// <summary>Domain controller hostname or IP</summary>
        public string DomainController { get; set; }

        /// <summary>Username (with domain, e.g., DOMAIN\user or user@domain.com)</summary>
        public string Username { get; set; }

        /// <summary>Domain name</summary>
        public string Domain { get; set; }

        /// <summary>Description/notes for this profile</summary>
        public string Description { get; set; }

        /// <summary>Environment type (Production, Test, Development, etc.)</summary>
        public string Environment { get; set; }

        /// <summary>Date/time when profile was created</summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>Date/time when profile was last used</summary>
        public DateTime LastUsedDate { get; set; }

        public ConnectionProfile()
        {
            CreatedDate = DateTime.Now;
            LastUsedDate = DateTime.Now;
        }

        /// <summary>Display name for UI bindings</summary>
        public string DisplayName => $"{Name} ({DomainController})";

        /// <summary>Icon for environment type</summary>
        public string EnvironmentIcon
        {
            get
            {
                return Environment?.ToLower() switch
                {
                    "production" => "🔴",
                    "test" => "🟡",
                    "development" => "🟢",
                    "staging" => "🟠",
                    _ => "🔵"
                };
            }
        }
    }
}
