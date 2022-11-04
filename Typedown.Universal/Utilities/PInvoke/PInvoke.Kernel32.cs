using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Typedown.Universal.Utilities
{
    public static partial class PInvoke
    {
        [DllImport("kernel32.dll", ExactSpelling = true)]
        public static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", ExactSpelling = true)]
        public static extern bool QueryPerformanceFrequency(out long frequency);

        [DllImport("kernel32", ExactSpelling = true)]
        public static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);
    }
}
