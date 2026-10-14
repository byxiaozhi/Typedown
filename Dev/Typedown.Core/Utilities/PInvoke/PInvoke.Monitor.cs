using System.Runtime.InteropServices;

namespace Typedown.Core.Utilities
{
    public static partial class PInvoke
    {
        public const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern bool ClientToScreen(nint hWnd, ref POINT lpPoint);

        /// <summary>Client area of the window in screen coordinates.</summary>
        public static RECT GetClientRectOnScreen(nint hwnd)
        {
            GetClientRect(hwnd, out var client);
            var origin = new POINT(0, 0);
            ClientToScreen(hwnd, ref origin);
            return new RECT { left = origin.X, top = origin.Y, right = origin.X + client.right, bottom = origin.Y + client.bottom };
        }

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool GetMonitorInfoW(nint hMonitor, ref MONITORINFO lpmi);

        /// <summary>Full bounds of the monitor the window is (mostly) on.</summary>
        public static RECT GetWindowMonitorRect(nint hwnd)
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfoW(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref info);
            return info.rcMonitor;
        }
    }
}
