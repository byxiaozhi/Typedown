using System;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Typedown.Universal.Controls
{
    public class CaptionDragBar : UserControl
    {
        private IWindowService WindowService => this.GetService<IWindowService>();

        private nint ParentHandle => WindowService.GetParent(WindowService.GetWindow(this));

        private const uint WM_NCHITTEST = 0x0084;
        private const uint WM_NCLBUTTONDOWN = 0x00A1;
        private const uint WM_NCLBUTTONDBLCLK = 0x00A3;
        private const uint WM_NCRBUTTONUP = 0x00A5;

        private nint HitTestResult;

        private DateTime lastClickTime;

        public CaptionDragBar()
        {
            Content = new Windows.UI.Xaml.Shapes.Rectangle() { Fill = new SolidColorBrush(Colors.Transparent) };
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            base.OnPointerMoved(e);
            HitTest();
        }

        protected override async void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                var doubleClick = DateTime.Now - lastClickTime <= WindowService.GetDoubleClickTime();
                await Dispatcher.RunIdleAsync(_ => { });
                PostPointerMessage(doubleClick ? WM_NCLBUTTONDBLCLK : WM_NCLBUTTONDOWN);
                lastClickTime = DateTime.Now;
            }
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonReleased)
                PostPointerMessage(WM_NCRBUTTONUP);
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                HitTestResult = 0;
                UpdateCursor();
            }
        }

        private bool PostPointerMessage(uint msg)
        {
            return WindowService.PostMessage(ParentHandle, msg, (nuint)HitTestResult, WindowService.GetCursorPos().PackPoint());
        }

        private void HitTest()
        {
            HitTestResult = WindowService.SendMessage(ParentHandle, WM_NCHITTEST, (nuint)HitTestResult, WindowService.GetCursorPos().PackPoint());
            UpdateCursor();
        }

        private void UpdateCursor()
        {
            Windows.UI.Xaml.Window.Current.CoreWindow.PointerCursor = new CoreCursor(HitTestResult switch
            {
                12 => CoreCursorType.SizeNorthSouth,
                13 => CoreCursorType.SizeNorthwestSoutheast,
                14 => CoreCursorType.SizeNortheastSouthwest,
                _ => CoreCursorType.Arrow
            }, 0);
        }
    }
}
