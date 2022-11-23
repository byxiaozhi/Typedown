using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Typedown.Universal.Utilities;

namespace Typedown.Windows
{
    public class DragWindow
    {
        private static readonly WindowClass windowClass = WindowClass.Register(typeof(DragWindow).FullName, StaticWndProc);

        private static readonly Dictionary<nint, DragWindow> windows = new();

        private static IntPtr StaticWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
        {
            if (windows.TryGetValue(hWnd, out var val))
            {
                var result = val.WndProc(hWnd, msg, wParam, lParam);
                if (msg == (uint)PInvoke.WindowMessage.WM_DESTROY)
                    windows.Remove(hWnd);
                return result;
            }
            return PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        public nint Handle { get; private set; }

        public DragWindow(nint parent)
        {
            CreateWindow(parent);
        }

        private void CreateWindow(nint parent)
        {
            Handle = windowClass.CreateWindow(null, PInvoke.WindowStyles.WS_CHILD, PInvoke.WindowStylesEx.WS_EX_NOREDIRECTIONBITMAP | PInvoke.WindowStylesEx.WS_EX_LAYERED, new(), parent, 0);
            PInvoke.SetLayeredWindowAttributes(Handle, 0, 255, PInvoke.LayeredWindowFlags.LWA_ALPHA);
            windows.Add(Handle, this);
        }

        protected virtual IntPtr WndProc(nint hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch ((PInvoke.WindowMessage)msg)
            {
                case PInvoke.WindowMessage.WM_NCHITTEST:
                    return new((int)PInvoke.HitTestFlags.HTTRANSPARENT);
            }
            return PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        ~DragWindow()
        {
            PInvoke.DestroyWindow(Handle);
        }
    }
}
