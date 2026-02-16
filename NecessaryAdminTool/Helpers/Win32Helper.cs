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
    }
}
