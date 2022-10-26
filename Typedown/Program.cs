using System;
using Typedown.Utilities;
using System.Windows;
using Typedown.Windows;

namespace Typedown
{
    public class Program
    {
        private static readonly PInvoke.HookProc hookProc = new(HookProc);

        private static IntPtr hHook;

        [STAThread]
        public static void Main()
        {
            hHook = PInvoke.SetWindowsHookEx(PInvoke.HookType.WH_CBT, hookProc, IntPtr.Zero, PInvoke.GetCurrentThreadId());
            using (new Universal.App())
                new App().Run();
        }

        private static IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code == 3) // HCBT_CREATEWND 
            {
                var className = PInvoke.GetClassName(wParam);
                if (className == "Windows.UI.Core.CoreWindow")
                    CoreWindow.SetCoreWindow(wParam);
            }
            return PInvoke.CallNextHookEx(hHook, code, wParam, lParam);
        }
    }
}
