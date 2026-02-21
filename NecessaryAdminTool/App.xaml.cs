using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
// TAG: #APPLICATION_STARTUP #FIRST_RUN_SETUP #DPI_INITIALIZATION #AUTO_SCAN

namespace NecessaryAdminTool
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// TAG: #APPLICATION_STARTUP #FIRST_RUN_SETUP #DPI_INITIALIZATION
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // TAG: #DPI_COMPREHENSIVE_FIX - Industry-standard WPF DPI fix for high-DPI displays
            // Addresses issues with non-integer scaling (125%, 175%, etc.)

            // FIX 1: Disable WPF hardware acceleration (prevents bitmap scaling artifacts at 175% DPI)
            // This forces software rendering which is slower but eliminates duplication/overlay bugs
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
            LogManager.LogInfo("[DPI] Hardware acceleration disabled - using software rendering for DPI stability");

            // FIX 2: Set text formatting mode to Display (better for high-DPI, prevents blurry text)
            // Display mode uses GDI-compatible rendering which handles fractional scaling better
            System.Windows.Media.TextOptions.TextFormattingModeProperty.OverrideMetadata(
                typeof(System.Windows.Controls.Control),
                new FrameworkPropertyMetadata(System.Windows.Media.TextFormattingMode.Display));
            LogManager.LogInfo("[DPI] Text formatting mode set to Display for crisp high-DPI text");

            // FIX 3: Force DPI context initialization before any windows are created
            // This prevents intermittent DPI scaling issues caused by race conditions
            try
            {
                // Force WPF to initialize its DPI awareness context immediately
                var dpiScale = VisualTreeHelper.GetDpi(this.MainWindow ?? new System.Windows.Window());
                LogManager.LogInfo($"[DPI] Initialized DPI context: {dpiScale.DpiScaleX * 100}% horizontal, {dpiScale.DpiScaleY * 100}% vertical");

                // Warn if using problematic fractional scaling
                if (dpiScale.DpiScaleX != 1.0 && dpiScale.DpiScaleX != 1.25 && dpiScale.DpiScaleX != 1.5 && dpiScale.DpiScaleX != 2.0)
                {
                    LogManager.LogWarning($"[DPI] Non-standard DPI scale detected: {dpiScale.DpiScaleX * 100}% - using software rendering for stability");
                }
            }
            catch
            {
                // If MainWindow isn't ready yet, create a temporary window to force DPI initialization
                try
                {
                    var tempWindow = new System.Windows.Window { Width = 0, Height = 0, WindowStyle = WindowStyle.None, ShowInTaskbar = false };
                    tempWindow.Show();
                    var dpiScale = VisualTreeHelper.GetDpi(tempWindow);
                    LogManager.LogInfo($"[DPI] Initialized DPI context via temp window: {dpiScale.DpiScaleX * 100}% horizontal, {dpiScale.DpiScaleY * 100}% vertical");
                    tempWindow.Close();
                }
                catch (Exception innerEx)
                {
                    LogManager.LogWarning($"[DPI] Could not pre-initialize DPI context: {innerEx.Message}");
                }
            }

            // Check for command-line arguments
            if (e.Args.Length > 0)
            {
                foreach (var arg in e.Args)
                {
                    if (arg.Equals("/autoscan", StringComparison.OrdinalIgnoreCase))
                    {
                        // TAG: #AUTO_SCAN #SCHEDULED_TASK
                        // Run automatic scan headlessly (called by Windows Task Scheduler)
                        // Runs on thread pool to avoid WPF dispatcher deadlocks
                        // Blocks main thread until scan completes, then exits cleanly
                        LogManager.LogInfo("Auto-scan triggered by scheduled task");
                        int exitCode = 0;
                        Task.Run(async () =>
                        {
                            try { await RunAutomaticScanAsync(); }
                            catch (Exception ex) { LogManager.LogError("[AutoScan] Fatal error in auto-scan", ex); exitCode = 1; }
                        }).Wait();
                        Environment.Exit(exitCode);
                        return;
                    }
                }
            }

            // Check if first-run setup is needed
            // TAG: #DEBUG_BYPASS - Hold CTRL+SHIFT during startup to skip setup (DEBUG builds only)
            #if DEBUG
            bool debugBypass = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0 &&
                               (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0;

            if (debugBypass)
            {
                LogManager.LogWarning("DEBUG MODE: Setup wizard bypassed via CTRL+SHIFT - marking setup as complete");
                NecessaryAdminTool.Properties.Settings.Default.SetupCompleted = true;
                NecessaryAdminTool.Properties.Settings.Default.Save();
            }
            #endif

            if (!NecessaryAdminTool.Properties.Settings.Default.SetupCompleted)
            {
                LogManager.LogInfo("First run detected - launching Setup Wizard");

                var setupWizard = new SetupWizardWindow();
                var result = setupWizard.ShowDialog();

                if (result != true)
                {
                    // User cancelled setup - exit application
                    LogManager.LogWarning("Setup wizard cancelled by user - exiting application");
                    Shutdown();
                    return;
                }

                LogManager.LogInfo("Setup wizard completed successfully");
            }

            // Continue with normal application startup
            // MainWindow will be shown automatically via StartupUri in App.xaml
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AUTOMATIC BACKGROUND SCAN ENGINE
        // TAG: #AUTO_SCAN #SCHEDULED_TASK #ENTERPRISE #ALL_DATABASE_TYPES
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Entry point for headless scheduled-task scan.
        /// Uses integrated Windows authentication (Kerberos token of the service account).
        /// Works with ALL database types: SQLite, SQL Server, Access, CSV.
        /// TAG: #AUTO_SCAN #HEADLESS #KERBEROS
        /// </summary>
        private async Task RunAutomaticScanAsync()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var startTime = DateTime.Now;
            int scanned = 0, online = 0, offline = 0, failed = 0;

            LogManager.LogInfo("=== AUTOMATIC SCAN STARTED ===");
            LogManager.LogInfo($"[AutoScan] Identity: {Environment.UserDomainName}\\{Environment.UserName}");
            LogManager.LogInfo($"[AutoScan] Database type: {NecessaryAdminTool.Properties.Settings.Default.DatabaseType ?? "(not set)"}");

            // ── Step 1: Verify database is configured ────────────────────────
            if (!Data.DataProviderFactory.VerifyConfiguration())
            {
                LogManager.LogError("[AutoScan] Database not configured. Run the setup wizard first.");
                return;
            }

            // ── Step 2: Discover domain and domain controller ────────────────
            string domainFqdn = null;
            string domainController = null;

            try
            {
                LogManager.LogInfo("[AutoScan] Discovering domain controller...");
                var domain = System.DirectoryServices.ActiveDirectory.Domain.GetCurrentDomain();
                domainFqdn = domain.Name;
                domainController = domain.FindDomainController()?.Name;

                if (string.IsNullOrEmpty(domainController))
                {
                    LogManager.LogError("[AutoScan] Could not find a domain controller for domain: " + domainFqdn);
                    return;
                }

                LogManager.LogInfo($"[AutoScan] Domain: {domainFqdn}  |  DC: {domainController}");
            }
            catch (Exception dcEx)
            {
                LogManager.LogError("[AutoScan] Domain discovery failed - machine must be domain-joined and online.", dcEx);
                return;
            }

            // ── Step 3: Query AD for all computers ───────────────────────────
            List<ADComputer> adComputers;

            using (var adManager = new ActiveDirectoryManager(domainController))
            {
                if (!adManager.Initialize(out string adError))
                {
                    LogManager.LogError($"[AutoScan] AD connection failed: {adError}");
                    return;
                }

                LogManager.LogInfo("[AutoScan] Querying Active Directory for all computers (paged, no limit)...");
                using (var adCts = new CancellationTokenSource(TimeSpan.FromMinutes(10)))
                {
                    adComputers = await adManager.GetComputersAsync(ct: adCts.Token);
                }
                LogManager.LogInfo($"[AutoScan] AD returned {adComputers.Count} computer accounts");
            }

            if (adComputers == null || adComputers.Count == 0)
            {
                LogManager.LogWarning("[AutoScan] No computer accounts found in AD - aborting scan");
                return;
            }

            // ── Step 4: Scan computers and save to database ──────────────────
            using (var provider = await Data.DataProviderFactory.CreateProviderAsync())
            {
                LogManager.LogInfo($"[AutoScan] Scanning {adComputers.Count} computers (max 20 parallel)...");

                // Concurrency limit: 20 parallel ping/WMI operations
                using (var semaphore = new SemaphoreSlim(20, 20))
                using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30))) // overall timeout
                {
                    var tasks = adComputers.Select(async adComputer =>
                    {
                        await semaphore.WaitAsync(cts.Token);
                        try
                        {
                            return await ScanSingleComputerAsync(adComputer, domainFqdn);
                        }
                        catch (OperationCanceledException)
                        {
                            return null;
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    var results = await Task.WhenAll(tasks);

                    // Persist results
                    foreach (var computerInfo in results)
                    {
                        if (computerInfo == null) continue;
                        scanned++;

                        try
                        {
                            await provider.SaveComputerAsync(computerInfo);

                            if (computerInfo.Status == "ONLINE") online++;
                            else offline++;
                        }
                        catch (Exception saveEx)
                        {
                            LogManager.LogError($"[AutoScan] DB save failed for {computerInfo.Hostname}", saveEx);
                            failed++;
                        }
                    }
                }

                // ── Step 5: Record scan history ──────────────────────────────
                sw.Stop();
                try
                {
                    var scanHistory = new Data.ScanHistory
                    {
                        StartTime = startTime,
                        EndTime = DateTime.Now,
                        ComputersScanned = scanned,
                        SuccessCount = online,
                        FailureCount = offline + failed,
                        DurationSeconds = sw.Elapsed.TotalSeconds
                    };

                    await provider.SaveScanHistoryAsync(scanHistory);
                    LogManager.LogInfo($"[AutoScan] Scan history saved to database");
                }
                catch (Exception histEx)
                {
                    LogManager.LogError("[AutoScan] Failed to save scan history (non-fatal)", histEx);
                }
            }

            LogManager.LogInfo($"=== AUTOMATIC SCAN COMPLETED ===");
            LogManager.LogInfo($"[AutoScan] Results: {scanned} total  |  {online} online  |  {offline} offline  |  {failed} DB errors");
            LogManager.LogInfo($"[AutoScan] Duration: {sw.Elapsed.TotalSeconds:F1}s ({sw.Elapsed:hh\\:mm\\:ss})");

            // Write a scan summary to the deployment log directory for review
            TryWriteScanSummary(startTime, scanned, online, offline, sw.Elapsed);
        }

        /// <summary>
        /// Scan a single computer: ping for status, WMI for hardware details.
        /// Uses integrated authentication (current Kerberos token) — no stored passwords needed.
        /// TAG: #AUTO_SCAN #PING #WMI #INTEGRATED_AUTH
        /// </summary>
        private async Task<Data.ComputerInfo> ScanSingleComputerAsync(ADComputer adComputer, string domain)
        {
            // Prefer DNS hostname for resolution; fall back to SAM name.
            // Trim() is mandatory — AD DNSHostName fields occasionally carry trailing whitespace
            // which silently breaks both Ping and WMI, causing false OFFLINE readings.
            string targetHost = (!string.IsNullOrWhiteSpace(adComputer.DNSHostName)
                ? adComputer.DNSHostName
                : adComputer.Name)?.Trim();

            var info = new Data.ComputerInfo
            {
                Hostname    = adComputer.Name,
                OS          = adComputer.OperatingSystem ?? string.Empty,
                OSVersion   = adComputer.OperatingSystemVersion ?? string.Empty,
                Domain      = domain,
                LastSeen    = DateTime.Now,
                Status      = "UNKNOWN",
                IPAddress   = string.Empty,
                Notes       = adComputer.Description ?? string.Empty
            };

            try
            {
                // ── Ping ────────────────────────────────────────────────────
                // Use SendPingAsync (truly async I/O completion port, no thread blocked)
                // instead of Task.Run(ping.Send) which burns a ThreadPool thread per host.
                // At 20 concurrent pings, Task.Run would starve the pool; SendPingAsync does not.
                PingReply reply = null;
                try
                {
                    using (var ping = new Ping())
                    {
                        reply = await ping.SendPingAsync(targetHost, 2000); // 2s timeout, truly async
                    }
                }
                catch { /* host unreachable or DNS failure - leave reply null */ }

                if (reply?.Status == IPStatus.Success)
                {
                    info.Status    = "ONLINE";
                    info.IPAddress = reply.Address?.ToString() ?? string.Empty;
                    info.LastSeen  = DateTime.Now;

                    LogManager.LogDebug($"[AutoScan] {adComputer.Name}: ONLINE ({reply.RoundtripTime}ms, {info.IPAddress})");

                    // ── WMI enrichment (best-effort, non-fatal) ─────────────
                    await TryEnrichWithWmiAsync(info, targetHost);
                }
                else
                {
                    info.Status = "OFFLINE";
                    LogManager.LogDebug($"[AutoScan] {adComputer.Name}: OFFLINE");
                }
            }
            catch (Exception ex)
            {
                info.Status = "OFFLINE";
                LogManager.LogDebug($"[AutoScan] {adComputer.Name}: scan error - {ex.Message}");
            }

            return info;
        }

        /// <summary>
        /// Enrich a ComputerInfo with OS/hardware data via WMI using integrated authentication.
        /// This is best-effort: failures are logged at DEBUG level and never abort the scan.
        /// TAG: #AUTO_SCAN #WMI #INTEGRATED_AUTH #BEST_EFFORT
        /// </summary>
        private async Task TryEnrichWithWmiAsync(Data.ComputerInfo info, string hostname)
        {
            // Strategy 0: NecessaryAdminAgent — no WMI firewall ports required.
            // NatAgentClient already checks token; returns null if token not configured.
            // TAG: #NAT_AGENT #AUTO_SCAN
            try
            {
                var agentInfo = await Managers.NatAgentClient.GetSystemInfoAsync(hostname).ConfigureAwait(false);
                if (agentInfo != null)
                {
                    LogManager.LogDebug($"[AutoScan] {info.Hostname}: Agent hit — skipping WMI enrichment");
                    if (!string.IsNullOrEmpty(agentInfo.OS))           info.OS = agentInfo.OS;
                    if (!string.IsNullOrEmpty(agentInfo.Build))        info.OSVersion = agentInfo.Build;
                    if (!string.IsNullOrEmpty(agentInfo.Serial))       info.SerialNumber = agentInfo.Serial;
                    if (!string.IsNullOrEmpty(agentInfo.Manufacturer)) info.Manufacturer = agentInfo.Manufacturer;
                    if (!string.IsNullOrEmpty(agentInfo.Model))        info.Model = agentInfo.Model;
                    if (!string.IsNullOrEmpty(agentInfo.Processor))    info.CPU = agentInfo.Processor;
                    if (!string.IsNullOrEmpty(agentInfo.LoggedInUser)) info.LastLoggedOnUser = agentInfo.LoggedInUser;
                    if (double.TryParse(agentInfo.TotalRamGB, out double ramGb))
                        info.RAM_GB = (int)Math.Round(ramGb);
                    if (double.TryParse(agentInfo.DiskTotalGB, out double dskGb))
                        info.DiskSize_GB = (int)Math.Round(dskGb);
                    if (double.TryParse(agentInfo.DiskFreeGB, out double freeGb))
                        info.DiskFree_GB = (int)Math.Round(freeGb);
                    if (!string.IsNullOrEmpty(agentInfo.LastBoot) &&
                        DateTime.TryParse(agentInfo.LastBoot, out DateTime boot))
                    {
                        info.LastBootTime = boot;
                        info.Uptime = (long)(DateTime.Now - boot).TotalSeconds;
                    }
                    return; // enrichment complete — skip WMI
                }
                LogManager.LogDebug($"[AutoScan] {info.Hostname}: Agent null — falling back to WMI");
            }
            catch (Exception agentEx)
            {
                LogManager.LogDebug($"[AutoScan] {info.Hostname}: Agent exception — falling back to WMI: {agentEx.Message}");
            }

            await Task.Run(() =>
            {
                try
                {
                    // Integrated auth (no explicit credentials) — uses current Kerberos token
                    var connOptions = new ConnectionOptions
                    {
                        Timeout          = TimeSpan.FromSeconds(10),
                        EnablePrivileges = true,
                        Authentication   = AuthenticationLevel.PacketPrivacy,
                        Impersonation    = ImpersonationLevel.Impersonate
                    };

                    // Note: ManagementScope does not implement IDisposable in .NET Framework.
                    // The connection is released automatically when the scope falls out of scope
                    // and the GC collects it. ManagementObjectSearcher and ManagementObject DO
                    // implement IDisposable and are explicitly disposed below.
                    var scope = new ManagementScope($@"\\{hostname}\root\cimv2", connOptions);

                    try { scope.Connect(); }
                    catch (Exception connEx)
                    {
                        LogManager.LogDebug($"[AutoScan] {info.Hostname}: WMI connect failed ({connEx.Message})");
                        return;
                    }

                    {
                        // local block — scope falls out at closing brace, eligible for GC

                        // Shared EnumerationOptions: 5s per-query timeout + block until results ready.
                        // Without this, a single slow host can stall at the default ~30s WMI timeout.
                        // 5s is generous for LAN; lower to 3s for very large fleets.
                        var enumOpts = new EnumerationOptions
                        {
                            Timeout         = TimeSpan.FromSeconds(5),
                            ReturnImmediately = false   // block until provider returns all rows
                        };

                        // ── Win32_OperatingSystem: OS name, version, last boot ──
                        try
                        {
                            using (var mos = new ManagementObjectSearcher(scope,
                                new ObjectQuery("SELECT Caption, Version, LastBootUpTime FROM Win32_OperatingSystem"),
                                enumOpts))
                            {
                                foreach (ManagementObject obj in mos.Get())
                                using (obj) // dispose COM object immediately after reading
                                {
                                    if (obj["Caption"] is string caption && !string.IsNullOrEmpty(caption))
                                        info.OS = caption.Trim();
                                    if (obj["Version"] is string ver && !string.IsNullOrEmpty(ver))
                                        info.OSVersion = ver;
                                    if (obj["LastBootUpTime"] is string bootTime)
                                    {
                                        try
                                        {
                                            info.LastBootTime = ManagementDateTimeConverter.ToDateTime(bootTime);
                                            info.Uptime = (long)(DateTime.Now - info.LastBootTime.Value).TotalSeconds;
                                        }
                                        catch { /* non-fatal datetime parse */ }
                                    }
                                    break;
                                }
                            }
                        }
                        catch (Exception ex) { LogManager.LogDebug($"[AutoScan] {info.Hostname}: Win32_OperatingSystem - {ex.Message}"); }

                        // ── Win32_ComputerSystem: RAM, last logged-on user, make/model ──
                        try
                        {
                            using (var mos = new ManagementObjectSearcher(scope,
                                new ObjectQuery("SELECT TotalPhysicalMemory, UserName, Manufacturer, Model FROM Win32_ComputerSystem"),
                                enumOpts))
                            {
                                foreach (ManagementObject obj in mos.Get())
                                using (obj)
                                {
                                    if (obj["TotalPhysicalMemory"] is ulong ram)
                                        info.RAM_GB = (int)(ram / (1024UL * 1024UL * 1024UL));
                                    info.LastLoggedOnUser = obj["UserName"]?.ToString() ?? string.Empty;
                                    info.Manufacturer     = obj["Manufacturer"]?.ToString()?.Trim() ?? string.Empty;
                                    info.Model            = obj["Model"]?.ToString()?.Trim() ?? string.Empty;
                                    break;
                                }
                            }
                        }
                        catch (Exception ex) { LogManager.LogDebug($"[AutoScan] {info.Hostname}: Win32_ComputerSystem - {ex.Message}"); }

                        // ── Win32_Processor: CPU name ────────────────────────────
                        try
                        {
                            using (var mos = new ManagementObjectSearcher(scope,
                                new ObjectQuery("SELECT Name FROM Win32_Processor"),
                                enumOpts))
                            {
                                foreach (ManagementObject obj in mos.Get())
                                using (obj)
                                {
                                    info.CPU = obj["Name"]?.ToString()?.Trim() ?? string.Empty;
                                    break; // first processor is enough
                                }
                            }
                        }
                        catch (Exception ex) { LogManager.LogDebug($"[AutoScan] {info.Hostname}: Win32_Processor - {ex.Message}"); }

                        // ── Win32_LogicalDisk (C:): disk size and free space ─────
                        try
                        {
                            using (var mos = new ManagementObjectSearcher(scope,
                                new ObjectQuery("SELECT Size, FreeSpace FROM Win32_LogicalDisk WHERE DeviceID='C:'"),
                                enumOpts))
                            {
                                foreach (ManagementObject obj in mos.Get())
                                using (obj)
                                {
                                    if (obj["Size"] is ulong sz)
                                        info.DiskSize_GB = (int)(sz / (1024UL * 1024UL * 1024UL));
                                    if (obj["FreeSpace"] is ulong fr)
                                        info.DiskFree_GB = (int)(fr / (1024UL * 1024UL * 1024UL));
                                    break;
                                }
                            }
                        }
                        catch (Exception ex) { LogManager.LogDebug($"[AutoScan] {info.Hostname}: Win32_LogicalDisk - {ex.Message}"); }

                        // ── Win32_BIOS: serial number ────────────────────────────
                        try
                        {
                            using (var mos = new ManagementObjectSearcher(scope,
                                new ObjectQuery("SELECT SerialNumber FROM Win32_BIOS"),
                                enumOpts))
                            {
                                foreach (ManagementObject obj in mos.Get())
                                using (obj)
                                {
                                    info.SerialNumber = obj["SerialNumber"]?.ToString()?.Trim() ?? string.Empty;
                                    break;
                                }
                            }
                        }
                        catch (Exception ex) { LogManager.LogDebug($"[AutoScan] {info.Hostname}: Win32_BIOS - {ex.Message}"); }

                        LogManager.LogDebug($"[AutoScan] {info.Hostname}: WMI complete — OS={info.OS}, RAM={info.RAM_GB}GB, CPU={info.CPU}");
                    } // scope goes out of block here — eligible for GC / WMI cleanup
                }
                catch (Exception ex)
                {
                    LogManager.LogDebug($"[AutoScan] {info.Hostname}: WMI skipped — {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Write a plain-text scan summary to the deployment log directory for review.
        /// Non-fatal: silently skips if the directory is inaccessible.
        /// TAG: #AUTO_SCAN #LOGGING #DEPLOYMENT_LOG
        /// </summary>
        private void TryWriteScanSummary(DateTime startTime, int scanned, int online, int offline, TimeSpan elapsed)
        {
            try
            {
                string logDir = NecessaryAdminTool.Properties.Settings.Default.DeploymentLogDirectory;
                if (string.IsNullOrWhiteSpace(logDir))
                {
                    string dbPath = NecessaryAdminTool.Properties.Settings.Default.DatabasePath;
                    logDir = !string.IsNullOrWhiteSpace(dbPath)
                        ? dbPath
                        : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NecessaryAdminTool");
                }

                System.IO.Directory.CreateDirectory(logDir);

                string logFile = System.IO.Path.Combine(logDir, $"AutoScan_{startTime:yyyyMMdd_HHmmss}.txt");
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("========================================");
                sb.AppendLine("  NecessaryAdminTool - Auto Scan Report");
                sb.AppendLine("========================================");
                sb.AppendLine($"  Date:        {startTime:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"  Identity:    {Environment.UserDomainName}\\{Environment.UserName}");
                sb.AppendLine($"  Duration:    {elapsed:hh\\:mm\\:ss} ({elapsed.TotalSeconds:F1}s)");
                sb.AppendLine($"  Database:    {NecessaryAdminTool.Properties.Settings.Default.DatabaseType}");
                sb.AppendLine("----------------------------------------");
                sb.AppendLine($"  Scanned:     {scanned}");
                sb.AppendLine($"  Online:      {online}");
                sb.AppendLine($"  Offline:     {offline}");
                sb.AppendLine("========================================");

                // FileShare.Read: DeploymentLogDirectory may be a network share
                using (var fs = new System.IO.FileStream(logFile, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read))
                using (var sw = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8))
                    sw.Write(sb.ToString());
                LogManager.LogInfo($"[AutoScan] Summary written to: {logFile}");
            }
            catch (Exception ex)
            {
                LogManager.LogDebug($"[AutoScan] Could not write scan summary (non-fatal): {ex.Message}");
            }
        }
    }
}
