using System;
using System.Reactive.Subjects;
using Windows.UI.Xaml;

namespace Typedown.Universal.Interfaces
{
    public interface IWindowService
    {
        Subject<nint> WindowStateChanged { get; }

        nint GetWindow(UIElement element);
    }
}
