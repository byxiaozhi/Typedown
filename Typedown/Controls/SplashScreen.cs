using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Typedown.Universal.Enums;

namespace Typedown.Controls
{
    public class SplashScreen : Grid
    {
        private readonly Border editorArea = new();

        public SplashScreen()
        {
            UseLayoutRounding = true;
            RowDefinitions.Add(new() { Height = new(68) });
            RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
            RowDefinitions.Add(new() { Height = new(31) });
            SetRow(editorArea, 1);
            Children.Add(editorArea);
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var isDarkMode = (AppTheme)Properties.Settings.Default.StartupTheme switch
            {
                AppTheme.Light => false,
                AppTheme.Dark => true,
                _ => !Utilities.Common.GetUseLightTheme()
            };
            editorArea.BorderThickness = new(0, 1, 0, 1);
            editorArea.Background = new SolidColorBrush(isDarkMode ? Color.FromRgb(0x28, 0x28, 0x28) : Color.FromRgb(0xf9, 0xf9, 0xf9));
            editorArea.BorderBrush = new SolidColorBrush(isDarkMode ? Color.FromArgb(0x19, 0, 0, 0) : Color.FromArgb(0x0f, 0, 0, 0));
        }
    }
}
