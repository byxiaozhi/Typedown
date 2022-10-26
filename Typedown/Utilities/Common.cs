using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace Typedown.Utilities
{
    public static class Common
    {
        public static Point MakePoint(this IntPtr p) => new(p.ToInt32() & 0xFFFF, p.ToInt32() >> 16);
    }
}
