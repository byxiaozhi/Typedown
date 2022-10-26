using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Typedown.Utilities
{
    public static partial class PInvoke
    {
        [DllImport("coredll.dll", SetLastError = true)]
        public static extern bool QueryPerformanceFrequency(out long frequency);
    }
}
