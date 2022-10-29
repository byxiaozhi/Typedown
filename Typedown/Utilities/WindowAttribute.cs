using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using Typedown.Windows;

namespace Typedown.Utilities
{
    public static class WindowAttribute
    {
        public static bool IsMicaSupported => Environment.OSVersion.Version.Build >= 22523;

        public static void SetMicaBackdrop(this AppWindow window, bool enable)
        {
            if (Environment.OSVersion.Version.Build >= 22523)
            {
                uint micaValue = enable ? 0x02u : 0;
                PInvoke.DwmSetWindowAttribute(window.Handle, PInvoke.DwmWindowAttribute.DWMWA_SYSTEMBACKDROP_TYPE, ref micaValue, Marshal.SizeOf(typeof(uint)));
            }
            else
            {
                uint trueValue = enable ? 0x01u : 0;
                PInvoke.DwmSetWindowAttribute(window.Handle, PInvoke.DwmWindowAttribute.DWMWA_MICA_EFFECT, ref trueValue, Marshal.SizeOf(typeof(uint)));
            }
        }

        public static void SetDarkMode(this AppWindow window, bool enable)
        {
            var darkModeValue = enable ? 1u : 0;
            PInvoke.DwmSetWindowAttribute(window.Handle, PInvoke.DwmWindowAttribute.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeValue, Marshal.SizeOf(typeof(uint)));
        }

        public static void SetCaptionColor(this AppWindow window, uint color)
        {
            PInvoke.DwmSetWindowAttribute(window.Handle, PInvoke.DwmWindowAttribute.DWMWA_CAPTION_COLOR, ref color, Marshal.SizeOf(typeof(uint)));
        }
    }
}
