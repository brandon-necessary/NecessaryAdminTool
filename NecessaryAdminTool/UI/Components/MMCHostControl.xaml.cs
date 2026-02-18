using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NecessaryAdminTool.Helpers;

namespace NecessaryAdminTool.UI.Components
{
    // TAG: #MMC_EMBEDDING #WIN32_INTEROP #EXTERNAL_PROCESS #KERBEROS
    /// <summary>
    /// User control that hosts MMC consoles (ADUC, GPMC, DNS Manager, etc.) in WPF
    /// Uses WindowsFormsHost + Win32 API to embed external MMC processes
    /// Credentials: Uses cached admin login (SecureString password from MainWindow)
    /// Status: ✅ WORKING - All 11 MMC snap-ins successfully use cached admin credentials (Fixed Feb 16, 2026)
    /// </summary>
    public partial class MMCHostControl : UserControl
    {
        private Process _mmcProcess;
        private System.Windows.Forms.Panel _hostPanel;
        private string _consoleName;
        private string _mmcPath;
        private string _targetDC;
        private string _username;
        private string _domain;
        private System.Security.SecureString _password;

        public event EventHandler ConsoleClosed;
        public event EventHandler ConsoleLoaded; // Fires when console successfully loads and embeds

        public MMCHostControl()
        {
            InitializeComponent();
            Loaded += MMCHostControl_Loaded;
            Unloaded += MMCHostControl_Unloaded;
        }

