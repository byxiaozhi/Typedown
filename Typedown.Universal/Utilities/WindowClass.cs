using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Foundation;

namespace Typedown.Universal.Utilities
{
    public sealed class WindowClass : IDisposable
    {
        public short ClassAtom { get; }

        public string ClassName { get; }

        public bool IsDisposed { get; private set; }

        private static readonly ConditionalWeakTable<WindowClass, PInvoke.WindowProc> windowProcs = new();

        private WindowClass(short classAtom, string className, PInvoke.WindowProc windowProc)
        {
            ClassAtom = classAtom;
            ClassName = className;
            windowProcs.Add(this, windowProc);
        }

        public static WindowClass Register(string className, PInvoke.WindowProc windowProc)
        {
            var wndClass = new PInvoke.WNDCLASSEX();
            wndClass.cbSize = Marshal.SizeOf(wndClass);
            wndClass.style = PInvoke.ClassStyles.HorizontalRedraw | PInvoke.ClassStyles.VerticalRedraw;
            wndClass.lpfnWndProc = windowProc;
            wndClass.cbClsExtra = 0;
            wndClass.cbWndExtra = 0;
            wndClass.hInstance = Process.GetCurrentProcess().Handle;
            wndClass.hIcon = PInvoke.LoadIcon(PInvoke.GetModuleHandle(null), "#32512");
            wndClass.hCursor = PInvoke.LoadCursor(0, (int)PInvoke.IDC_STANDARD_CURSORS.IDC_ARROW);
            wndClass.hbrBackground = 0;
            wndClass.lpszMenuName = null;
            wndClass.lpszClassName = className;
            var atom = PInvoke.RegisterClassEx(ref wndClass);
            if (atom == 0)
                return null;
            return new(atom, className, windowProc);
        }

        public nint CreateWindow(string title = null, PInvoke.WindowStyles style = 0, PInvoke.WindowStylesEx styleEx = 0, Rect rect = default, nint hWndParent = 0, nint hMenu = 0)
        {
            return PInvoke.CreateWindowEx(styleEx, ClassName, title, style, (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height, hWndParent, hMenu, Process.GetCurrentProcess().Handle, 0);
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
