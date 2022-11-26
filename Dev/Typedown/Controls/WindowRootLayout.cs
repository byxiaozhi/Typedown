using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.Controls
{
    public class WindowRootLayout : Grid
    {
        public static DependencyProperty WindowProperty { get; } = DependencyProperty.Register(nameof(Window), typeof(Windows.AppWindow), typeof(WindowRootLayout), new(null));
        public Windows.AppWindow Window { get => (Windows.AppWindow)GetValue(WindowProperty); set => SetValue(WindowProperty, value); }

        public WindowRootLayout()
        {
            Name = nameof(WindowRootLayout);
        }
    }
}