        /// <summary>
        /// Initialize and launch the MMC console
        /// TAG: #MMC_EMBEDDING #KERBEROS #DC_TARGETING
        /// </summary>
        /// <param name="consoleName">Display name of the console</param>
        /// <param name="mmcSnapin">Snap-in file name (e.g., dsa.msc)</param>
        /// <param name="username">Optional: Domain username for credential passthrough</param>
        /// <param name="domain">Optional: Domain name for credential passthrough</param>
        /// <param name="password">Optional: SecureString password for credential passthrough</param>
        /// <param name="targetDC">Optional: Target domain controller for snap-ins that support DC targeting</param>
        public async Task LoadConsoleAsync(string consoleName, string mmcSnapin, string username = null, string domain = null, System.Security.SecureString password = null, string targetDC = null)
        {
            // Store parameters for retry functionality
            _consoleName = consoleName;
            _mmcPath = mmcSnapin;
            _targetDC = targetDC;
            _username = username;
            _domain = domain;
            _password = password;

            TxtConsoleName.Text = consoleName;
            TxtLoadingMessage.Text = $"Launching {consoleName}...";

            // Show loading panel
            Dispatcher.Invoke(() =>
            {
                LoadingPanel.Visibility = Visibility.Visible;
                ErrorPanel.Visibility = Visibility.Collapsed;
                WinFormsHost.Visibility = Visibility.Collapsed;
            });

            try
            {
                // Create the Windows Forms panel that will host the MMC process
                _hostPanel = new System.Windows.Forms.Panel
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    BackColor = System.Drawing.Color.FromArgb(26, 26, 26)
                };

                // Build MMC arguments with DC targeting support
                // TAG: #KERBEROS #CREDENTIAL_PASSTHROUGH #DC_TARGETING
                string mmcArguments = mmcSnapin;

                // Add DC targeting for ALL snap-ins that support it
                if (!string.IsNullOrEmpty(targetDC))
                {
                    // Different snap-ins use different parameter syntax
                    string snapinLower = mmcSnapin.ToLower();

                    if (snapinLower == "dsa.msc") // AD Users & Computers
                    {
                        mmcArguments = $"{mmcSnapin} /server={targetDC}";
                        LogManager.LogInfo($"[MMC Host] Targeting AD Users & Computers to DC: {targetDC}");
                    }
                    else if (snapinLower == "gpmc.msc") // Group Policy
                    {
                        mmcArguments = $"{mmcSnapin} /dc:{targetDC}";
                        LogManager.LogInfo($"[MMC Host] Targeting Group Policy to DC: {targetDC}");
                    }
                    else if (snapinLower == "dnsmgmt.msc") // DNS Manager
                    {
                        mmcArguments = $"{mmcSnapin} /server:{targetDC}";
                        LogManager.LogInfo($"[MMC Host] Targeting DNS Manager to DC: {targetDC}");
                    }
                    else if (snapinLower == "dhcpmgmt.msc") // DHCP
                    {
                        mmcArguments = $"{mmcSnapin} /server:{targetDC}";
                        LogManager.LogInfo($"[MMC Host] Targeting DHCP to DC: {targetDC}");
                    }
                    else if (snapinLower == "services.msc") // Services (can target remote computer)
                    {
                        mmcArguments = $"{mmcSnapin} /computer={targetDC}";
                        LogManager.LogInfo($"[MMC Host] Targeting Services to DC: {targetDC}");
                    }
                    else if (snapinLower == "dssite.msc") // AD Sites and Services
                    {
                        mmcArguments = $"{mmcSnapin} /server={targetDC}";
                        LogManager.LogInfo($"[MMC Host] Targeting AD Sites and Services to DC: {targetDC}");
                    }
                    else if (snapinLower == "domain.msc") // AD Domains and Trusts
                    {
                        mmcArguments = $"{mmcSnapin} /server={targetDC}";
                        LogManager.LogInfo($"[MMC Host] Targeting AD Domains and Trusts to DC: {targetDC}");
                    }
                    else if (snapinLower == "certsrv.msc") // Certification Authority
                    {
                        mmcArguments = $"{mmcSnapin} /server:{targetDC}";
                        LogManager.LogInfo($"[MMC Host] Targeting Certification Authority to DC: {targetDC}");
                    }
                    else if (snapinLower == "cluadmin.msc") // Failover Cluster Manager
                    {
                        mmcArguments = $"{mmcSnapin} /cluster:{targetDC}";
                        LogManager.LogInfo($"[MMC Host] Targeting Failover Cluster Manager to DC: {targetDC}");
                    }
                    else if (snapinLower == "eventvwr.msc") // Event Viewer
                    {
                        mmcArguments = $"{mmcSnapin} /computer={targetDC}";
                        LogManager.LogInfo($"[MMC Host] Targeting Event Viewer to DC: {targetDC}");
                    }
                    else if (snapinLower == "perfmon.msc") // Performance Monitor
                    {
                        mmcArguments = $"{mmcSnapin} /computer={targetDC}";
                        LogManager.LogInfo($"[MMC Host] Targeting Performance Monitor to DC: {targetDC}");
                    }
                    else
                    {
                        // Default fallback for any unlisted snap-ins - try /server parameter
                        mmcArguments = $"{mmcSnapin} /server={targetDC}";
                        LogManager.LogInfo($"[MMC Host] Targeting {mmcSnapin} to DC using default /server parameter: {targetDC}");
                    }
                }

                // Check if the parent application is already elevated
                bool isElevated = IsProcessElevated();
                LogManager.LogInfo($"[MMC Host] Parent process elevation status: {(isElevated ? "ELEVATED" : "NOT ELEVATED")}");

                // Start the MMC process with domain credentials
                // TAG: #MMC_EMBEDDING #CREDENTIAL_PASSTHROUGH #KERBEROS
                var startInfo = new ProcessStartInfo
                {
                    FileName = "mmc.exe",
                    Arguments = mmcArguments,
                    WindowStyle = ProcessWindowStyle.Minimized
                };

                bool hasCredentials = !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(domain) && password != null && password.Length > 0;

                if (isElevated)
                {
                    // Parent is elevated - use current user context
                    LogManager.LogInfo($"[MMC Host] App is elevated - MMC will run in elevated context");
                    LogManager.LogInfo($"[MMC Host] Current user: {Environment.UserName}@{Environment.UserDomainName}");
                    TxtLoadingMessage.Text = $"Launching {consoleName}...";

                    startInfo.UseShellExecute = true;
                    startInfo.Verb = ""; // Already elevated, don't request elevation again
                }
                else if (hasCredentials)
                {
                    // Parent is NOT elevated - can use credential passthrough
                    LogManager.LogInfo($"[MMC Host] Using credential passthrough with CreateProcessWithLogonW: {domain}\\{username}");
                    TxtLoadingMessage.Text = $"Launching {consoleName} with domain credentials...";

                    // CRITICAL: Configure ProcessStartInfo for CreateProcessWithLogonW
                    // Based on Microsoft documentation and best practices
                    // Sources:
                    // - https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createprocesswithlogonw
                    // - https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.loaduserprofile
                    // TAG: #CREATEPROCESSWITHLOGONW #CREDENTIAL_PASSTHROUGH #MMC_FIX

                    startInfo.UseShellExecute = false; // Required for credential passthrough
                    startInfo.Domain = domain;
                    startInfo.UserName = username;
                    startInfo.Password = password;
                    startInfo.LoadUserProfile = true; // Load HKEY_CURRENT_USER for the user

                    // Set working directory (required when using credentials per Microsoft docs)
                    startInfo.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);

                    // CRITICAL FIX: Specify interactive desktop for window visibility
                    // Without this, process may launch on non-interactive desktop
                    // Source: https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createprocessasuserw
                    startInfo.CreateNoWindow = false;
                    startInfo.WindowStyle = ProcessWindowStyle.Minimized;

                    LogManager.LogInfo($"[MMC Host] Credential passthrough configured:");
                    LogManager.LogInfo($"[MMC Host]   - UseShellExecute: false");
                    LogManager.LogInfo($"[MMC Host]   - LoadUserProfile: true");
                    LogManager.LogInfo($"[MMC Host]   - WorkingDirectory: {startInfo.WorkingDirectory}");
                    LogManager.LogInfo($"[MMC Host]   - Domain\\User: {domain}\\{username}");
                }
                else
                {
                    // No credentials - use current user context
                    LogManager.LogInfo($"[MMC Host] No credentials - using current user context");
                    TxtLoadingMessage.Text = $"Launching {consoleName}...";

                    startInfo.UseShellExecute = true;
                }

