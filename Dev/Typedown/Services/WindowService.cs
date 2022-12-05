using Typedown.Core.Interfaces;
using Windows.UI.Xaml;
using System.Reactive.Subjects;
using Typedown.Windows;
using Windows.Foundation;
using Typedown.Core.Utilities;

namespace Typedown.Services
{
    public class WindowService : IWindowService
    {
        public Subject<nint> WindowStateChanged { get; } = new();

        public Subject<nint> WindowIsActivedChanged { get; } = new();

        public Subject<nint> WindowScaleChanged { get; } = new();

        public void RaiseWindowStateChanged(nint hWnd) => WindowStateChanged.OnNext(hWnd);

        public void RaiseWindowIsActivedChanged(nint hWnd) => WindowIsActivedChanged.OnNext(hWnd);

        public void RaiseWindowScaleChanged(nint hWnd) => WindowScaleChanged.OnNext(hWnd);

        public nint GetWindow(UIElement element) => AppWindow.GetWindow(element)?.Handle ?? default;

        public nint GetXamlSourceHandle(UIElement element) => AppWindow.GetWindow(element)?.XamlSourceHandle ?? default;

        public Point GetCursorPos(UIElement relativeTo)
        {
            var window = AppWindow.GetWindow(relativeTo);
            PInvoke.GetCursorPos(out var screenPos);
            PInvoke.GetWindowRect(window.XamlSourceHandle, out var xamlRootRect);
            var pos = new Point((screenPos.X - xamlRootRect.left) / window.ScalingFactor, (screenPos.Y - xamlRootRect.top) / window.ScalingFactor);
            return relativeTo.XamlRoot.Content.TransformToVisual(relativeTo).TransformPoint(pos);
        }
    }
}
