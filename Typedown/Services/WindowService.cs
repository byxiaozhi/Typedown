using System;
using Typedown.Controls;
using Typedown.Universal.Interfaces;
using Windows.UI.Xaml;
using Typedown.Utilities;

namespace Typedown.Services
{
    public class WindowService : IWindowService
    {
        public event EventHandler<nint> WindowStateChanged;

        public void RaiseWindowStateChanged(nint hWnd) => WindowStateChanged?.Invoke(this, hWnd);

        public nint GetWindow(UIElement element) => AppXamlHost.GetAppXamlHost(element)?.Handle ?? default;

        public nint GetParent(nint hWnd) => PInvoke.GetParent(hWnd);

        public bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam) => PInvoke.PostMessage(hWnd, msg, wParam, lParam);

        public nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam) => PInvoke.SendMessage(hWnd, msg, wParam, lParam);

        public System.Drawing.Point GetCursorPos() => PInvoke.GetCursorPos();

        public bool IsZoomed(nint hWnd) => PInvoke.IsZoomed(hWnd);

        public TimeSpan GetDoubleClickTime() => TimeSpan.FromMilliseconds(PInvoke.GetDoubleClickTime());
    }
}
