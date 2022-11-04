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
    }
}
