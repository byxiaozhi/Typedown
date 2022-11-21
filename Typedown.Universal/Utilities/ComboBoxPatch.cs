using Typedown.Universal.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Typedown.Universal.Utilities
{
    public class ComboBoxPatch
    {
        public static readonly DependencyProperty ApplyProperty = DependencyProperty.RegisterAttached("Apply", typeof(bool), typeof(ComboBoxPatch), new(false, OnDependencyPropertyChanged));
        public static bool GetApply(ComboBox target) => (bool)target.GetValue(ApplyProperty);
        public static void SetApply(ComboBox target, bool value) => target.SetValue(ApplyProperty, value);

        private static void OnDependencyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var target = d as ComboBox;
            if ((bool)e.NewValue)
            {
                target.DropDownOpened += OnDropDownOpened;
            }
            else
            {
                target.DropDownOpened -= OnDropDownOpened;
            }
        }

        private static void OnDropDownOpened(object sender, object e)
        {
            var target = sender as ComboBox;
            var settings = target.GetService<SettingsViewModel>();
            if (settings != null && settings.AppTheme != Enums.AppTheme.Default)
            {
                foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(target.XamlRoot))
                {
                    popup.RequestedTheme = settings.AppTheme == Enums.AppTheme.Light ? ElementTheme.Dark : ElementTheme.Light;
                    popup.RequestedTheme = settings.AppTheme == Enums.AppTheme.Light ? ElementTheme.Light : ElementTheme.Dark;
                }
            }
        }
    }
}
