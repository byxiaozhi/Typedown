using System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Typedown.Utilities
{
    public class CoreWindow
    {
        public static nint CoreWindowHandle { get; private set; }

        public static void DetachCoreWindow()
        {
            if (CoreWindowHandle != IntPtr.Zero)
            {
                PInvoke.SetParent(new(CoreWindowHandle), HWND.Null);
                PInvoke.ShowWindow(new(CoreWindowHandle), SHOW_WINDOW_CMD.SW_HIDE);
            }
        }

        public static bool TrySetCoreWindow(IntPtr handle)
        {
            if (NativeMethods.GetClassName(handle) == "Windows.UI.Core.CoreWindow")
            {
                CoreWindowHandle = handle;
                var exStyle = PInvoke.GetWindowLong(new(CoreWindowHandle), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
                PInvoke.SetWindowLong(new(CoreWindowHandle), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle | (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW);
                return true;
            }
            return false;
        }
    }
}
