using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Converters;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Typedown.Universal.Controls
{
    public class WindowRootLayout : Grid
    {
        public static DependencyProperty WindowProperty { get; } = DependencyProperty.Register(nameof(Window), typeof(Windows.AppWindow), typeof(WindowRootLayout), new(null));
        public Windows.AppWindow Window { get => (Windows.AppWindow)GetValue(WindowProperty); set => SetValue(WindowProperty, value); }

        public WindowRootLayout()
        {
            Name = "WindowRootLayout";
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SetBinding(RequestedThemeProperty, new Binding()
            {
                Source = this.GetService<SettingsViewModel>(),
                Path = new(nameof(SettingsViewModel.AppTheme)),
                Converter = new ElementThemeConverter()
            });
        }
    }
}
