using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
// TAG: #REMOTE_EXECUTION #WINRM #POWERSHELL_DEPLOYMENT #VERSION_2_5

namespace NecessaryAdminTool.Managers
{
    /// <summary>
    /// Result of a remote script execution attempt.
    /// </summary>
    public class RemoteScriptResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public string MethodUsed { get; set; } = string.Empty;
        public int ExitCode { get; set; }
        public TimeSpan Elapsed { get; set; }
    }

    /// <summary>
    /// Executes PowerShell scripts on remote computers.
    ///
    /// Execution order:
    ///   1. WinRM via System.Management.Automation API (preferred – no child process,
    ///      Kerberos auth, real-time output capture, lowest EDR footprint)
    ///   2. PowerShell subprocess Invoke-Command fallback (if direct API fails or
    ///      WinRM port is not reachable from this machine)
    ///
    /// PsExec is intentionally excluded – it uploads PSEXESVC.exe to ADMIN$ which is
    /// flagged by virtually every enterprise EDR product (Cortex XDR, CrowdStrike, etc.)
    /// and puts plaintext credentials in process arguments visible to all users.
    ///
    /// TAG: #REMOTE_EXECUTION #WINRM #POWERSHELL_DEPLOYMENT
    /// </summary>
    public static class RemoteScriptManager
    {
        private const int WinRmPort = 5985;
        private const int WinRmPortTls = 5986;

        // ──────────────────────────────────────────────────────────────────────
        // Public entry point: auto-detect best method
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Executes a PowerShell script on a remote computer.
        /// Automatically tries WinRM direct API first, then subprocess fallback.
        /// </summary>
        public static async Task<RemoteScriptResult> ExecuteAsync(
            string targetComputer,
            string scriptContent,
            string authUser,
            SecureString authPass,
            Action<string> progressCallback = null,
            int timeoutMs = 120000)
        {
            // TAG: #REMOTE_EXECUTION #WINRM
            LogManager.LogInfo($"RemoteScriptManager.ExecuteAsync() - START - Target: {targetComputer}");
            var sw = Stopwatch.StartNew();

            // Parse domain\user from authUser
            string domain = string.Empty;
            string username = authUser ?? string.Empty;
            if (!string.IsNullOrEmpty(authUser) && authUser.Contains("\\"))
            {
                var parts = authUser.Split(new[] { '\\' }, 2);
                domain = parts[0];
                username = parts[1];
            }

            try
            {
                // ── Method 1: Direct WinRM API (System.Management.Automation) ──
                progressCallback?.Invoke("[Method 1] Testing WinRM port 5985...");
                bool winRmOpen = await IsPortOpenAsync(targetComputer, WinRmPort, timeoutMs: 3000).ConfigureAwait(false);

                if (winRmOpen)
                {
                    LogManager.LogInfo($"[RemoteScriptManager] WinRM port open on {targetComputer} - using direct SMA API");
                    progressCallback?.Invoke("[Method 1] WinRM available - connecting via System.Management.Automation...");

                    var result = await ExecuteViaWinRmApiAsync(
                        targetComputer, scriptContent, domain, username, authPass, progressCallback, timeoutMs).ConfigureAwait(false);

                    result.Elapsed = sw.Elapsed;

                    if (result.Success || (result.Output?.Length ?? 0) > 0)
                    {
                        // Got a meaningful response (errors from the remote script are still valid)
                        LogManager.LogInfo($"RemoteScriptManager.ExecuteAsync() - SUCCESS via WinRM API - {sw.ElapsedMilliseconds}ms");
                        return result;
                    }

                    // SMA API connected but returned nothing useful - try subprocess
                    progressCallback?.Invoke($"[Method 1] ✗ WinRM API returned no output - trying subprocess...");
                    LogManager.LogWarning($"[RemoteScriptManager] WinRM API empty result for {targetComputer}, falling back");
                }
                else
                {
                    progressCallback?.Invoke("[Method 1] ✗ WinRM port 5985 not reachable - trying subprocess method...");
                    LogManager.LogWarning($"[RemoteScriptManager] WinRM port closed on {targetComputer}");
                }

                // ── Method 2: PowerShell subprocess Invoke-Command ──
                progressCallback?.Invoke("[Method 2] Attempting PowerShell subprocess with Invoke-Command...");
                LogManager.LogInfo($"[RemoteScriptManager] Falling back to subprocess for {targetComputer}");

                var fallbackResult = await ExecuteViaSubprocessAsync(
                    targetComputer, scriptContent, domain, username, authPass, progressCallback, timeoutMs).ConfigureAwait(false);

                fallbackResult.Elapsed = sw.Elapsed;
                return fallbackResult;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"RemoteScriptManager.ExecuteAsync() - UNHANDLED - Target: {targetComputer}", ex);
                return new RemoteScriptResult
                {
                    Success = false,
                    Errors = new List<string> { ex.Message },
                    MethodUsed = "Error",
                    Elapsed = sw.Elapsed
                };
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Method 1: Direct WinRM via System.Management.Automation
        // ──────────────────────────────────────────────────────────────────────

        private static async Task<RemoteScriptResult> ExecuteViaWinRmApiAsync(
            string targetComputer,
            string scriptContent,
            string domain,
            string username,
            SecureString password,
            Action<string> progressCallback,
            int timeoutMs)
        {
            // TAG: #WINRM #KERBEROS #SYSTEM_MANAGEMENT_AUTOMATION
            return await Task.Run(() =>
            {
                var result = new RemoteScriptResult { MethodUsed = "WinRM Direct API (SMA)" };
                var outputLines = new List<string>();

                try
                {
                    LogManager.LogInfo($"[RemoteScriptManager] Opening WinRM runspace to {targetComputer}");

                    var uri = new Uri($"http://{targetComputer}:{WinRmPort}/WSMAN");

                    // Build PSCredential - Kerberos is preferred (uses domain ticket, no plaintext on wire)
                    // Falls back to NTLM automatically if Kerberos is unavailable
                    PSCredential credential = null;
                    if (password != null && !string.IsNullOrEmpty(username))
                    {
                        string fullUser = string.IsNullOrEmpty(domain)
                            ? username
                            : $"{domain}\\{username}";
                        credential = new PSCredential(fullUser, password);
                    }

                    var connectionInfo = new WSManConnectionInfo(
                        uri,
                        "http://schemas.microsoft.com/powershell/Microsoft.PowerShell",
                        credential)
                    {
                        // Kerberos: uses cached domain ticket (no plaintext over wire)
                        // Falls back to Negotiate (NTLM) automatically if Kerberos unavailable
                        AuthenticationMechanism = AuthenticationMechanism.Negotiate,
                        OperationTimeout = timeoutMs,
                        OpenTimeout = 15000,
                        IdleTimeout = 60000
                    };

                    using (var runspace = RunspaceFactory.CreateRunspace(connectionInfo))
                    {
                        runspace.Open();
                        LogManager.LogInfo($"[RemoteScriptManager] Runspace opened to {targetComputer}");

                        using (var ps = PowerShell.Create())
                        {
                            ps.Runspace = runspace;
                            ps.AddScript(scriptContent);

                            // Stream output in real-time via PSDataCollection
                            var psOutput = new PSDataCollection<PSObject>();
                            psOutput.DataAdded += (s, e) =>
                            {
                                var item = psOutput[e.Index]?.ToString() ?? string.Empty;
                                if (!string.IsNullOrEmpty(item))
                                {
                                    outputLines.Add(item);
                                    progressCallback?.Invoke(item);
                                }
                            };

                            ps.Streams.Warning.DataAdded += (s, e) =>
                            {
                                var w = ps.Streams.Warning[e.Index]?.ToString();
                                if (!string.IsNullOrEmpty(w))
                                    result.Warnings.Add(w);
                            };

                            // Invoke synchronously inside Task.Run
                            ps.Invoke(null, psOutput);

                            result.Success = !ps.HadErrors;
                            result.Output = string.Join(Environment.NewLine, outputLines);
                            result.Errors = ps.Streams.Error
                                .Select(e => e?.ToString() ?? string.Empty)
                                .Where(e => !string.IsNullOrEmpty(e))
                                .ToList();

                            if (result.Errors.Any())
                            {
                                foreach (var err in result.Errors)
                                    progressCallback?.Invoke($"[ERROR] {err}");
                            }

                            LogManager.LogInfo($"[RemoteScriptManager] WinRM API complete - HadErrors: {ps.HadErrors}, OutputLines: {outputLines.Count}");
                        }
                    }

                    progressCallback?.Invoke($"[Method 1] ✓ WinRM Direct API succeeded");
                    return result;
                }
                catch (System.Management.Automation.Remoting.PSRemotingTransportException pex)
                {
                    // WinRM transport error (auth failure, access denied, etc.)
                    result.Success = false;
                    string detail = pex.Message;
                    result.Errors.Add($"WinRM transport error: {detail}");
                    progressCallback?.Invoke($"[Method 1] ✗ WinRM transport error: {detail}");
                    LogManager.LogWarning($"[RemoteScriptManager] WinRM PSRemotingTransportException for {targetComputer}: {detail}");
                    return result;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Errors.Add(ex.Message);
                    progressCallback?.Invoke($"[Method 1] ✗ WinRM API error: {ex.Message}");
                    LogManager.LogError($"[RemoteScriptManager] WinRM API failed for {targetComputer}", ex);
                    return result;
                }
            });
        }

        // ──────────────────────────────────────────────────────────────────────
        // Method 2: PowerShell subprocess with Invoke-Command
        // ──────────────────────────────────────────────────────────────────────

        private static async Task<RemoteScriptResult> ExecuteViaSubprocessAsync(
            string targetComputer,
            string scriptContent,
            string domain,
            string username,
            SecureString password,
            Action<string> progressCallback,
            int timeoutMs)
        {
            // TAG: #POWERSHELL_SUBPROCESS #INVOKE_COMMAND
            var result = new RemoteScriptResult { MethodUsed = "PowerShell Subprocess (Invoke-Command)" };

            return await Task.Run(() =>
            {
                try
                {
                    // Safe target for embedding in PS string
                    string safeTarget = targetComputer.Replace("'", "''");

                    // Encode scriptContent as base64 to prevent ScriptBlock injection.
                    // Embedding scriptContent directly inside {{ {scriptContent} }} allows a
                    // malicious caller to close the ScriptBlock early with '}' and inject commands.
                    string innerB64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(scriptContent ?? string.Empty));

                    // Wrap script in Invoke-Command; reconstruct ScriptBlock from base64 at runtime
                    string fullScript = $"$ProgressPreference='SilentlyContinue'; " +
                        $"$_sb = [ScriptBlock]::Create([Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('{innerB64}'))); " +
                        $"Invoke-Command -ComputerName '{safeTarget}' -ScriptBlock $_sb | Out-String";

                    string b64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(fullScript));

                    // Prefer the full path to powershell.exe
                    string psExe = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.System),
                        "WindowsPowerShell", "v1.0", "powershell.exe");
                    if (!File.Exists(psExe)) psExe = "powershell.exe";

                    var psi = new ProcessStartInfo
                    {
                        FileName = psExe,
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {b64}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    // Pass credentials via ProcessStartInfo (uses CreateProcessWithLogonW)
                    if (password != null && !string.IsNullOrEmpty(username))
                    {
                        psi.Domain = domain;
                        psi.UserName = username;
                        psi.Password = password;
                        psi.LoadUserProfile = false;
                    }

                    var outputLines = new List<string>();
                    var errorLines = new List<string>();

                    using (var proc = Process.Start(psi))
                    {
                        proc.OutputDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                outputLines.Add(e.Data);
                                progressCallback?.Invoke(e.Data);
                            }
                        };
                        proc.ErrorDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                                errorLines.Add(e.Data);
                        };
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();

                        if (!proc.WaitForExit(timeoutMs))
                        {
                            try { proc.Kill(); } catch (InvalidOperationException) { /* Already exited between check and kill */ }
                            result.Errors.Add($"Subprocess timed out after {timeoutMs / 1000}s");
                            progressCallback?.Invoke($"[Method 2] ✗ Timed out after {timeoutMs / 1000}s");
                            LogManager.LogWarning($"[RemoteScriptManager] Subprocess timed out for {targetComputer}");
                        }
                        else
                        {
                            result.ExitCode = proc.ExitCode;
                            result.Success = proc.ExitCode == 0;
                        }
                    }

                    result.Output = string.Join(Environment.NewLine, outputLines);
                    result.Errors.AddRange(errorLines);

                    if (result.Success)
                        progressCallback?.Invoke($"[Method 2] ✓ Subprocess Invoke-Command succeeded");
                    else
                        progressCallback?.Invoke($"[Method 2] ✗ Subprocess exited with code {result.ExitCode}");

                    LogManager.LogInfo($"[RemoteScriptManager] Subprocess complete - ExitCode: {result.ExitCode}, Success: {result.Success}");
                    return result;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Errors.Add(ex.Message);
                    progressCallback?.Invoke($"[Method 2] ✗ Subprocess error: {ex.Message}");
                    LogManager.LogError($"[RemoteScriptManager] Subprocess failed for {targetComputer}", ex);
                    return result;
                }
            });
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Tests whether a TCP port is reachable within the given timeout.</summary>
        public static async Task<bool> IsPortOpenAsync(string host, int port, int timeoutMs = 3000)
        {
            try
            {
                using (var tcp = new TcpClient())
                {
                    var connectTask = tcp.ConnectAsync(host, port);
                    if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)).ConfigureAwait(false) == connectTask
                        && !connectTask.IsFaulted)
                    {
                        return true;
                    }
                }
            }
            catch { /* unreachable */ }
            return false;
        }

        /// <summary>
        /// Quick check: returns true if the target has WinRM listening on port 5985.
        /// Use this to show a hint in the UI before attempting deployment.
        /// </summary>
        public static Task<bool> IsWinRMAvailableAsync(string targetComputer)
            => IsPortOpenAsync(targetComputer, WinRmPort, timeoutMs: 3000);

        /// <summary>
        /// Formats a RemoteScriptResult into a human-readable summary for the terminal.
        /// </summary>
        public static string FormatResult(RemoteScriptResult result, string actionName, string targetComputer)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"\n{'─', -60}");
            sb.AppendLine($"  {actionName} on {targetComputer}");
            sb.AppendLine($"  Method : {result.MethodUsed}");
            sb.AppendLine($"  Status : {(result.Success ? "✓ SUCCESS" : "✗ FAILED")}");
            sb.AppendLine($"  Time   : {result.Elapsed.TotalSeconds:F1}s");
            if (!string.IsNullOrWhiteSpace(result.Output))
            {
                sb.AppendLine($"  Output :");
                foreach (var line in result.Output.Split('\n'))
                    if (!string.IsNullOrWhiteSpace(line))
                        sb.AppendLine($"    {line.TrimEnd()}");
            }
            if (result.Errors.Any())
            {
                sb.AppendLine($"  Errors :");
                foreach (var err in result.Errors)
                    sb.AppendLine($"    ⚠ {err}");
            }
            sb.AppendLine($"{'─', -60}");
            return sb.ToString();
        }
    }
}
