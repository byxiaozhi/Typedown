using System;
using System.Runtime.InteropServices;

namespace Typedown.WinUI.Utilities
{
    // Just enough raw kernel32 surface to do an *alertable* wait on a managed ManualResetEvent's
    // underlying handle — see Program.cs's RedirectActivationTo for why a plain
    // ManualResetEvent.WaitOne() (a non-alertable wait) isn't good enough there.
    internal static class Win32Event
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint WaitForSingleObjectEx(IntPtr handle, uint milliseconds, bool alertable);
    }
}