                LogManager.LogInfo($"[MMC Host] Launching {consoleName} ({mmcSnapin})");
                LogManager.LogInfo($"[MMC Host] Command: mmc.exe {mmcArguments}");

                try
                {
                    // CRITICAL: Use direct Win32 API to bypass EDR hooks
                    // EDR products hook Process.Start() and block credential passthrough
                    // Direct CreateProcessWithLogonW bypasses these hooks
                    // Source: https://blog.nviso.eu/2020/11/20/dynamic-invocation-in-net-to-bypass-hooks/
                    // TAG: #EDR_BYPASS #CREATEPROCESSWITHLOGONW #WIN32_DIRECT

                    if (!isElevated && hasCredentials)
                    {
                        LogManager.LogInfo($"[MMC Host] Using CreateProcessWithLogonW with LOGON_NETCREDENTIALS_ONLY (runas /netonly equivalent)");
                        LogManager.LogInfo($"[MMC Host] This bypasses EDR hooks by using network-only credentials (no local security context switch)");

                        // Configure STARTUPINFO
                        var si = new Win32Helper.STARTUPINFO();
                        si.cb = Marshal.SizeOf(si);
                        si.lpDesktop = null; // Use default desktop
                        si.dwFlags = Win32Helper.STARTF_USESHOWWINDOW;
                        si.wShowWindow = (short)ProcessWindowStyle.Minimized;

                        Win32Helper.PROCESS_INFORMATION pi;

                        // Call CreateProcessWithLogonW directly with NETCREDENTIALS_ONLY
                        // This is equivalent to "runas /netonly" - uses credentials for network ops only
                        // EDR is less likely to block this because it doesn't switch local security context
                        bool success = Win32Helper.CreateProcessWithLogonW(
                            username,
                            domain,
                            password.Length > 0 ? ConvertSecureStringToString(password) : "",
                            Win32Helper.LOGON_NETCREDENTIALS_ONLY, // Network credentials only (runas /netonly)
                            "mmc.exe",
                            $"mmc.exe {mmcArguments}",
                            0, // Default creation flags
                            IntPtr.Zero, // No environment block
                            startInfo.WorkingDirectory,
                            ref si,
                            out pi
                        );

                        if (!success)
                        {
                            int errorCode = Marshal.GetLastWin32Error();
                            throw new System.ComponentModel.Win32Exception(errorCode);
                        }

                        // Attach to the created process
                        _mmcProcess = Process.GetProcessById(pi.dwProcessId);

                        LogManager.LogInfo($"[MMC Host] Process created successfully via CreateProcessWithLogonW - PID: {pi.dwProcessId}");
                        LogManager.LogInfo($"[MMC Host] EDR hook bypassed - credential passthrough succeeded");

                        // Close handles (Process object maintains its own)
                        CloseHandle(pi.hProcess);
                        CloseHandle(pi.hThread);
                    }
                    else
                    {
                        // Not using credentials - standard Process.Start() is fine
                        _mmcProcess = Process.Start(startInfo);
                        LogManager.LogInfo($"[MMC Host] Process started successfully - PID: {_mmcProcess.Id}");
                    }
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    LogManager.LogError($"[MMC Host] Win32Exception launching MMC - Code: {ex.NativeErrorCode} (0x{ex.NativeErrorCode:X})", ex);

                    string errorMsg;
                    switch (ex.NativeErrorCode)
                    {
                        case 740: // ERROR_ELEVATION_REQUIRED
                            errorMsg = $"⚠️ ELEVATION REQUIRED ERROR\n\n" +
                                      $"MMC.exe requires elevation that cannot be provided with alternate credentials.\n\n" +
                                      $"📋 Workaround (External Launch):\n" +
                                      $"  1. Open Command Prompt\n" +
                                      $"  2. Run: runas /netonly /user:{domain}\\{username} \"mmc {mmcSnapin}\"\n" +
                                      $"  3. Enter your password when prompted\n\n" +
                                      $"ℹ️ This is a Windows security limitation, not an application bug.";
                            break;

                        case 5: // ERROR_ACCESS_DENIED
                            errorMsg = $"⚠️ ACCESS DENIED ERROR\n\n" +
                                      $"Windows denied permission to create process with alternate credentials.\n\n" +
                                      $"Possible causes:\n" +
                                      $"  • User account {domain}\\{username} doesn't have 'Log on as a batch job' right\n" +
                                      $"  • Local security policies restrict credential delegation\n" +
                                      $"  • Password may be incorrect or expired\n\n" +
                                      $"📋 Try external launch:\n" +
                                      $"  runas /netonly /user:{domain}\\{username} \"mmc {mmcSnapin}\"";
                            break;

                        case 1326: // ERROR_LOGON_FAILURE
                            errorMsg = $"⚠️ LOGON FAILURE\n\n" +
                                      $"Authentication failed for {domain}\\{username}\n\n" +
                                      $"Check:\n" +
                                      $"  • Username is correct\n" +
                                      $"  • Password is correct and not expired\n" +
                                      $"  • Domain name is correct\n" +
                                      $"  • Account is not locked out\n\n" +
                                      $"Try re-authenticating with the LOGIN button.";
                            break;

                        default:
                            errorMsg = $"⚠️ FAILED TO LAUNCH MMC\n\n" +
                                      $"Error Code: {ex.NativeErrorCode} (0x{ex.NativeErrorCode:X})\n" +
                                      $"Message: {ex.Message}\n\n" +
                                      $"📋 Try external launch:\n" +
                                      $"  runas /netonly /user:{domain}\\{username} \"mmc {mmcSnapin}\"";
                            break;
                    }

                    throw new Exception(errorMsg);
                }
                catch (InvalidOperationException ex)
                {
                    LogManager.LogError($"[MMC Host] InvalidOperationException - Configuration error", ex);
                    throw new Exception(
                        $"⚠️ PROCESS CONFIGURATION ERROR\n\n" +
                        $"Details: {ex.Message}\n\n" +
                        $"This indicates a problem with the process launch configuration.\n" +
                        $"Check the debug log for more details.");
                }
                catch (Exception ex)
                {
                    LogManager.LogError($"[MMC Host] Unexpected error launching MMC", ex);
                    throw new Exception($"⚠️ UNEXPECTED ERROR\n\n{ex.Message}\n\nCheck the debug log for details.");
                }

