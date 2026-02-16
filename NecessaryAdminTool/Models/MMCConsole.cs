using System;
using System.Collections.Generic;

namespace NecessaryAdminTool.Models
{
    // TAG: #MMC_EMBEDDING #ADMIN_TOOLS #AUTO_UPDATE_VERSION
    /// <summary>
    /// Represents an MMC (Microsoft Management Console) snap-in configuration.
    /// Used to dynamically launch and embed admin tools like ADUC, GPMC, DNS Manager, etc.
    /// </summary>
    public class MMCConsole
    {
        /// <summary>
        /// Display name of the console (e.g., "AD Users & Computers")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// MMC snap-in file name (e.g., "dsa.msc")
        /// </summary>
        public string SnapinFile { get; set; }

        /// <summary>
        /// Description of what this console does
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Whether this console requires elevated (admin) privileges
        /// </summary>
        public bool RequiresElevation { get; set; }

        /// <summary>
        /// Icon emoji for UI display
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Gets the static list of all supported MMC consoles
        /// TAG: #MMC_EMBEDDING #ADMIN_TOOLS
        /// </summary>
        public static List<MMCConsole> GetAllConsoles()
        {
            return new List<MMCConsole>
            {
                new MMCConsole
                {
                    Name = "AD Users & Computers",
                    SnapinFile = "dsa.msc",
                    Description = "Manage Active Directory users, groups, computers, and OUs",
                    RequiresElevation = false,
                    Icon = "👥"
                },
                new MMCConsole
                {
                    Name = "Group Policy (GPMC)",
                    SnapinFile = "gpmc.msc",
                    Description = "Manage Group Policy Objects and apply policies across domain",
                    RequiresElevation = false,
                    Icon = "📋"
                },
                new MMCConsole
                {
                    Name = "DNS Manager",
                    SnapinFile = "dnsmgmt.msc",
                    Description = "Manage DNS zones, records, and server settings",
                    RequiresElevation = false,
                    Icon = "🌐"
                },
                new MMCConsole
                {
                    Name = "DHCP",
                    SnapinFile = "dhcpmgmt.msc",
                    Description = "Manage DHCP scopes, leases, and reservations",
                    RequiresElevation = false,
                    Icon = "📡"
                },
                new MMCConsole
                {
                    Name = "Services (Local/Remote)",
                    SnapinFile = "services.msc",
                    Description = "Manage Windows services on local or remote computers",
                    RequiresElevation = false,
                    Icon = "⚙️"
                },
                new MMCConsole
                {
                    Name = "AD Sites and Services",
                    SnapinFile = "dssite.msc",
                    Description = "Manage Active Directory sites, subnets, and replication",
                    RequiresElevation = false,
                    Icon = "🌍"
                },
                new MMCConsole
                {
                    Name = "AD Domains and Trusts",
                    SnapinFile = "domain.msc",
                    Description = "Manage domain functional levels and forest trusts",
                    RequiresElevation = false,
                    Icon = "🔗"
                },
                new MMCConsole
                {
                    Name = "Certification Authority",
                    SnapinFile = "certsrv.msc",
                    Description = "Manage certificate templates and issued certificates",
                    RequiresElevation = true,
                    Icon = "🔐"
                },
                new MMCConsole
                {
                    Name = "Failover Cluster Manager",
                    SnapinFile = "cluadmin.msc",
                    Description = "Manage Windows Server failover clusters",
                    RequiresElevation = false,
                    Icon = "🔄"
                },
                new MMCConsole
                {
                    Name = "Event Viewer",
                    SnapinFile = "eventvwr.msc",
                    Description = "View system, application, and security event logs",
                    RequiresElevation = false,
                    Icon = "📊"
                },
                new MMCConsole
                {
                    Name = "Performance Monitor",
                    SnapinFile = "perfmon.msc",
                    Description = "Monitor system performance and resource usage",
                    RequiresElevation = false,
                    Icon = "📈"
                }
            };
        }

        /// <summary>
        /// Get console by display name
        /// TAG: #MMC_EMBEDDING
        /// </summary>
        public static MMCConsole GetByName(string name)
        {
            var consoles = GetAllConsoles();
            return consoles.Find(c => c.Name == name);
        }

        /// <summary>
        /// Get console by snap-in file name
        /// TAG: #MMC_EMBEDDING
        /// </summary>
        public static MMCConsole GetBySnapinFile(string snapinFile)
        {
            var consoles = GetAllConsoles();
            return consoles.Find(c => c.SnapinFile.Equals(snapinFile, StringComparison.OrdinalIgnoreCase));
        }
    }
}
