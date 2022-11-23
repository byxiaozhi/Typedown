using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Typedown.Universal.Utilities;
using Typedown.Utilities;

namespace Typedown.Windows
{
    public class ExitSigWindow
    {
        private static readonly WindowClass windowClass = WindowClass.Register(typeof(ExitSigWindow).FullName, StaticWndProc);

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
            Handle = windowClass.CreateWindow();
        }
    }
}
