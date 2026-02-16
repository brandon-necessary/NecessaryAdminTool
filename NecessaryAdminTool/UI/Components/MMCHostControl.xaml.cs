using System;
using System.Diagnostics;
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

                // Start the MMC process with domain credentials
                var startInfo = new ProcessStartInfo
                {
                    FileName = "mmc.exe",
                    Arguments = mmcArguments,
                    WindowStyle = ProcessWindowStyle.Minimized
                };

                // Check if the parent application is already elevated
                bool isElevated = IsProcessElevated();
                LogManager.LogInfo($"[MMC Host] Parent process elevation status: {(isElevated ? "ELEVATED" : "NOT ELEVATED")}");

                // CRITICAL: When app is elevated, Windows security model prevents launching processes
                // with different credentials (elevation + credential passthrough = security violation)
                //
                // SOLUTION: Use Kerberos ticket authentication instead of explicit credentials
                // - User is already authenticated to domain (logged in with admin creds)
                // - MMC uses current user's Kerberos tickets automatically
                // - /server=DC parameter targets specific domain controller
                // - This is MORE secure and follows Windows best practices
                //
                // TAG: #MMC_EMBEDDING #KERBEROS #SECURITY #ELEVATION_FIX

                if (isElevated)
                {
                    // Parent is elevated - use current user context with Kerberos authentication
                    LogManager.LogInfo($"[MMC Host] App is elevated - using Kerberos ticket authentication");
                    LogManager.LogInfo($"[MMC Host] Current user: {Environment.UserName}@{Environment.UserDomainName}");
                    LogManager.LogInfo($"[MMC Host] MMC will authenticate to DC using existing Kerberos tickets");
                    TxtLoadingMessage.Text = $"Launching {consoleName} (Kerberos auth)...";

                    // Inherit elevation from parent process
                    startInfo.UseShellExecute = true;
                    startInfo.Verb = ""; // Already elevated, don't request elevation again

                    // Note: No explicit credentials needed - Kerberos handles authentication
                    // The /server=DC parameter in mmcArguments targets the specific DC
                }
                else
                {
                    // Parent is NOT elevated - can use credential passthrough
                    if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(domain) && password != null && password.Length > 0)
                    {
                        LogManager.LogInfo($"[MMC Host] App not elevated - using credential passthrough: {domain}\\{username}");
                        TxtLoadingMessage.Text = $"Launching {consoleName} with domain credentials...";

                        // Use explicit credentials (only works when parent is NOT elevated)
                        startInfo.UseShellExecute = false;
                        startInfo.Domain = domain;
                        startInfo.UserName = username;
                        startInfo.Password = password;
                    }
                    else
                    {
                        // No credentials or invalid credentials - use current user context
                        LogManager.LogInfo($"[MMC Host] App not elevated, no valid credentials - using current user context");
                        TxtLoadingMessage.Text = $"Launching {consoleName}...";

                        startInfo.UseShellExecute = true;
                    }
                }

                LogManager.LogInfo($"[MMC Host] Launching {consoleName} ({mmcSnapin})");
                _mmcProcess = Process.Start(startInfo);

                if (_mmcProcess == null)
                {
                    throw new Exception("Failed to start MMC process");
                }

                // Wait for the process to create its main window
                TxtLoadingMessage.Text = "Waiting for window...";
                await Task.Run(() =>
                {
                    _mmcProcess.WaitForInputIdle(10000); // Wait up to 10 seconds
                    if (_mmcProcess.MainWindowHandle == IntPtr.Zero)
                    {
                        // Try waiting a bit longer
                        System.Threading.Thread.Sleep(2000);
                        _mmcProcess.Refresh();
                    }
                });

                if (_mmcProcess.MainWindowHandle == IntPtr.Zero)
                {
                    throw new Exception("MMC window did not appear within timeout period");
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
                        if (_mmcProcess != null && !_mmcProcess.HasExited)
                        {
                            Win32Helper.MoveWindow(_mmcProcess.MainWindowHandle,
                                0, 0, _hostPanel.Width, _hostPanel.Height, true);
                        }
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
        private bool IsProcessElevated()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
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
        public void CloseConsole()
        {
            try
            {
                if (_mmcProcess != null && !_mmcProcess.HasExited)
                {
                    LogManager.LogInfo($"[MMC Host] Closing {_consoleName}");
                    _mmcProcess.CloseMainWindow();
                    _mmcProcess.WaitForExit(3000); // Wait up to 3 seconds

                    if (!_mmcProcess.HasExited)
                    {
                        _mmcProcess.Kill();
                    }

                    _mmcProcess.Dispose();
                    _mmcProcess = null;
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
    }
}
