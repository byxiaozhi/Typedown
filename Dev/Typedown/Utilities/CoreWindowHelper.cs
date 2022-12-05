using System;
using Typedown.Core.Utilities;

namespace Typedown.Utilities
{
    public static class CoreWindowHelper
    {
        public static nint CoreWindowHandle { get; private set; }

        public static void DetachCoreWindow()
        {
            if (CoreWindowHandle != IntPtr.Zero)
            {
                PInvoke.SetParent(CoreWindowHandle, IntPtr.Zero);
                PInvoke.ShowWindow(CoreWindowHandle, PInvoke.ShowWindowCommand.Hide);
            }
        }

        public static void SetCoreWindow(IntPtr handle)
        {
            if (IsCoreWindow(handle))
            {
                CoreWindowHandle = handle;
                var exStyle = PInvoke.GetWindowLong(CoreWindowHandle, PInvoke.WindowLongFlags.GWL_EXSTYLE);
                PInvoke.SetWindowLong(CoreWindowHandle, PInvoke.WindowLongFlags.GWL_EXSTYLE, exStyle | (int)PInvoke.WindowStylesEx.WS_EX_TOOLWINDOW);
            }
        }

        public static bool IsCoreWindow(nint handle)
        {
            return PInvoke.GetClassName(handle) == typeof(global::Windows.UI.Core.CoreWindow).FullName;
        }
    }
}
