using System;

namespace Typedown.Utilities
{
    public class CoreWindow
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
            CoreWindowHandle = handle;
            var exStyle = PInvoke.GetWindowLong(CoreWindowHandle, PInvoke.WindowLongFlags.GWL_EXSTYLE);
            PInvoke.SetWindowLong(CoreWindowHandle, PInvoke.WindowLongFlags.GWL_EXSTYLE, exStyle | (int)PInvoke.WindowStylesEx.WS_EX_TOOLWINDOW);
        }
    }
}
