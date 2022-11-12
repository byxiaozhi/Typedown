using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Typedown.Universal.Utilities;
using Typedown.Utilities;

namespace Typedown.Windows
{
    public class ExitSigWindow
    {
        private static readonly string ClassName = typeof(ExitSigWindow).FullName;

        private static readonly PInvoke.WindowProc staticWndProc = new(StaticWndProc);

        static ExitSigWindow()
        {
            RegisterClass();
        }

        private static bool RegisterClass()
        {
            var wndClass = new PInvoke.WNDCLASSEX();
            wndClass.cbSize = Marshal.SizeOf(wndClass);
            wndClass.style = 0;
            wndClass.lpfnWndProc = staticWndProc;
            wndClass.cbClsExtra = 0;
            wndClass.cbWndExtra = 0;
            wndClass.hInstance = Process.GetCurrentProcess().Handle;
            wndClass.hIcon = 0;
            wndClass.hCursor = 0;
            wndClass.hbrBackground = 0;
            wndClass.lpszMenuName = null;
            wndClass.lpszClassName = ClassName;
            var result = PInvoke.RegisterClassEx(ref wndClass);
            return result != 0;
        }

        private static IntPtr StaticWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
        {
            if (msg == (uint)PInvoke.WindowMessage.WM_DESTROY)
                Dispatcher.Current.Shutdown();
            return PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        public static nint Handle { get; private set; }

        public static void Start()
        {
            if (Handle != default)
                return;
            Handle = PInvoke.CreateWindowEx(0, ClassName, "", 0, 0, 0, 0, 0, 0, 0, Process.GetCurrentProcess().Handle, 0);
        }
    }
}
