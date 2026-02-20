// TAG: #NAT_AGENT #VERSION_1_0 #WINDOWS_SERVICE
// NecessaryAdminAgent - Entry point + install/uninstall/console modes
//
// Usage:
//   NecessaryAdminAgent.exe                           -- run as Windows service (SCM launches this)
//   NecessaryAdminAgent.exe --console                 -- run in console window for debugging
//   NecessaryAdminAgent.exe --install --token <tok> [--port 443]
//   NecessaryAdminAgent.exe --uninstall

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using Microsoft.Win32;

namespace NecessaryAdminAgent
{
    static class Program
    {
        internal const string SERVICE_NAME = "NecessaryAdminAgent";
        internal const string SERVICE_DISPLAY = "NecessaryAdminTool Agent";
        internal const string SERVICE_DESC = "Provides local WMI data over TCP for NecessaryAdminTool fleet scans. Eliminates WMI firewall port requirements.";
        internal const string REGISTRY_KEY = @"SOFTWARE\NecessaryAdminTool\Agent";
        internal const string LOG_PATH = @"C:\ProgramData\NecessaryAdminTool\Agent\agent.log";
        internal const string FIREWALL_RULE = "NecessaryAdminAgent";

        static void Main(string[] args)
        {
            // Parse command-line args
            string mode = "";
            string token = "";
            int port = 443;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--install":   mode = "install";   break;
                    case "--uninstall": mode = "uninstall"; break;
                    case "--console":   mode = "console";   break;
                    case "--token":
                        if (i + 1 < args.Length) token = args[++i];
                        break;
                    case "--port":
                        if (i + 1 < args.Length) int.TryParse(args[++i], out port);
                        break;
                }
            }

