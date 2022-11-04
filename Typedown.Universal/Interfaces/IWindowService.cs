using System;
using Windows.UI.Xaml;

namespace Typedown.Universal.Interfaces
{
    public interface IWindowService
    {
        event EventHandler<nint> WindowStateChanged;

        nint GetWindow(UIElement element);
    }
}