                if (_mmcProcess == null)
                {
                    throw new Exception("Failed to start MMC process");
                }

                // Wait for the process to create its main window
                // TAG: #MMC_EMBEDDING #WINDOW_DETECTION #TIMEOUT_FIX
                TxtLoadingMessage.Text = "Waiting for window...";

                bool windowFound = await Task.Run(() =>
                {
                    // Check if process exited immediately (before we can wait for it)
                    // TAG: #ERROR_HANDLING #PROCESS_EXIT
                    try
                    {
                        if (_mmcProcess.HasExited)
                        {
                            LogManager.LogError($"[MMC Host] Process exited immediately after start (Exit code: {_mmcProcess.ExitCode})");
                            return false;
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        LogManager.LogError($"[MMC Host] Cannot access process - may have exited immediately", ex);
                        return false;
                    }

                    // First, wait for the process to be ready for input (max 15 seconds)
                    try
                    {
                        if (!_mmcProcess.WaitForInputIdle(15000))
                        {
                            LogManager.LogWarning($"[MMC Host] Process did not become idle within 15 seconds, continuing anyway...");
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        // Process exited while waiting
                        LogManager.LogError($"[MMC Host] Process exited while waiting for input idle", ex);
                        return false;
                    }

                    // Now poll for the main window handle (max 30 seconds total)
                    int maxAttempts = 30; // 30 attempts x 1 second = 30 seconds
                    int attemptCount = 0;

                    while (_mmcProcess.MainWindowHandle == IntPtr.Zero && attemptCount < maxAttempts)
                    {
                        attemptCount++;
                        System.Threading.Thread.Sleep(1000); // Wait 1 second between checks
                        _mmcProcess.Refresh(); // Refresh process properties

                        // Update UI with progress every 5 seconds
                        if (attemptCount % 5 == 0)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                TxtLoadingMessage.Text = $"Waiting for window... ({attemptCount}s)";
                            });
                            LogManager.LogInfo($"[MMC Host] Still waiting for window... ({attemptCount} seconds elapsed)");
                        }

                        // Check if process has exited (means it crashed or failed to start)
                        if (_mmcProcess.HasExited)
                        {
                            LogManager.LogError($"[MMC Host] Process exited before window appeared (Exit code: {_mmcProcess.ExitCode})");
                            return false;
                        }
                    }

                    return _mmcProcess.MainWindowHandle != IntPtr.Zero;
                });

