using System;
using Typedown.Controls;
using Typedown.Universal.Interfaces;
using Windows.UI.Xaml;
using Windows.Win32;

namespace Typedown.Services
{
    public class WindowService : IWindowService
    {
        public event EventHandler<nint> WindowStateChanged;

        public void RaiseWindowStateChanged(nint hWnd) => WindowStateChanged?.Invoke(this, hWnd);

        public nint GetWindow(UIElement element) => AppXamlHost.GetAppXamlHost(element)?.Handle ?? default;

        public nint GetParent(nint hWnd) => PInvoke.GetParent(new(hWnd));

        public bool PostMessage(nint hWnd, uint msg, nuint wParam, nint lParam) => PInvoke.PostMessage(new(hWnd), msg, wParam, lParam);

        public nint SendMessage(nint hWnd, uint msg, nuint wParam, nint lParam) => PInvoke.SendMessage(new(hWnd), msg, wParam, lParam).Value;

        public System.Drawing.Point GetCursorPos() => PInvoke.GetCursorPos(out var pos) ? pos : default;

        public bool IsZoomed(nint hWnd) => PInvoke.IsZoomed(new(hWnd));

        public TimeSpan GetDoubleClickTime() => TimeSpan.FromMilliseconds(PInvoke.GetDoubleClickTime());
    }
}
