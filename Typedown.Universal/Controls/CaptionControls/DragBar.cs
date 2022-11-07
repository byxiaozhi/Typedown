using System;
using System.Threading.Tasks;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Windows.UI;
using Windows.UI.Input;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Typedown.Universal.Controls
{
    public class DragBar : Grid
    {
        private IWindowService WindowService => this.GetService<IWindowService>();

        private nint ParentHandle => PInvoke.GetParent(WindowService.GetWindow(this));

        private DateTime lastClickTime;

        public DragBar()
        {
            Background = new SolidColorBrush(Colors.Transparent);
            AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), false);
            AddHandler(PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), false);
        }

        private async void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var props = e.GetCurrentPoint(this).Properties;
            if (props.IsLeftButtonPressed)
            {
                var doubleClick = DateTime.Now - lastClickTime <= TimeSpan.FromMilliseconds(PInvoke.GetDoubleClickTime());
                lastClickTime = DateTime.Now;
                await PostPointerMessage(doubleClick ? PInvoke.WindowMessage.WM_NCLBUTTONDBLCLK : PInvoke.WindowMessage.WM_NCLBUTTONDOWN);
            }
        }

        private async void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonReleased)
            {
                await PostPointerMessage(PInvoke.WindowMessage.WM_NCRBUTTONUP);
            }
        }

        private async Task PostPointerMessage(PInvoke.WindowMessage msg)
        {
            await Dispatcher.TryRunIdleAsync(_ => PInvoke.PostMessage(ParentHandle, (uint)msg, (nint)PInvoke.HitTestFlags.CAPTION, PackPoint(PInvoke.GetCursorPos())));
        }

        public nint PackPoint(System.Drawing.Point point) => point.X | (point.Y << 16);
    }
}
