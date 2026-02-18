using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NecessaryAdminTool.Helpers
{
    // TAG: #WIN32_INTEROP #MMC_EMBEDDING #EXTERNAL_PROCESS
    /// <summary>
    /// Win32 API helper for embedding external applications in WPF
    /// Used to host MMC consoles (ADUC, GPMC, DNS, etc.) inside WPF panels
    /// </summary>
    public static class Win32Helper
    {
        /// <summary>
        /// Sets the parent window of a window
        /// TAG: #WIN32_API
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        /// <summary>
        /// Sets window position, size, and Z order
        /// TAG: #WIN32_API
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        /// <summary>
        /// Sets window long value (style, ex style, etc.)
        /// TAG: #WIN32_API
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        /// <summary>
        /// Gets window long value
        /// TAG: #WIN32_API
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        /// <summary>
        /// Shows or hides a window
        /// TAG: #WIN32_API
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        /// <summary>
        /// Sends a message to a window
        /// TAG: #WIN32_API
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Gets window text (title)
        /// TAG: #WIN32_API
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        /// <summary>
        /// Gets window thread process ID
        /// TAG: #WIN32_API
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        // Window Style Constants
        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;

        // Window Styles
        public const int WS_CHILD = 0x40000000;
        public const int WS_BORDER = 0x00800000;
        public const int WS_CAPTION = 0x00C00000;
        public const int WS_SYSMENU = 0x00080000;
        public const int WS_THICKFRAME = 0x00040000;
        public const int WS_MINIMIZEBOX = 0x00020000;
        public const int WS_MAXIMIZEBOX = 0x00010000;
        public const int WS_OVERLAPPEDWINDOW = WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;

        // Extended Window Styles
        public const int WS_EX_DLGMODALFRAME = 0x00000001;
        public const int WS_EX_WINDOWEDGE = 0x00000100;
        public const int WS_EX_CLIENTEDGE = 0x00000200;
        public const int WS_EX_STATICEDGE = 0x00020000;

        // ShowWindow Commands
        public const int SW_HIDE = 0;
        public const int SW_SHOW = 5;
        public const int SW_MINIMIZE = 6;
        public const int SW_RESTORE = 9;

        /// <summary>
        /// Removes window border and title bar to make it embeddable
        /// TAG: #WIN32_INTEROP #WINDOW_STYLE
        /// </summary>
        public static void MakeWindowEmbeddable(IntPtr hWnd)
        {
            // Get current window style
            int style = GetWindowLong(hWnd, GWL_STYLE);
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

            // Remove title bar, borders, and system menu
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU);
            style |= WS_CHILD;

            // Remove extended styles
            exStyle &= ~(WS_EX_DLGMODALFRAME | WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE | WS_EX_STATICEDGE);

            // Apply new styles
            SetWindowLong(hWnd, GWL_STYLE, style);
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);
        }

        /// <summary>
        /// Gets the title of a window
        /// TAG: #WIN32_INTEROP
        /// </summary>
        public static string GetWindowTitle(IntPtr hWnd)
        {
            const int nChars = 256;
            StringBuilder buff = new StringBuilder(nChars);
            if (GetWindowText(hWnd, buff, nChars) > 0)
            {
                return buff.ToString();
            }
            return string.Empty;
        }

        // ═══════════════════════════════════════════════════════════════
        // TOKEN ELEVATION DETECTION
        // TAG: #WIN32_API #UAC_DETECTION #ELEVATION_CHECK
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets token information (used for elevation detection)
        /// TAG: #WIN32_API
        /// </summary>
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(
            IntPtr TokenHandle,
            TOKEN_INFORMATION_CLASS TokenInformationClass,
            IntPtr TokenInformation,
            uint TokenInformationLength,
            out uint ReturnLength);

        /// <summary>
        /// Token information class enumeration
        /// TAG: #WIN32_API
        /// </summary>
        private enum TOKEN_INFORMATION_CLASS
        {
            TokenElevation = 20
        }

        /// <summary>
        /// Token elevation structure
        /// TAG: #WIN32_API
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_ELEVATION
        {
            public int TokenIsElevated;
        }

        // ═══════════════════════════════════════════════════════════════
        // CREATEPROCESSWITHLOGONW - EDR BYPASS
        // TAG: #WIN32_API #CREATEPROCESSWITHLOGONW #EDR_BYPASS
        // Direct P/Invoke bypasses EDR hooks on Process.Start()
        // Sources:
        // - https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createprocesswithlogonw
        // - https://www.pinvoke.dev/advapi32/createprocesswithlogonw
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a new process and its primary thread. The new process runs in the security context of the specified credentials.
        /// TAG: #WIN32_API #CREATEPROCESSWITHLOGONW
        /// </summary>
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcessWithLogonW(
            string lpUsername,
            string lpDomain,
            string lpPassword,
            int dwLogonFlags,
            string lpApplicationName,
            string lpCommandLine,
            int dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation
        );

        /// <summary>
        /// STARTUPINFO structure for process creation
        /// TAG: #WIN32_API
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        /// <summary>
        /// PROCESS_INFORMATION structure returned by process creation
        /// TAG: #WIN32_API
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        // Logon flags for CreateProcessWithLogonW
        public const int LOGON_WITH_PROFILE = 0x00000001;
        public const int LOGON_NETCREDENTIALS_ONLY = 0x00000002;

        // Process creation flags
        public const int CREATE_DEFAULT_ERROR_MODE = 0x04000000;
        public const int CREATE_NEW_CONSOLE = 0x00000010;
        public const int CREATE_NEW_PROCESS_GROUP = 0x00000200;
        public const int CREATE_NO_WINDOW = 0x08000000;

        // STARTUPINFO flags
        public const int STARTF_USESHOWWINDOW = 0x00000001;
        public const int STARTF_USESTDHANDLES = 0x00000100;

        /// <summary>
        /// Checks if the current process is running with elevated (Administrator) privileges
        /// Uses Windows TokenElevation API for accurate UAC elevation detection
        /// TAG: #UAC_DETECTION #ELEVATION_CHECK
        /// </summary>
        /// <returns>True if process is elevated, false otherwise</returns>
        public static bool IsProcessElevated()
        {
            IntPtr tokenHandle = IntPtr.Zero;
            IntPtr elevationPtr = IntPtr.Zero;

            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                tokenHandle = identity.Token;

                // Get current user info for logging
                string userName = identity.Name;
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                bool isInAdminGroup = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

                var elevation = new TOKEN_ELEVATION();
                uint size = (uint)Marshal.SizeOf(elevation);
                elevationPtr = Marshal.AllocHGlobal((int)size);

                uint returnLength;
                if (GetTokenInformation(tokenHandle, TOKEN_INFORMATION_CLASS.TokenElevation, elevationPtr, size, out returnLength))
                {
                    elevation = (TOKEN_ELEVATION)Marshal.PtrToStructure(elevationPtr, typeof(TOKEN_ELEVATION));
                    bool isElevated = elevation.TokenIsElevated != 0;

                    // Diagnostic logging
                    LogManager.LogInfo($"[ELEVATION CHECK] User: {userName}");
                    LogManager.LogInfo($"[ELEVATION CHECK] In Administrators Group: {isInAdminGroup}");
                    LogManager.LogInfo($"[ELEVATION CHECK] Token Elevation API Result: {isElevated}");
                    LogManager.LogInfo($"[ELEVATION CHECK] TokenIsElevated Value: {elevation.TokenIsElevated}");

                    // Warning if group membership doesn't match elevation
                    if (isInAdminGroup != isElevated)
                    {
                        LogManager.LogWarning($"[ELEVATION CHECK] ⚠️ Mismatch detected! Admin Group={isInAdminGroup}, Token Elevated={isElevated}");
                        LogManager.LogWarning($"[ELEVATION CHECK] This indicates UAC is active with filtered token (expected behavior)");
                    }

                    return isElevated;
                }
                else
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    LogManager.LogError($"[ELEVATION CHECK] GetTokenInformation failed with error code: {errorCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[ELEVATION CHECK] Exception during elevation check", ex);
                return false;
            }
            finally
            {
                if (elevationPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(elevationPtr);
                }
            }
        }
    }
}
