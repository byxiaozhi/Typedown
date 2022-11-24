using System;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.Controls
{
    public class DragBar : Control
    {
        private static readonly WindowClass windowClass = WindowClass.Register(typeof(DragBar).FullName);

        private nint dragBarhandle;

        private nint parentHandle;

        private nint xamlSourceHandle;

        public DragBar()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
        }

        private void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var windowService = this.GetService<IWindowService>();
            parentHandle = windowService.GetWindow(this);
            xamlSourceHandle = windowService.GetXamlSourceHandle(this);
            var style = PInvoke.WindowStyles.WS_CHILD | PInvoke.WindowStyles.WS_VISIBLE;
            var styleEx = PInvoke.WindowStylesEx.WS_EX_NOREDIRECTIONBITMAP | PInvoke.WindowStylesEx.WS_EX_LAYERED;
            dragBarhandle = windowClass.CreateWindow(WndProc, Name, style, styleEx, new(), parentHandle, 0);
            PInvoke.SetLayeredWindowAttributes(dragBarhandle, 0, 255, PInvoke.LayeredWindowFlags.LWA_ALPHA);
            PInvoke.SetWindowPos(dragBarhandle, 0, 0, 0, 0, 0, 0);
            UpdatePos();
        }

        private void OnUnloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            PInvoke.DestroyWindow(dragBarhandle);
            dragBarhandle = IntPtr.Zero;
        }

        private void OnSizeChanged(object sender, Windows.UI.Xaml.SizeChangedEventArgs e)
        {
            UpdatePos();
        }

        private void UpdatePos()
        {
            if (dragBarhandle == IntPtr.Zero)
                return;
            PInvoke.GetWindowRect(parentHandle, out var parentRect);
            PInvoke.GetWindowRect(xamlSourceHandle, out var xamlSourceRect);
            var position = TransformToVisual(XamlRoot.Content).TransformPoint(new(0, 0));
            var scalingFactor = PInvoke.GetDpiForWindow(dragBarhandle) / 96d;
            var x = position.X * scalingFactor;
            var y = position.Y * scalingFactor + xamlSourceRect.top - parentRect.top;
            var width = ActualWidth * scalingFactor;
            var height = ActualHeight * scalingFactor;
            PInvoke.SetWindowPos(dragBarhandle, 0, (int)x, (int)y, (int)width, (int)height, PInvoke.SetWindowPosFlags.SWP_NOZORDER);
        }

        protected virtual IntPtr WndProc(nint hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            return (PInvoke.WindowMessage)msg switch
            {
                PInvoke.WindowMessage.WM_NCHITTEST => new((int)PInvoke.HitTestFlags.HTTRANSPARENT),
                _ => PInvoke.DefWindowProc(hWnd, msg, wParam, lParam),
            };
        }
    }
}
