using System;
using System.Runtime.InteropServices;

namespace Typedown.WinUI.Utilities
{
    // Small raw Win32 interop for bringing another window in this process to the foreground —
    // used by MainWindow's multi-window support (FocusIfOpenElsewhere) to match the original's
    // PInvoke.SetForegroundWindow/IsIconic/ShowWindow calls in Typedown\App.cs's OpenNewWindow.
    internal static class Win32Window
    {
        public const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);
    }
}
