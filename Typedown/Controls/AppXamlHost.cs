using Microsoft.Toolkit.Wpf.UI.XamlHost;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Typedown.Universal.Resources.Converters;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Typedown.Utilities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Typedown.Controls
{
    public class AppXamlHost : WindowsXamlHost
    {
        private static readonly ConditionalWeakTable<UIElement, AppXamlHost> instanceTable = new();

        public UIElement Content { get; }

        public AppXamlHost(UIElement content)
        {
            InitialTypeName = "Typedown.Controls.AppXamlHostRootLayout";
            Content = content;
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            var res = base.BuildWindowCore(hwndParent);
            CoreWindow.DetachCoreWindow();
            return res;
        }

        protected override void OnChildChanged()
        {
            base.OnChildChanged();
            if (GetUwpInternalObject() is UIElement element)
            {
                instanceTable.Add(element, this);
                if (element is AppXamlHostRootLayout layout)
                    layout.Children.Add(Content);
            }
        }

        public static AppXamlHost GetAppXamlHost(UIElement element)
        {
            return instanceTable.TryGetValue(element.XamlRoot.Content, out var val) ? val : null;
        }
    }

    public class AppXamlHostRootLayout : Grid
    {
        public AppXamlHostRootLayout()
        {
            Name = "RootLayout";
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