            switch (mode)
            {
                case "install":
                    Install(token, port);
                    break;
                case "uninstall":
                    Uninstall();
                    break;
                case "console":
                    RunConsole();
                    break;
                default:
                    // No args → launched by SCM as service
                    ServiceBase.Run(new AgentService());
                    break;
            }
        }

        // --install: write registry, configure firewall rule, create + start service
        static void Install(string token, int port)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.Error.WriteLine("ERROR: --token is required for --install");
                AgentLog.Write("Install() - ERROR: no token provided");
                Environment.Exit(1);
            }

            if (port < 1 || port > 65535)
            {
                Console.Error.WriteLine($"ERROR: invalid port {port} - must be 1-65535");
                AgentLog.Write($"Install() - ERROR: invalid port {port}");
                Environment.Exit(1);
            }

            Console.WriteLine($"[NecessaryAdminAgent] Installing service (port={port})...");

            // 1. Write config to registry (HKLM - requires elevation)
            try
            {
                using (var key = Registry.LocalMachine.CreateSubKey(REGISTRY_KEY, true))
                {
                    if (key == null) throw new InvalidOperationException("CreateSubKey returned null — check elevation");
                    key.SetValue("Token", token, RegistryValueKind.String);
                    key.SetValue("Port", port, RegistryValueKind.DWord);
                }
                Console.WriteLine("[NecessaryAdminAgent] Registry config written.");
                AgentLog.Write($"Install() - Registry config written to HKLM\\{REGISTRY_KEY}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: Failed to write registry: {ex.Message}");
                AgentLog.Write($"Install() - ERROR writing registry: {ex.Message}");
                Environment.Exit(2);
            }

            // 2. Ensure log directory exists with restricted ACL (SYSTEM + Administrators only)
            try
            {
                string logDir = Path.GetDirectoryName(LOG_PATH);
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);

                // TAG: #SECURITY_HARDENED - Restrict log directory to SYSTEM + Administrators
                try
                {
                    var dirInfo = new System.IO.DirectoryInfo(logDir);
                    var acl = dirInfo.GetAccessControl();
                    // Remove inherited rules, apply explicit SYSTEM + Administrators only
                    acl.SetAccessRuleProtection(true, false); // disable inheritance, remove inherited rules
                    acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                        new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.LocalSystemSid, null),
                        System.Security.AccessControl.FileSystemRights.FullControl,
                        System.Security.AccessControl.InheritanceFlags.ContainerInherit | System.Security.AccessControl.InheritanceFlags.ObjectInherit,
                        System.Security.AccessControl.PropagationFlags.None,
                        System.Security.AccessControl.AccessControlType.Allow));
                    acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                        new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid, null),
                        System.Security.AccessControl.FileSystemRights.FullControl,
                        System.Security.AccessControl.InheritanceFlags.ContainerInherit | System.Security.AccessControl.InheritanceFlags.ObjectInherit,
                        System.Security.AccessControl.PropagationFlags.None,
                        System.Security.AccessControl.AccessControlType.Allow));
                    dirInfo.SetAccessControl(acl);
                    Console.WriteLine("[NecessaryAdminAgent] Log directory ACL set (SYSTEM + Administrators only).");
                    AgentLog.Write("Install() - Log directory ACL restricted to SYSTEM + Administrators");
                }
                catch (Exception aclEx)
                {
                    // Non-fatal — log directory still usable, just not restricted
                    Console.Error.WriteLine($"WARNING: Could not set log directory ACL: {aclEx.Message}");
                    AgentLog.Write($"Install() - WARNING: ACL set failed: {aclEx.Message}");
                }
            }
            catch { /* non-fatal */ }

            // 3. Configure firewall rule (idempotent: delete then add)
            string exePath = Assembly.GetExecutingAssembly().Location;
            RunNetsh($"advfirewall firewall delete rule name=\"{FIREWALL_RULE}\"");
            RunNetsh($"advfirewall firewall add rule name=\"{FIREWALL_RULE}\" dir=in action=allow protocol=TCP localport={port} description=\"NecessaryAdminTool agent listener\"");
            Console.WriteLine($"[NecessaryAdminAgent] Firewall rule configured for port {port}.");

            // 4. Remove existing service (idempotent)
            RunSc($"stop {SERVICE_NAME}");
            System.Threading.Thread.Sleep(2000); // give SCM time to stop
            RunSc($"delete {SERVICE_NAME}");
            System.Threading.Thread.Sleep(1000);

            // 5. Create service
            RunSc($"create {SERVICE_NAME} binPath= \"{exePath}\" start= auto type= own DisplayName= \"{SERVICE_DISPLAY}\"");
            RunSc($"description {SERVICE_NAME} \"{SERVICE_DESC}\"");
            Console.WriteLine("[NecessaryAdminAgent] Service created.");

            // 6. Start service
            RunSc($"start {SERVICE_NAME}");
            Console.WriteLine("[NecessaryAdminAgent] Service started.");

            // 7. Output confirmation for ME capture (token NOT echoed — it was already known to ME as an input parameter)
            Console.WriteLine($"NAT_AGENT_CONFIGURED=true");
            Console.WriteLine($"NAT_AGENT_PORT={port}");
            Console.WriteLine("[NecessaryAdminAgent] Install complete.");
            AgentLog.Write($"Install() - complete, port={port}");
        }

        // --uninstall: stop + delete service, remove firewall rule
        static void Uninstall()
        {
            Console.WriteLine("[NecessaryAdminAgent] Uninstalling...");

            RunSc($"stop {SERVICE_NAME}");
            System.Threading.Thread.Sleep(2000);
            RunSc($"delete {SERVICE_NAME}");
            RunNetsh($"advfirewall firewall delete rule name=\"{FIREWALL_RULE}\"");

            // Remove registry key
            try { Registry.LocalMachine.DeleteSubKeyTree(REGISTRY_KEY, false); }
            catch { /* already gone */ }

            Console.WriteLine("[NecessaryAdminAgent] Uninstall complete.");
        }

        // --console: run listener in current console window (for debugging)
        static void RunConsole()
        {
            Console.WriteLine("[NecessaryAdminAgent] Running in console mode. Press Ctrl+C to stop.");
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; };

            var svc = new AgentService();
            svc.StartInConsole();

            Console.WriteLine("[NecessaryAdminAgent] Listener active. Press Enter to stop.");
            Console.ReadLine();
            svc.StopInConsole();
        }

        internal static void RunNetsh(string args)
        {
            try
            {
                var p = new Process
                {
                    StartInfo = new ProcessStartInfo("netsh", args)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                p.Start();
                bool exited = p.WaitForExit(10000);
                if (!exited) p.Kill();
                if (p.ExitCode != 0)
                    AgentLog.Write($"RunNetsh({args}) - exit code {p.ExitCode}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"netsh error: {ex.Message}");
                AgentLog.Write($"RunNetsh({args}) - exception: {ex.Message}");
            }
        }

        internal static void RunSc(string args)
        {
            try
            {
                var p = new Process
                {
                    StartInfo = new ProcessStartInfo("sc.exe", args)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                p.Start();
                bool exited = p.WaitForExit(10000);
                if (!exited) p.Kill();
                // sc.exe returns non-zero for expected states (e.g., service not found on delete) — log but don't fail
                if (p.ExitCode != 0)
                    AgentLog.Write($"RunSc({args}) - exit code {p.ExitCode}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"sc.exe error: {ex.Message}");
                AgentLog.Write($"RunSc({args}) - exception: {ex.Message}");
            }
        }
    }
}
