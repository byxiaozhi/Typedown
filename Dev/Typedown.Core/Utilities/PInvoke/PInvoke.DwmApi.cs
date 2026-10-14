using System.Runtime.InteropServices;

namespace Typedown.Core.Utilities
{
    public static partial class PInvoke
    {
        public enum DwmWindowAttribute : uint
        {
            DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
            DWMWA_MICA_EFFECT = 1029,
            DWMWA_SYSTEMBACKDROP_TYPE = 38,
            DWMWA_CAPTION_COLOR = 35,
            DWMWA_WINDOW_CORNER_PREFERENCE = 33,
            DWMWA_BORDER_COLOR = 34,
        }

        public const uint DWMWCP_DEFAULT = 0;
        public const uint DWMWCP_DONOTROUND = 1;
        public const uint DWMWA_COLOR_DEFAULT = 0xFFFFFFFF;
        public const uint DWMWA_COLOR_NONE = 0xFFFFFFFE;

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        };

        [DllImport("dwmapi.dll", ExactSpelling = true)]
        public static extern int DwmSetWindowAttribute(nint hwnd, DwmWindowAttribute dwAttribute, ref uint pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll", ExactSpelling = true)]
        public static extern int DwmExtendFrameIntoClientArea(nint hwnd, MARGINS pMarInset);

        [DllImport("dwmapi.dll", ExactSpelling = true)]
        public static extern bool DwmDefWindowProc(nint hwnd, int msg, nint wParam, nint lParam, out nint plResult);
    }
}
