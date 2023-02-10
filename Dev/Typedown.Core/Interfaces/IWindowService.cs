using System.Reactive.Subjects;
using Windows.Foundation;
using Windows.UI.Xaml;

namespace Typedown.Core.Interfaces
{
    public interface IWindowService
    {
        Subject<nint> WindowStateChanged { get; }

        Subject<nint> WindowIsActivedChanged { get; }

        nint GetWindow(UIElement element);

        nint GetXamlSourceHandle(UIElement element);

        Point GetCursorPos(UIElement relativeTo);
    }
}
