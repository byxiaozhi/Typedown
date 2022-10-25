using System;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using Windows.Win32.Foundation;
using Typedown.Utilities;

namespace Typedown
{
    public class Program
    {
        private static readonly HOOKPROC hookProc = new(HookProc);

        private static HHOOK hHook;

        [STAThread]
        public static void Main()
        {
            hHook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_CBT, hookProc, HINSTANCE.Null, PInvoke.GetCurrentThreadId());
            using (new Universal.App())
            {
                var app = new App();
                app.InitializeComponent();
                app.Run();
            }
        }

        private static unsafe LRESULT HookProc(int code, WPARAM wParam, LPARAM lParam)
        {
            if (code == PInvoke.HCBT_CREATEWND)
                CoreWindow.TrySetCoreWindow((nint)wParam.Value);
            return PInvoke.CallNextHookEx(hHook, code, wParam, lParam);
        }
    }
}
