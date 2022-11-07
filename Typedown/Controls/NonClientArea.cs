using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Typedown.Universal.Utilities;

namespace Typedown.Controls
{
    public class NonClientArea : HwndHost
    {
        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            var styleEx = PInvoke.WindowStylesEx.WS_EX_LAYERED | PInvoke.WindowStylesEx.WS_EX_NOREDIRECTIONBITMAP;
            var style = PInvoke.WindowStyles.WS_CHILD;
            var hwnd = PInvoke.CreateWindowEx(styleEx, "STATIC", "NonClientArea", style, 0, 0, 0, 0, hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            PInvoke.SetLayeredWindowAttributes(hwnd, 0, 255, PInvoke.LayeredWindowFlags.LWA_ALPHA);
            PInvoke.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, PInvoke.SetWindowPosFlags.SWP_NOSIZE | PInvoke.SetWindowPosFlags.SWP_NOMOVE);
            return new HandleRef(this, hwnd);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            PInvoke.DestroyWindow(hwnd.Handle);
        }

        protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch ((PInvoke.WindowMessage)msg)
            {
                case PInvoke.WindowMessage.WM_NCHITTEST:
                    handled = true;
                    return (nint)PInvoke.HitTestFlags.HTTRANSPARENT;
            }
            return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
        }
    }
}
