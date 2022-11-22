using System;
using System.Reactive.Subjects;
using Windows.Foundation;
using Windows.UI.Xaml;

namespace Typedown.Universal.Interfaces
{
    public interface IWindowService
    {
        Subject<nint> WindowStateChanged { get; }

        Subject<nint> WindowIsActivedChanged { get; }

        Subject<nint> WindowScaleChanged { get; }

        nint GetWindow(UIElement element);

        Point GetCursorPos(UIElement relativeTo);
    }
}
