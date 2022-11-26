using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Foundation;

namespace Typedown.Universal.Utilities
{
    public sealed class WindowClass : IDisposable
    {
        public string ClassName { get; }

        public bool IsDisposed { get; private set; }

        private readonly PInvoke.WindowProc defWindowProc;

        private readonly Dictionary<nint, PInvoke.WindowProc> windowProcs = new();

        private WindowClass(string className)
        {
            ClassName = className;
            defWindowProc = WndProc;
        }

        private nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
        {
            if (windowProcs.TryGetValue(hWnd, out var proc))
            {
                if (msg == (uint)PInvoke.WindowMessage.WM_DESTROY)
                    windowProcs.Remove(hWnd);
                return proc(hWnd, msg, wParam, lParam);
            }
            return PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        public static WindowClass Register(string className)
        {
            var result = new WindowClass(className);
            var wndClass = new PInvoke.WNDCLASSEX();
            wndClass.cbSize = Marshal.SizeOf(wndClass);
            wndClass.style = PInvoke.ClassStyles.HorizontalRedraw | PInvoke.ClassStyles.VerticalRedraw;
            wndClass.lpfnWndProc = result.defWindowProc;
            wndClass.cbClsExtra = 0;
            wndClass.cbWndExtra = 0;
            wndClass.hInstance = Process.GetCurrentProcess().Handle;
            wndClass.hIcon = PInvoke.LoadIcon(PInvoke.GetModuleHandle(null), "#32512");
            wndClass.hCursor = PInvoke.LoadCursor(0, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_ARROW);
            wndClass.hbrBackground = 0;
            wndClass.lpszMenuName = null;
            wndClass.lpszClassName = className;
            var atom = PInvoke.RegisterClassEx(ref wndClass);
            if (atom == 0) return null;
            return result;
        }

        public nint CreateWindow(PInvoke.WindowProc windowProc = null, string title = null, PInvoke.WindowStyles style = 0, PInvoke.WindowStylesEx styleEx = 0, Rect rect = default, nint hWndParent = 0, nint hMenu = 0)
        {
            var hWnd = PInvoke.CreateWindowEx(styleEx, ClassName, title, style, (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height, hWndParent, hMenu, Process.GetCurrentProcess().Handle, 0);
            if (windowProc != null)
                windowProcs.Add(hWnd, windowProc);
            return hWnd;
        }

        public async void Dispose()
        {
            await App.Dispatcher.RunIdleAsync(_ =>
            {
                if (!IsDisposed)
                {
                    IsDisposed = true;
                    PInvoke.EnumProcessWindow(Process.GetCurrentProcess().Id).Where(hWnd => PInvoke.GetClassName(hWnd) == ClassName).ToList().ForEach(hWnd => PInvoke.DestroyWindow(hWnd));
                    PInvoke.UnregisterClass(ClassName, 0);
                }
            });
        }

        ~WindowClass()
        {
            Dispose();
        }
    }
}
