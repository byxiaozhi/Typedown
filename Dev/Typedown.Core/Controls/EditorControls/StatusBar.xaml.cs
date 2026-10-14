using System;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls
{
    public sealed partial class StatusBar : UserControl
    {
        /// <summary>
        /// Paints this bar from a custom theme. Null puts the colours of the built-in theme back; the inner
        /// grid carries the visible background, so setting the control's own is not enough.
        /// </summary>
        public void ApplyThemeBrushes(Windows.UI.Xaml.Media.Brush background, Windows.UI.Xaml.Media.Brush foreground)
        {
            if (RootGrid != null)
            {
                if (background != null) RootGrid.Background = background;
                else RootGrid.ClearValue(Windows.UI.Xaml.Controls.Panel.BackgroundProperty);
            }
            if (foreground != null) Foreground = foreground;
            else ClearValue(ForegroundProperty);
        }

        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        public EditorViewModel Editor => ViewModel?.EditorViewModel;

        public StatusBar()
        {
            this.InitializeComponent();
            // Switching the status bar off and on again builds a new control, which knows nothing of the
            // theme that was applied to the old one — so it asks for the colours itself when it loads.
            Loaded += (_, _) => ApplyCurrentTheme();
        }

        private void ApplyCurrentTheme()
        {
            try
            {
                var theme = ThemeFiles.Find(Settings?.CustomTheme);
                ApplyThemeBrushes(
                    ThemeFiles.Brush(theme?.Surface) ?? ThemeFiles.Brush(theme?.Background),
                    ThemeFiles.Brush(theme?.Foreground) ?? ThemeFiles.Readable(theme?.Surface ?? theme?.Background));
            }
            catch (Exception ex)
            {
                Log.Debug($"status bar theme: {ex.Message}");
            }
        }

        /// <summary>The badge is also the way out: clicking it puts the document back into editing.</summary>
        private void OnLeaveReadOnlyClick(object sender, RoutedEventArgs e)
        {
            if (Settings != null)
                Settings.ReadOnly = false;
        }

        private string CharacterUnit(int number) => number != 1 ? Locale.GetString("Characters") : Locale.GetString("Character");

        private string WordUnit(int number) => number != 1 ? Locale.GetString("Words") : Locale.GetString("Word");

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings?.StopTracking();
        }
    }
}
