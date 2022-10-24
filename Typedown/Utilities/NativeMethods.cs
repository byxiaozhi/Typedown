using System;
using System.Collections.Generic;
using System.Text;
using Windows.Win32;

namespace Typedown.Utilities
{
    public static class NativeMethods
    {
        public static unsafe string GetClassName(nint hWnd)
        {
            var buffer = new char[256];
            fixed (char* p = buffer) PInvoke.GetClassName(new(hWnd), p, buffer.Length);
            return new string(buffer).TrimEnd('\0');
        }
    }
}
