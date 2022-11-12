using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Typedown.Universal.Utilities;

namespace Typedown.Windows
{
    public class DragWindow
    {
        private static readonly string ClassName = typeof(DragWindow).FullName;

        private static readonly Dictionary<nint, DragWindow> windows = new();

        private static readonly PInvoke.WindowProc staticWndProc = new(StaticWndProc);

        static DragWindow()
        {
            RegisterClass();
        }

        private static bool RegisterClass()
        {
            var wndClass = new PInvoke.WNDCLASSEX();
            wndClass.cbSize = Marshal.SizeOf(wndClass);
            wndClass.style = PInvoke.ClassStyles.HorizontalRedraw | PInvoke.ClassStyles.VerticalRedraw;
            wndClass.lpfnWndProc = staticWndProc;
            wndClass.cbClsExtra = 0;
            wndClass.cbWndExtra = 0;
            wndClass.hInstance = Process.GetCurrentProcess().Handle;
            wndClass.hIcon = IntPtr.Zero;
            wndClass.hCursor = PInvoke.LoadCursor(IntPtr.Zero, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_ARROW);
            wndClass.hbrBackground = 0;
            wndClass.lpszMenuName = null;
            wndClass.lpszClassName = ClassName;
            var result = PInvoke.RegisterClassEx(ref wndClass);
            return result != 0;
        }

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
            Handle = PInvoke.CreateWindowEx(
                PInvoke.WindowStylesEx.WS_EX_NOREDIRECTIONBITMAP | PInvoke.WindowStylesEx.WS_EX_LAYERED,
                ClassName,
                "",
                PInvoke.WindowStyles.WS_CHILD,
                0, 0, 0, 0,
                parent,
                IntPtr.Zero,
                Process.GetCurrentProcess().Handle,
                IntPtr.Zero);
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
