// TAG: #NAT_AGENT #VERSION_1_0 #WMI_QUERIES
// QueryHandler.cs - WMI queries + JSON builder for all supported agent commands
// All queries run locally (no firewall issues since agent is ON the target machine).
//
// Supported commands:
//   PING            - hostname, timestamp, agent_version
//   GET_SYSTEM_INFO - full 20-field snapshot matching Master_Update_Log.csv schema
//   GET_PROCESSES   - top 30 by WorkingSetSize
//   GET_SERVICES    - Name, State, StartMode, DisplayName
//   GET_NETWORK     - all adapters with IP, MAC, DHCP
//   GET_INSTALLED   - registry uninstall keys (HKLM + Wow6432Node)

using System;
using System.Collections.Generic;
using System.Management;
using System.Text;
using Microsoft.Win32;

namespace NecessaryAdminAgent
{
    internal static class QueryHandler
    {
        private static readonly Version _agentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

        public static string Execute(string command)
        {
            try
            {
                switch ((command ?? "").ToUpperInvariant())
                {
                    case "PING":          return Ping();
                    case "GET_SYSTEM_INFO": return GetSystemInfo();
                    case "GET_PROCESSES": return GetProcesses();
                    case "GET_SERVICES":  return GetServices();
                    case "GET_NETWORK":   return GetNetwork();
                    case "GET_INSTALLED": return GetInstalled();
                    default:
                        AgentLog.Write($"QueryHandler.Execute() - unknown command: {(command ?? "").Replace("\r","").Replace("\n","").Replace("\t"," ")}");
                        return Error("Unknown command");
                }
            }
            catch (Exception ex)
            {
                // Sanitize command before logging to prevent log injection via newlines
                string safeCmd = (command ?? "").Replace("\r", "").Replace("\n", "").Replace("\t", " ");
                AgentLog.Write($"QueryHandler.Execute({safeCmd}) - ERROR: {ex.Message}");
                // Return generic error to client — do not expose internal exception details
                return Error("Query failed — see agent log for details");
            }
        }

        // ---- PING ----
        private static string Ping()
        {
            return $"{{\"success\":true,\"command\":\"PING\"," +
                   $"\"hostname\":{Jstr(Environment.MachineName)}," +
                   $"\"timestamp\":{Jstr(DateTime.UtcNow.ToString("o"))}," +
                   $"\"agent_version\":{Jstr(_agentVersion.ToString())}}}";
        }