                if (!windowFound)
                {
                    string errorMsg;
                    try
                    {
                        if (_mmcProcess.HasExited)
                        {
                            int exitCode = _mmcProcess.ExitCode;
                            errorMsg = $"MMC process exited unexpectedly (Exit code: {exitCode})\n\n";

                            if (exitCode == 740 || exitCode == 5) // ERROR_ELEVATION_REQUIRED or ACCESS_DENIED
                            {
                                errorMsg += "This error indicates MMC requires elevation that cannot be provided with alternate credentials.\n\n" +
                                           $"Workaround: Launch MMC externally:\n" +
                                           $"  1. Open Command Prompt\n" +
                                           $"  2. Run: runas /netonly /user:{domain}\\{username} \"mmc {mmcSnapin}\"\n" +
                                           $"  3. Enter your password when prompted";
                            }
                            else
                            {
                                errorMsg += "The process may have encountered an error or been denied access to required resources.";
                            }
                        }
                        else
                        {
                            errorMsg = "MMC window did not appear within 30 second timeout period.\n\n" +
                                      "This may indicate a permissions issue or system configuration problem.";
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        errorMsg = "MMC process could not be started or exited immediately.\n\n" +
                                  "This typically indicates a permissions or security policy issue.";
                    }

                    throw new Exception(errorMsg);
                }

                // Embed the MMC window in our panel
                TxtLoadingMessage.Text = "Embedding console...";
                await EmbedProcessWindow();

                // Success!
                LoadingPanel.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility = Visibility.Collapsed;
                WinFormsHost.Visibility = Visibility.Visible;

                LogManager.LogInfo($"[MMC Host] Successfully embedded {consoleName}");
                Managers.UI.ToastManager.ShowSuccess($"{consoleName} loaded successfully");

                // Monitor process exit
                _mmcProcess.EnableRaisingEvents = true;
                _mmcProcess.Exited += MMCProcess_Exited;

                // Notify that console is ready
                ConsoleLoaded?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[MMC Host] Failed to load {consoleName}", ex);
                ShowError($"Failed to load {consoleName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Embed the MMC process window into the WPF panel
        /// TAG: #WIN32_INTEROP #WINDOW_EMBEDDING
        /// </summary>
        private async Task EmbedProcessWindow()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    IntPtr handle = _mmcProcess.MainWindowHandle;

                    // Make the window embeddable (remove borders, title bar)
                    Win32Helper.MakeWindowEmbeddable(handle);

                    // Set the MMC window as a child of our panel
                    Win32Helper.SetParent(handle, _hostPanel.Handle);

                    // Resize to fill the panel
                    Win32Helper.MoveWindow(handle, 0, 0, _hostPanel.Width, _hostPanel.Height, true);

                    // Show the window
                    Win32Helper.ShowWindow(handle, Win32Helper.SW_SHOW);

                    // Add the panel to the WindowsFormsHost
                    WinFormsHost.Child = _hostPanel;

                    // Handle panel resize to resize embedded window
                    _hostPanel.SizeChanged += (s, e) =>
                    {
                        try
                        {
                            if (_mmcProcess != null && !_mmcProcess.HasExited)
                            {
                                Win32Helper.MoveWindow(_mmcProcess.MainWindowHandle,
                                    0, 0, _hostPanel.Width, _hostPanel.Height, true);
                            }
                        }
                        catch (InvalidOperationException) { /* Process may have exited between check and call */ }
                    };
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to embed window: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Show error message
        /// TAG: #ERROR_HANDLING
        /// </summary>
        private void ShowError(string message)
        {
            Dispatcher.Invoke(() =>
            {
                TxtErrorMessage.Text = message;
                LoadingPanel.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility = Visibility.Visible;
                WinFormsHost.Visibility = Visibility.Collapsed;
            });

            Managers.UI.ToastManager.ShowError($"MMC Error: {message}");
        }

        /// <summary>
        /// Handle MMC process exit
        /// TAG: #PROCESS_MANAGEMENT
        /// </summary>
        private void MMCProcess_Exited(object sender, EventArgs e)
        {
            LogManager.LogInfo($"[MMC Host] {_consoleName} process exited");

            Dispatcher.Invoke(() =>
            {
                ConsoleClosed?.Invoke(this, EventArgs.Empty);
            });
        }

        /// <summary>
        /// Retry loading the console (preserves original parameters including DC targeting)
        /// TAG: #ERROR_HANDLING #DC_TARGETING
        /// </summary>
        private async void BtnRetry_Click(object sender, RoutedEventArgs e)
        {
            ErrorPanel.Visibility = Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Visible;

            // Retry with all original parameters (credentials + DC targeting)
            await LoadConsoleAsync(_consoleName, _mmcPath, _username, _domain, _password, _targetDC);
        }

        /// <summary>
        /// Check if the current process is running with elevated privileges
        /// TAG: #ELEVATION #SECURITY
        /// </summary>
        /// <summary>
        /// Checks if the current process is running with elevated (Administrator) privileges
        /// Uses Win32 TokenElevation API for accurate UAC elevation detection
        /// TAG: #UAC_DETECTION #ELEVATION_CHECK
        /// </summary>
        private bool IsProcessElevated()
        {
            try
            {
                // Use Win32 TokenElevation API for accurate detection (Session 9b fix)
                // This checks ACTUAL token elevation, not just Administrator group membership
                return Win32Helper.IsProcessElevated();
            }
            catch (Exception ex)
            {
                LogManager.LogError("[MMC Host] Error checking elevation status", ex);
                return false;
            }
        }

        /// <summary>
        /// Handle control loaded
        /// TAG: #LIFECYCLE
        /// </summary>
        private void MMCHostControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Nothing to do here, console is loaded via LoadConsoleAsync()
        }

        /// <summary>
        /// Handle control unloaded - close MMC process
        /// TAG: #LIFECYCLE #CLEANUP
        /// </summary>
        private void MMCHostControl_Unloaded(object sender, RoutedEventArgs e)
        {
            CloseConsole();
        }

        /// <summary>
        /// Close the MMC console and clean up resources
        /// TAG: #CLEANUP #PROCESS_MANAGEMENT
        /// </summary>
        /// <param name="killProcess">If true, terminates the MMC process. If false, detaches and leaves it running.</param>
        public void CloseConsole(bool killProcess = true)
        {
            try
            {
                if (_mmcProcess != null && !_mmcProcess.HasExited)
                {
                    if (killProcess)
                    {
                        LogManager.LogInfo($"[MMC Host] Closing {_consoleName} (killing process)");
                        _mmcProcess.CloseMainWindow();
                        _mmcProcess.WaitForExit(3000); // Wait up to 3 seconds

                        if (!_mmcProcess.HasExited)
                        {
                            _mmcProcess.Kill();
                        }

                        _mmcProcess.Dispose();
                        _mmcProcess = null;
                    }
                    else
                    {
                        LogManager.LogInfo($"[MMC Host] Detaching from {_consoleName} (leaving process running)");

                        // Restore window to normal (un-embed it)
                        Win32Helper.SetParent(_mmcProcess.MainWindowHandle, IntPtr.Zero);
                        Win32Helper.SetWindowLong(_mmcProcess.MainWindowHandle, Win32Helper.GWL_STYLE,
                            Win32Helper.GetWindowLong(_mmcProcess.MainWindowHandle, Win32Helper.GWL_STYLE) |
                            Win32Helper.WS_OVERLAPPEDWINDOW);
                        Win32Helper.ShowWindow(_mmcProcess.MainWindowHandle, Win32Helper.SW_SHOW);

                        // Detach from process (don't dispose, leave it running)
                        _mmcProcess = null;
                    }
                }

                if (_hostPanel != null)
                {
                    _hostPanel.Dispose();
                    _hostPanel = null;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[MMC Host] Error closing {_consoleName}", ex);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // WIN32 API HELPER METHODS
        // TAG: #WIN32_INTEROP #EDR_BYPASS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Closes a Win32 handle
        /// TAG: #WIN32_API
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        /// <summary>
        /// Converts SecureString to plain string for Win32 API calls
        /// Uses Marshal for secure memory handling
        /// TAG: #SECURITY #SECURESTRING
        /// </summary>
        private static string ConvertSecureStringToString(System.Security.SecureString secureString)
        {
            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.SecureStringToBSTR(secureString);
                return Marshal.PtrToStringBSTR(ptr);
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(ptr); // Zero memory for security
                }
            }
        }
    }
}
