using System;
using Windows.UI.Xaml;

namespace Typedown.Universal.Interfaces
{
    public interface IWindowService
    {
        event EventHandler<nint> WindowStateChanged;

        nint GetWindow(UIElement element);

        nint GetParent(nint hWnd);

        bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

        bool IsZoomed(nint hWnd);

        void SetForegroundWindow(nint hWnd);
    }
}