        // ---- GET_SYSTEM_INFO ----
        // Returns 20-field snapshot matching Master_Update_Log.csv schema
        private static string GetSystemInfo()
        {
            string os = "", build = "", arch = "", totalRamGB = "0", freeRamGB = "0";
            string lastBoot = "", computerName = Environment.MachineName;
            string manufacturer = "", model = "", serial = "", processor = "";
            string ipAddress = "", macAddress = "", diskTotalGB = "0", diskFreeGB = "0";
            string loggedInUser = "", tpmStatus = "", secureBoot = "";

            // Win32_OperatingSystem
            try
            {
                using (var mos = new ManagementObjectSearcher("SELECT Caption,BuildNumber,OSArchitecture,TotalVisibleMemorySize,FreePhysicalMemory,LastBootUpTime FROM Win32_OperatingSystem"))
                foreach (ManagementObject mo in mos.Get())
                {
                    os = mo["Caption"]?.ToString() ?? "";
                    build = mo["BuildNumber"]?.ToString() ?? "";
                    arch = mo["OSArchitecture"]?.ToString() ?? "";
                    ulong totalKB = (ulong)(mo["TotalVisibleMemorySize"] ?? 0UL);
                    ulong freeKB  = (ulong)(mo["FreePhysicalMemory"] ?? 0UL);
                    totalRamGB = Math.Round(totalKB / 1048576.0, 2).ToString("F2");
                    freeRamGB  = Math.Round(freeKB  / 1048576.0, 2).ToString("F2");
                    if (mo["LastBootUpTime"] != null)
                        lastBoot = ManagementDateTimeConverter.ToDateTime(mo["LastBootUpTime"].ToString()).ToString("o");
                    break;
                }
            }
            catch (Exception ex) { AgentLog.Write($"GetSystemInfo() Win32_OperatingSystem: {ex.Message}"); }

            // Win32_ComputerSystem
            try
            {
                using (var mos = new ManagementObjectSearcher("SELECT Manufacturer,Model,UserName FROM Win32_ComputerSystem"))
                foreach (ManagementObject mo in mos.Get())
                {
                    manufacturer = mo["Manufacturer"]?.ToString()?.Trim() ?? "";
                    model = mo["Model"]?.ToString()?.Trim() ?? "";
                    loggedInUser = mo["UserName"]?.ToString() ?? "";
                    break;
                }
            }
            catch (Exception ex) { AgentLog.Write($"GetSystemInfo() Win32_ComputerSystem: {ex.Message}"); }

            // Win32_BIOS (serial number)
            try
            {
                using (var mos = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
                foreach (ManagementObject mo in mos.Get())
                {
                    serial = mo["SerialNumber"]?.ToString()?.Trim() ?? "";
                    break;
                }
            }
            catch (Exception ex) { AgentLog.Write($"GetSystemInfo() Win32_BIOS: {ex.Message}"); }

            // Win32_Processor
            try
            {
                using (var mos = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                foreach (ManagementObject mo in mos.Get())
                {
                    processor = mo["Name"]?.ToString()?.Trim() ?? "";
                    break;
                }
            }
            catch (Exception ex) { AgentLog.Write($"GetSystemInfo() Win32_Processor: {ex.Message}"); }

            // Win32_NetworkAdapterConfiguration (first enabled IPv4)
            try
            {
                using (var mos = new ManagementObjectSearcher("SELECT IPAddress,MACAddress FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=True"))
                foreach (ManagementObject mo in mos.Get())
                {
                    string[] ips = (string[])(mo["IPAddress"] ?? new string[0]);
                    foreach (string ip in ips)
                    {
                        if (ip.Contains(".") && !ip.StartsWith("127.") && !ip.StartsWith("169.254."))
                        {
                            ipAddress = ip;
                            macAddress = mo["MACAddress"]?.ToString() ?? "";
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(ipAddress)) break;
                }
            }
            catch (Exception ex) { AgentLog.Write($"GetSystemInfo() Win32_NetworkAdapterConfiguration: {ex.Message}"); }

            // Win32_DiskDrive (first disk, total size)
            try
            {
                using (var mos = new ManagementObjectSearcher("SELECT Size FROM Win32_DiskDrive"))
                foreach (ManagementObject mo in mos.Get())
                {
                    ulong bytes = (ulong)(mo["Size"] ?? 0UL);
                    diskTotalGB = Math.Round(bytes / 1073741824.0, 1).ToString("F1");
                    break;
                }
                // Free space from C: logical disk
                using (var mos2 = new ManagementObjectSearcher("SELECT FreeSpace,Size FROM Win32_LogicalDisk WHERE DeviceID='C:'"))
                foreach (ManagementObject mo in mos2.Get())
                {
                    ulong free  = (ulong)(mo["FreeSpace"] ?? 0UL);
                    diskFreeGB  = Math.Round(free / 1073741824.0, 1).ToString("F1");
                    break;
                }
            }
            catch (Exception ex) { AgentLog.Write($"GetSystemInfo() Win32_DiskDrive: {ex.Message}"); }

            // TPM status via Win32_Tpm (root\cimv2\Security\MicrosoftTpm namespace)
            // ManagementScope does not implement IDisposable, so no using statement
            try
            {
                var tpmScope = new ManagementScope(@"\\.\root\cimv2\Security\MicrosoftTpm");
                using (var mos = new ManagementObjectSearcher(tpmScope, new ObjectQuery("SELECT IsActivated_InitialValue,IsEnabled_InitialValue,IsOwned_InitialValue FROM Win32_Tpm")))
                foreach (ManagementObject mo in mos.Get())
                {
                    bool active  = (bool)(mo["IsActivated_InitialValue"] ?? false);
                    bool enabled = (bool)(mo["IsEnabled_InitialValue"]  ?? false);
                    tpmStatus = (active && enabled) ? "Enabled" : "Disabled";
                    break;
                }
            }
            catch { tpmStatus = "Unknown"; }

            // Secure Boot via registry
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State"))
                {
                    object val = key?.GetValue("UEFISecureBootEnabled");
                    secureBoot = (val is int i) ? (i == 1 ? "Enabled" : "Disabled") : "Unknown";
                }
            }
            catch { secureBoot = "Unknown"; }

            var sb = new StringBuilder();
            sb.Append("{\"success\":true,\"command\":\"GET_SYSTEM_INFO\"");
            sb.Append($",\"hostname\":{Jstr(computerName)}");
            sb.Append($",\"os\":{Jstr(os)}");
            sb.Append($",\"build\":{Jstr(build)}");
            sb.Append($",\"arch\":{Jstr(arch)}");
            sb.Append($",\"manufacturer\":{Jstr(manufacturer)}");
            sb.Append($",\"model\":{Jstr(model)}");
            sb.Append($",\"serial\":{Jstr(serial)}");
            sb.Append($",\"processor\":{Jstr(processor)}");
            sb.Append($",\"total_ram_gb\":{Jstr(totalRamGB)}");
            sb.Append($",\"free_ram_gb\":{Jstr(freeRamGB)}");
            sb.Append($",\"disk_total_gb\":{Jstr(diskTotalGB)}");
            sb.Append($",\"disk_free_gb\":{Jstr(diskFreeGB)}");
            sb.Append($",\"ip_address\":{Jstr(ipAddress)}");
            sb.Append($",\"mac_address\":{Jstr(macAddress)}");
            sb.Append($",\"logged_in_user\":{Jstr(loggedInUser)}");
            sb.Append($",\"last_boot\":{Jstr(lastBoot)}");
            sb.Append($",\"tpm_status\":{Jstr(tpmStatus)}");
            sb.Append($",\"secure_boot\":{Jstr(secureBoot)}");
            sb.Append($",\"agent_version\":{Jstr(_agentVersion.ToString())}");
            sb.Append("}");
            return sb.ToString();
        }

        // ---- GET_PROCESSES ----
        // Returns top 30 processes by working set size
        private static string GetProcesses()
        {
            var list = new List<string>();
            try
            {
                using (var mos = new ManagementObjectSearcher("SELECT Name,ProcessId,WorkingSetSize,CommandLine FROM Win32_Process"))
                {
                    var rows = new List<(string name, uint pid, ulong ws, string cmd)>();
                    foreach (ManagementObject mo in mos.Get())
                    {
                        string name = mo["Name"]?.ToString() ?? "";
                        uint pid    = (uint)(mo["ProcessId"] ?? 0u);
                        ulong ws    = (ulong)(mo["WorkingSetSize"] ?? 0UL);
                        string cmd  = mo["CommandLine"]?.ToString() ?? "";
                        rows.Add((name, pid, ws, cmd));
                    }
                    rows.Sort((a, b) => b.ws.CompareTo(a.ws));
                    int count = Math.Min(30, rows.Count);
                    for (int i = 0; i < count; i++)
                    {
                        var r = rows[i];
                        list.Add($"{{\"name\":{Jstr(r.name)},\"pid\":{r.pid},\"ws_mb\":{Math.Round(r.ws / 1048576.0, 1).ToString("F1")},\"cmd\":{Jstr(Truncate(r.cmd, 200))}}}");
                    }
                }
            }
            catch (Exception ex) { AgentLog.Write($"GetProcesses() ERROR: {ex.Message}"); }

            return $"{{\"success\":true,\"command\":\"GET_PROCESSES\",\"processes\":[{string.Join(",", list)}]}}";
        }

        // ---- GET_SERVICES ----
        private static string GetServices()
        {
            var list = new List<string>();
            try
            {
                using (var mos = new ManagementObjectSearcher("SELECT Name,DisplayName,State,StartMode FROM Win32_Service"))
                foreach (ManagementObject mo in mos.Get())
                {
                    string name        = mo["Name"]?.ToString() ?? "";
                    string displayName = mo["DisplayName"]?.ToString() ?? "";
                    string state       = mo["State"]?.ToString() ?? "";
                    string startMode   = mo["StartMode"]?.ToString() ?? "";
                    list.Add($"{{\"name\":{Jstr(name)},\"display_name\":{Jstr(displayName)},\"state\":{Jstr(state)},\"start_mode\":{Jstr(startMode)}}}");
                }
            }
            catch (Exception ex) { AgentLog.Write($"GetServices() ERROR: {ex.Message}"); }

            return $"{{\"success\":true,\"command\":\"GET_SERVICES\",\"services\":[{string.Join(",", list)}]}}";
        }

        // ---- GET_NETWORK ----
        private static string GetNetwork()
        {
            var list = new List<string>();
            try
            {
                using (var mos = new ManagementObjectSearcher("SELECT Description,IPAddress,MACAddress,DefaultIPGateway,DHCPEnabled,DHCPServer,DNSServerSearchOrder FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=True"))
                foreach (ManagementObject mo in mos.Get())
                {
                    string desc    = mo["Description"]?.ToString() ?? "";
                    string mac     = mo["MACAddress"]?.ToString() ?? "";
                    bool dhcp      = (bool)(mo["DHCPEnabled"] ?? false);
                    string dhcpSrv = mo["DHCPServer"]?.ToString() ?? "";
                    string[] ips   = (string[])(mo["IPAddress"] ?? new string[0]);
                    string[] gws   = (string[])(mo["DefaultIPGateway"] ?? new string[0]);
                    string[] dns   = (string[])(mo["DNSServerSearchOrder"] ?? new string[0]);
                    string ipList  = "[" + string.Join(",", Array.ConvertAll(ips, Jstr)) + "]";
                    string gwList  = "[" + string.Join(",", Array.ConvertAll(gws, Jstr)) + "]";
                    string dnsList = "[" + string.Join(",", Array.ConvertAll(dns, Jstr)) + "]";

                    list.Add($"{{\"description\":{Jstr(desc)},\"mac\":{Jstr(mac)},\"dhcp\":{(dhcp?"true":"false")},\"dhcp_server\":{Jstr(dhcpSrv)},\"ip_addresses\":{ipList},\"gateways\":{gwList},\"dns_servers\":{dnsList}}}");
                }
            }
            catch (Exception ex) { AgentLog.Write($"GetNetwork() ERROR: {ex.Message}"); }

            return $"{{\"success\":true,\"command\":\"GET_NETWORK\",\"adapters\":[{string.Join(",", list)}]}}";
        }

        // ---- GET_INSTALLED ----
        // Reads both HKLM\SOFTWARE\...\Uninstall and Wow6432Node
        private static string GetInstalled()
        {
            var list = new List<string>();
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            ReadUninstallKey(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", list, seen);
            ReadUninstallKey(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", list, seen);

            return $"{{\"success\":true,\"command\":\"GET_INSTALLED\",\"applications\":[{string.Join(",", list)}]}}";
        }

        private static void ReadUninstallKey(RegistryKey hive, string path, List<string> list, System.Collections.Generic.HashSet<string> seen)
        {
            try
            {
                using (var uninstall = hive.OpenSubKey(path))
                {
                    if (uninstall == null) return;
                    foreach (string subName in uninstall.GetSubKeyNames())
                    {
                        try
                        {
                            using (var sub = uninstall.OpenSubKey(subName))
                            {
                                if (sub == null) continue;
                                string name = sub.GetValue("DisplayName")?.ToString() ?? "";
                                if (string.IsNullOrWhiteSpace(name)) continue;
                                if (!seen.Add(name)) continue; // deduplicate

                                string publisher   = sub.GetValue("Publisher")?.ToString() ?? "";
                                string version     = sub.GetValue("DisplayVersion")?.ToString() ?? "";
                                string installDate = sub.GetValue("InstallDate")?.ToString() ?? "";
                                list.Add($"{{\"name\":{Jstr(name)},\"publisher\":{Jstr(publisher)},\"version\":{Jstr(version)},\"install_date\":{Jstr(installDate)}}}");
                            }
                        }
                        catch { /* skip bad keys */ }
                    }
                }
            }
            catch (Exception ex) { AgentLog.Write($"ReadUninstallKey({path}) ERROR: {ex.Message}"); }
        }

        // ---- Helpers ----

        private static string Error(string message)
            => $"{{\"success\":false,\"error\":{Jstr(message)}}}";

        // Escape a string value for inline JSON (no Newtonsoft dependency)
        private static string Jstr(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder("\"", s.Length + 2);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append($"\\u{(int)c:x4}");
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static string Truncate(string s, int max)
            => (s != null && s.Length > max) ? s.Substring(0, max) + "..." : s ?? "";
    }
}
