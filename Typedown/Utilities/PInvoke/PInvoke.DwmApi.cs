using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Text;

namespace Typedown.Utilities
{
    public static partial class PInvoke
    {
        public enum DwmWindowAttribute : uint
        {
            DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
            DWMWA_MICA_EFFECT = 1029,
            DWMWA_SYSTEMBACKDROP_TYPE = 38,
            DWMWA_CAPTION_COLOR = 35,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        };

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, DwmWindowAttribute dwAttribute, ref uint pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, MARGINS pMarInset);

        [DllImport("dwmapi.dll")]
        public static extern bool DwmDefWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);

        [DllImport("user32.dll", SetLastError = false, ExactSpelling = true)]
        public static extern IntPtr GetSystemMenu(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool bRevert);
    }
}
