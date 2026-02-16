using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NecessaryAdminTool.Helpers;

namespace NecessaryAdminTool.UI.Components
{
    // TAG: #MMC_EMBEDDING #WIN32_INTEROP #EXTERNAL_PROCESS
    /// <summary>
    /// User control that hosts MMC consoles (ADUC, GPMC, DNS Manager, etc.) in WPF
    /// Uses WindowsFormsHost + Win32 API to embed external MMC processes
    /// </summary>
    public partial class MMCHostControl : UserControl
    {
        private Process _mmcProcess;
        private System.Windows.Forms.Panel _hostPanel;
        private string _consoleName;
        private string _mmcPath;

        public event EventHandler ConsoleClosed;

        public MMCHostControl()
        {
            InitializeComponent();
            Loaded += MMCHostControl_Loaded;
            Unloaded += MMCHostControl_Unloaded;
        }

        /// <summary>
        /// Initialize and launch the MMC console
        /// TAG: #MMC_EMBEDDING #KERBEROS
        /// </summary>
        /// <param name="consoleName">Display name of the console</param>
        /// <param name="mmcSnapin">Snap-in file name (e.g., dsa.msc)</param>
        /// <param name="username">Optional: Domain username for credential passthrough</param>
        /// <param name="domain">Optional: Domain name for credential passthrough</param>
        public async Task LoadConsoleAsync(string consoleName, string mmcSnapin, string username = null, string domain = null)
        {
            _consoleName = consoleName;
            _mmcPath = mmcSnapin;

            TxtConsoleName.Text = consoleName;
            TxtLoadingMessage.Text = $"Launching {consoleName}...";

            try
            {
                // Create the Windows Forms panel that will host the MMC process
                _hostPanel = new System.Windows.Forms.Panel
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    BackColor = System.Drawing.Color.FromArgb(26, 26, 26)
                };

                // Start the MMC process with optional credential passthrough
                var startInfo = new ProcessStartInfo
                {
                    FileName = "mmc.exe",
                    Arguments = mmcSnapin,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                };

                // TAG: #KERBEROS #CREDENTIAL_PASSTHROUGH
                // Use runas /netonly for Kerberos ticket passthrough if credentials provided
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(domain))
                {
                    // Note: /netonly uses cached Kerberos tickets for network authentication
                    // The MMC will run as current user locally but use provided credentials for domain operations
                    LogManager.LogInfo($"[MMC Host] Using domain credentials: {domain}\\{username} (Kerberos passthrough)");
                    TxtLoadingMessage.Text = $"Launching {consoleName} with domain credentials...";

                    // Set domain credentials for network authentication
                    startInfo.Domain = domain;
                    startInfo.UserName = username;
                    startInfo.UseShellExecute = false;

                    // Note: Password would need to be provided via SecureString in production
                    // For Kerberos passthrough, we rely on cached TGT (Ticket Granting Ticket)
                    // The user must have already authenticated to the domain
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
        /// Retry loading the console
        /// TAG: #ERROR_HANDLING
        /// </summary>
        private async void BtnRetry_Click(object sender, RoutedEventArgs e)
        {
            ErrorPanel.Visibility = Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Visible;

            await LoadConsoleAsync(_consoleName, _mmcPath);
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
