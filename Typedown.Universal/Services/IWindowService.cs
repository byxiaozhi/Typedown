using System;
using Windows.UI.Xaml;

namespace Typedown.Universal.Services
{
    public interface IWindowService
    {
        delegate void WindowStateChangedHandler(nint hWnd);

        event WindowStateChangedHandler WindowStateChanged;

        nint GetWindow(UIElement element);

        nint GetParent(nint hWnd);

        bool PostMessage(nint hWnd, uint msg, nuint wParam, nint lParam);

        nint SendMessage(nint hWnd, uint msg, nuint wParam, nint lParam);

        System.Drawing.Point GetCursorPos();

        bool IsZoomed(nint hWnd);

        TimeSpan GetDoubleClickTime();
    }
}
