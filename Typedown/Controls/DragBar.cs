using System;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Typedown.Utilities;

namespace Typedown.Controls
{
    public class DragBar : HwndHost
    {
        private HwndSource hwndSource;

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            hwndSource = new HwndSource(new HwndSourceParameters()
            {
                ParentWindow = hwndParent.Handle,
                WindowStyle = (int)PInvoke.WindowStyles.WS_CHILD,
                UsesPerPixelTransparency = true,
                Height = 0,
                Width = 0,
            })
            {
                RootVisual = new Grid() { Background = new SolidColorBrush(Color.FromArgb(1, 0x80, 0x80, 0x80)) }
            };
            PInvoke.SetWindowPos(hwndSource.Handle, IntPtr.Zero, 0, 0, 0, 0, PInvoke.SetWindowPosFlags.SWP_NOSIZE | PInvoke.SetWindowPosFlags.SWP_NOMOVE);
            return new HandleRef(this, hwndSource.Handle);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            hwndSource.Dispose();
        }

        protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch ((PInvoke.WindowMessage)msg)
            {
                case PInvoke.WindowMessage.WM_NCHITTEST:
                    handled = true;
                    return new(-1);
            }
            return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
        }
    }
}
