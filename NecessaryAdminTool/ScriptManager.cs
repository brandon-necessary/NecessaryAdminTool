using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace NecessaryAdminTool
{
    // TAG: #VERSION_7.1 #SCRIPTS #BULK_OPERATIONS #AUTOMATION
    /// <summary>
    /// Manages PowerShell script library and execution
    /// Supports bulk execution across multiple computers with parallel processing
    /// </summary>
    public class ScriptManager
    {
        private static readonly string ScriptLibraryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NecessaryAdminTool", "ScriptLibrary");

        /// <summary>
        /// Script categories for organization
        /// </summary>
        public enum ScriptCategory
        {
            ActiveDirectory,
            WMI,
            Network,
            Services,
            Registry,
            FileSystem,
            Security,
            Custom
        }

        /// <summary>
        /// Saved script in the library
        /// </summary>
        public class SavedScript
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public ScriptCategory Category { get; set; }
            public string ScriptContent { get; set; }
            public DateTime CreatedDate { get; set; }
            public DateTime LastModified { get; set; }
            public bool IsBuiltIn { get; set; }

            public string CategoryIcon
            {
                get
                {
                    return Category switch
                    {
                        ScriptCategory.ActiveDirectory => "🌐",
                        ScriptCategory.WMI => "💻",
                        ScriptCategory.Network => "🌍",
                        ScriptCategory.Services => "⚙️",
                        ScriptCategory.Registry => "📋",
                        ScriptCategory.FileSystem => "📁",
                        ScriptCategory.Security => "🔒",
                        _ => "📜"
                    };
                }
            }
        }

        /// <summary>
        /// Result of script execution on a single computer
        /// </summary>
        public class ScriptExecutionResult
        {
            public string Hostname { get; set; }
            public bool Success { get; set; }
            public string Output { get; set; }
            public string Error { get; set; }
            public DateTime Timestamp { get; set; }
            public TimeSpan Duration { get; set; }
            public int ExitCode { get; set; }

            public string StatusIcon => Success ? "✅" : "❌";
            public string StatusText => Success ? "Success" : "Failed";
        }

        /// <summary>
        /// Initialize script library directory
        /// </summary>
        public static void Initialize()
        {
            try
            {
                if (!Directory.Exists(ScriptLibraryPath))
                {
                    Directory.CreateDirectory(ScriptLibraryPath);
                    LogManager.LogInfo($"[ScriptManager] Created script library: {ScriptLibraryPath}");
                }

                // Create built-in scripts if they don't exist
                CreateBuiltInScripts();
            }
            catch (Exception ex)
            {
                LogManager.LogError("[ScriptManager] Failed to initialize script library", ex);
            }
        }

        /// <summary>
        /// Create built-in script templates
        /// </summary>
        private static void CreateBuiltInScripts()
        {
            var builtInScripts = new List<SavedScript>
            {
                new SavedScript
                {
                    Name = "Get Installed Software",
                    Description = "Lists all installed applications from registry",
                    Category = ScriptCategory.Registry,
                    IsBuiltIn = true,
                    CreatedDate = DateTime.Now,
                    LastModified = DateTime.Now,
                    ScriptContent = @"# Get installed software from registry
$software = @()
$paths = @(
    'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
    'HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
)

foreach ($path in $paths) {
    $software += Get-ItemProperty $path -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName } |
        Select-Object DisplayName, DisplayVersion, Publisher, InstallDate
}

$software | Sort-Object DisplayName | Format-Table -AutoSize"
                },

                new SavedScript
                {
                    Name = "Get Disk Space",
                    Description = "Shows disk space usage for all drives",
                    Category = ScriptCategory.WMI,
                    IsBuiltIn = true,
                    CreatedDate = DateTime.Now,
                    LastModified = DateTime.Now,
                    ScriptContent = @"# Get disk space for all drives
Get-WmiObject Win32_LogicalDisk -Filter ""DriveType=3"" |
    Select-Object DeviceID,
        @{Name='Size(GB)';Expression={[math]::Round($_.Size/1GB,2)}},
        @{Name='FreeSpace(GB)';Expression={[math]::Round($_.FreeSpace/1GB,2)}},
        @{Name='Used(GB)';Expression={[math]::Round(($_.Size-$_.FreeSpace)/1GB,2)}},
        @{Name='PercentFree';Expression={[math]::Round(($_.FreeSpace/$_.Size)*100,2)}} |
    Format-Table -AutoSize"
                },

                new SavedScript
                {
                    Name = "Get Running Services",
                    Description = "Lists all running Windows services",
                    Category = ScriptCategory.Services,
                    IsBuiltIn = true,
                    CreatedDate = DateTime.Now,
                    LastModified = DateTime.Now,
                    ScriptContent = @"# Get running services
Get-Service | Where-Object { $_.Status -eq 'Running' } |
    Select-Object Name, DisplayName, Status, StartType |
    Sort-Object DisplayName |
    Format-Table -AutoSize"
                },

                new SavedScript
                {
                    Name = "Get Local Administrators",
                    Description = "Lists members of the local Administrators group",
                    Category = ScriptCategory.Security,
                    IsBuiltIn = true,
                    CreatedDate = DateTime.Now,
                    LastModified = DateTime.Now,
                    ScriptContent = @"# Get local administrators
$adminGroup = Get-LocalGroup -Name 'Administrators'
Get-LocalGroupMember -Group $adminGroup |
    Select-Object Name, ObjectClass, PrincipalSource |
    Format-Table -AutoSize"
                },

                new SavedScript
                {
                    Name = "Check BitLocker Status",
                    Description = "Checks BitLocker encryption status for all volumes",
                    Category = ScriptCategory.Security,
                    IsBuiltIn = true,
                    CreatedDate = DateTime.Now,
                    LastModified = DateTime.Now,
                    ScriptContent = @"# Check BitLocker status
Get-BitLockerVolume |
    Select-Object MountPoint, EncryptionMethod, VolumeStatus, ProtectionStatus,
        @{Name='EncryptionPercentage';Expression={$_.EncryptionPercentage}} |
    Format-Table -AutoSize"
                },

                new SavedScript
                {
                    Name = "Get Last Logged User",
                    Description = "Gets the last logged-on user from registry",
                    Category = ScriptCategory.Registry,
                    IsBuiltIn = true,
                    CreatedDate = DateTime.Now,
                    LastModified = DateTime.Now,
                    ScriptContent = @"# Get last logged-on user
$lastUser = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI' -Name LastLoggedOnUser -ErrorAction SilentlyContinue
if ($lastUser) {
    Write-Output ""Last Logged-On User: $($lastUser.LastLoggedOnUser)""
} else {
    Write-Output ""Unable to determine last logged-on user""
}"
                },

                new SavedScript
                {
                    Name = "Get Network Configuration",
                    Description = "Shows IP configuration and network adapters",
                    Category = ScriptCategory.Network,
                    IsBuiltIn = true,
                    CreatedDate = DateTime.Now,
                    LastModified = DateTime.Now,
                    ScriptContent = @"# Get network configuration
Get-NetIPConfiguration |
    Where-Object { $_.IPv4DefaultGateway -ne $null } |
    Select-Object InterfaceAlias,
        @{Name='IPAddress';Expression={$_.IPv4Address.IPAddress}},
        @{Name='Gateway';Expression={$_.IPv4DefaultGateway.NextHop}},
        @{Name='DNSServer';Expression={$_.DNSServer.ServerAddresses -join ', '}} |
    Format-Table -AutoSize"
                },

                new SavedScript
                {
                    Name = "Get System Uptime",
                    Description = "Shows system boot time and uptime",
                    Category = ScriptCategory.WMI,
                    IsBuiltIn = true,
                    CreatedDate = DateTime.Now,
                    LastModified = DateTime.Now,
                    ScriptContent = @"# Get system uptime
$os = Get-WmiObject Win32_OperatingSystem
$bootTime = $os.ConvertToDateTime($os.LastBootUpTime)
$uptime = (Get-Date) - $bootTime

Write-Output ""Boot Time: $bootTime""
Write-Output ""Uptime: $($uptime.Days) days, $($uptime.Hours) hours, $($uptime.Minutes) minutes"""
                }
            };

            // Save built-in scripts if they don't exist
            foreach (var script in builtInScripts)
            {
                string filePath = Path.Combine(ScriptLibraryPath, $"{script.Name}.json");
                if (!File.Exists(filePath))
                {
                    SaveScript(script);
                }
            }
        }

        /// <summary>
        /// Load all scripts from library
        /// </summary>
        public static List<SavedScript> LoadAllScripts()
        {
            var scripts = new List<SavedScript>();

            try
            {
                if (!Directory.Exists(ScriptLibraryPath))
                    return scripts;

                var jsonFiles = Directory.GetFiles(ScriptLibraryPath, "*.json");
                var serializer = new JavaScriptSerializer();

                foreach (var file in jsonFiles)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var script = serializer.Deserialize<SavedScript>(json);
                        scripts.Add(script);
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogWarning($"[ScriptManager] Failed to load script: {file} - {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("[ScriptManager] Failed to load scripts", ex);
            }

            return scripts.OrderBy(s => s.Category).ThenBy(s => s.Name).ToList();
        }

        /// <summary>
        /// Save script to library
        /// </summary>
        public static bool SaveScript(SavedScript script)
        {
            try
            {
                if (!Directory.Exists(ScriptLibraryPath))
                    Directory.CreateDirectory(ScriptLibraryPath);

                script.LastModified = DateTime.Now;
                if (script.CreatedDate == DateTime.MinValue)
                    script.CreatedDate = DateTime.Now;

                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(script);

                string filePath = Path.Combine(ScriptLibraryPath, $"{script.Name}.json");
                File.WriteAllText(filePath, json);

                LogManager.LogInfo($"[ScriptManager] Saved script: {script.Name}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[ScriptManager] Failed to save script: {script.Name}", ex);
                return false;
            }
        }

        /// <summary>
        /// Delete script from library
        /// </summary>
        public static bool DeleteScript(string scriptName)
        {
            try
            {
                string filePath = Path.Combine(ScriptLibraryPath, $"{scriptName}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    LogManager.LogInfo($"[ScriptManager] Deleted script: {scriptName}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[ScriptManager] Failed to delete script: {scriptName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Export script to .ps1 file
        /// </summary>
        public static bool ExportScript(SavedScript script, string filePath)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"# Script: {script.Name}");
                sb.AppendLine($"# Description: {script.Description}");
                sb.AppendLine($"# Category: {script.Category}");
                sb.AppendLine($"# Created: {script.CreatedDate:yyyy-MM-dd}");
                sb.AppendLine($"# Modified: {script.LastModified:yyyy-MM-dd}");
                sb.AppendLine();
                sb.AppendLine(script.ScriptContent);

                File.WriteAllText(filePath, sb.ToString());
                LogManager.LogInfo($"[ScriptManager] Exported script to: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[ScriptManager] Failed to export script: {script.Name}", ex);
                return false;
            }
        }

        /// <summary>
        /// Import script from .ps1 file
        /// </summary>
        public static SavedScript ImportScript(string filePath, string name = null, ScriptCategory category = ScriptCategory.Custom)
        {
            try
            {
                string content = File.ReadAllText(filePath);

                return new SavedScript
                {
                    Name = name ?? Path.GetFileNameWithoutExtension(filePath),
                    Description = $"Imported from {Path.GetFileName(filePath)}",
                    Category = category,
                    ScriptContent = content,
                    CreatedDate = DateTime.Now,
                    LastModified = DateTime.Now,
                    IsBuiltIn = false
                };
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[ScriptManager] Failed to import script: {filePath}", ex);
                return null;
            }
        }

        /// <summary>
        /// Execute PowerShell script on multiple computers in parallel
        /// TAG: #VERSION_7.1 #BULK_OPERATIONS #PARALLEL_EXECUTION
        /// </summary>
        public static async Task<List<ScriptExecutionResult>> ExecuteScriptBulkAsync(
            string scriptContent,
            string[] hostnames,
            string username = null,
            string password = null,
            int maxConcurrency = 10,
            CancellationToken cancellationToken = default,
            IProgress<(string hostname, int completed, int total)> progress = null)
        {
            var results = new List<ScriptExecutionResult>();
            var semaphore = new SemaphoreSlim(maxConcurrency);
            int completed = 0;
            int total = hostnames.Length;

            var tasks = hostnames.Select(async hostname =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var result = await ExecuteScriptAsync(hostname, scriptContent, username, password, cancellationToken);
                    lock (results)
                    {
                        results.Add(result);
                        completed++;
                        progress?.Report((hostname, completed, total));
                    }
                    return result;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return results.OrderBy(r => r.Hostname).ToList();
        }

        /// <summary>
        /// Execute PowerShell script on a single computer
        /// </summary>
        private static async Task<ScriptExecutionResult> ExecuteScriptAsync(
            string hostname,
            string scriptContent,
            string username,
            string password,
            CancellationToken cancellationToken)
        {
            var result = new ScriptExecutionResult
            {
                Hostname = hostname,
                Timestamp = DateTime.Now
            };

            var startTime = DateTime.Now;

            await Task.Run(() =>
            {
                try
                {
                    // Create runspace configuration
                    var initialSessionState = InitialSessionState.CreateDefault();
                    initialSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;

                    using (var runspace = RunspaceFactory.CreateRunspace(initialSessionState))
                    {
                        runspace.Open();

                        // Build script with remote execution
                        var scriptBuilder = new StringBuilder();

                        // If credentials provided, use Invoke-Command for remote execution
                        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                        {
                            scriptBuilder.AppendLine($"$secPass = ConvertTo-SecureString '{password}' -AsPlainText -Force");
                            scriptBuilder.AppendLine($"$cred = New-Object System.Management.Automation.PSCredential('{username}', $secPass)");
                            scriptBuilder.AppendLine($"Invoke-Command -ComputerName '{hostname}' -Credential $cred -ScriptBlock {{");
                            scriptBuilder.AppendLine(scriptContent);
                            scriptBuilder.AppendLine("}");
                        }
                        else
                        {
                            // Try local execution or Invoke-Command without credentials
                            if (hostname.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase) ||
                                hostname.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                            {
                                scriptBuilder.AppendLine(scriptContent);
                            }
                            else
                            {
                                scriptBuilder.AppendLine($"Invoke-Command -ComputerName '{hostname}' -ScriptBlock {{");
                                scriptBuilder.AppendLine(scriptContent);
                                scriptBuilder.AppendLine("}");
                            }
                        }

                        using (var pipeline = runspace.CreatePipeline())
                        {
                            pipeline.Commands.AddScript(scriptBuilder.ToString());

                            var output = pipeline.Invoke();
                            var errors = pipeline.Error.ReadToEnd();

                            // Collect output
                            var outputBuilder = new StringBuilder();
                            foreach (var item in output)
                            {
                                outputBuilder.AppendLine(item?.ToString() ?? "");
                            }

                            result.Output = outputBuilder.ToString().Trim();

                            // Collect errors
                            if (errors.Count > 0)
                            {
                                var errorBuilder = new StringBuilder();
                                foreach (var error in errors)
                                {
                                    errorBuilder.AppendLine(error?.ToString() ?? "");
                                }
                                result.Error = errorBuilder.ToString().Trim();
                                result.Success = false;
                                result.ExitCode = 1;
                            }
                            else
                            {
                                result.Success = true;
                                result.ExitCode = 0;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = $"Exception: {ex.Message}\n\n{ex.StackTrace}";
                    result.ExitCode = -1;
                    LogManager.LogError($"[ScriptManager] Script execution failed on {hostname}", ex);
                }
            }, cancellationToken);

            result.Duration = DateTime.Now - startTime;
            return result;
        }

        /// <summary>
        /// Export execution results to CSV
        /// </summary>
        public static bool ExportResultsToCsv(List<ScriptExecutionResult> results, string filePath)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Hostname,Status,Duration(ms),Output,Error");

                foreach (var result in results)
                {
                    string output = result.Output?.Replace("\"", "\"\"").Replace("\r\n", " ").Replace("\n", " ") ?? "";
                    string error = result.Error?.Replace("\"", "\"\"").Replace("\r\n", " ").Replace("\n", " ") ?? "";

                    sb.AppendLine($"\"{result.Hostname}\",\"{result.StatusText}\",{result.Duration.TotalMilliseconds},\"{output}\",\"{error}\"");
                }

                File.WriteAllText(filePath, sb.ToString());
                LogManager.LogInfo($"[ScriptManager] Exported results to: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[ScriptManager] Failed to export results", ex);
                return false;
            }
        }

        /// <summary>
        /// Export execution results to TXT (formatted)
        /// </summary>
        public static bool ExportResultsToTxt(List<ScriptExecutionResult> results, string filePath)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("========================================");
                sb.AppendLine("Script Execution Results");
                sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Total Computers: {results.Count}");
                sb.AppendLine($"Successful: {results.Count(r => r.Success)}");
                sb.AppendLine($"Failed: {results.Count(r => !r.Success)}");
                sb.AppendLine("========================================");
                sb.AppendLine();

                foreach (var result in results)
                {
                    sb.AppendLine($"Computer: {result.Hostname}");
                    sb.AppendLine($"Status: {result.StatusIcon} {result.StatusText}");
                    sb.AppendLine($"Duration: {result.Duration.TotalSeconds:F2}s");
                    sb.AppendLine($"Timestamp: {result.Timestamp:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine();

                    if (!string.IsNullOrEmpty(result.Output))
                    {
                        sb.AppendLine("Output:");
                        sb.AppendLine(result.Output);
                        sb.AppendLine();
                    }

                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        sb.AppendLine("Error:");
                        sb.AppendLine(result.Error);
                        sb.AppendLine();
                    }

                    sb.AppendLine("----------------------------------------");
                    sb.AppendLine();
                }

                File.WriteAllText(filePath, sb.ToString());
                LogManager.LogInfo($"[ScriptManager] Exported results to: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[ScriptManager] Failed to export results", ex);
                return false;
            }
        }
    }
}
