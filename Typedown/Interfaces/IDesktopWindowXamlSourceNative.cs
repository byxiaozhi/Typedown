using System;
using System.Runtime.InteropServices;

namespace Typedown.Interfaces
{
    [ComImport, Guid("3cbcf1bf-2f76-4e9c-96ab-e84b37972554"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    partial interface IDesktopWindowXamlSourceNative
    {
        void AttachToWindow(IntPtr parentWnd);
        IntPtr WindowHandle { get; }
    }
}
