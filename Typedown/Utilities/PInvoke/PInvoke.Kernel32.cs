using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Typedown.Utilities
{
    public static partial class PInvoke
    {
        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();
    }
}
