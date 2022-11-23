using System;
using Typedown.Universal.Utilities;
using Typedown.Utilities;

namespace Typedown.Windows
{
    public class ExitSigWindow
    {
        private static readonly WindowClass windowClass = WindowClass.Register(typeof(ExitSigWindow).FullName);

        public static nint Handle { get; private set; }

        public static void Start()
        {
            if (Handle != default)
                return;
            Handle = windowClass.CreateWindow(WndProc);
        }

        private static IntPtr WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
        {
            if (msg == (uint)PInvoke.WindowMessage.WM_DESTROY)
                Dispatcher.Current.Shutdown();
            return PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
        }
    }
}
