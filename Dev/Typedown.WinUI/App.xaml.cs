using Microsoft.UI.Xaml;

namespace Typedown.WinUI
{
    public partial class App : Application
    {
        private Window? window;

        public App()
        {
            // The app starts in the theme chosen in Caret, not only its windows: resources that controls
            // from libraries (the tab strip, the Settings cards) look up are resolved against the app's
            // theme, so with Windows in dark mode and Caret set to light they came out dark. It can only
            // be set here, before anything loads; after a change in View > Theme, those few parts follow
            // on the next start.
            var theme = new ViewModels.SettingsViewModel().AppTheme;
            if (theme == Enums.AppTheme.Light) RequestedTheme = ApplicationTheme.Light;
            else if (theme == Enums.AppTheme.Dark) RequestedTheme = ApplicationTheme.Dark;
            InitializeComponent();
            // Settings > Layout > Density: WinUI's own compact sizing for every control.
            if (new ViewModels.SettingsViewModel().AppCompactMode)
            {
                try { Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new System.Uri("ms-appx:///Microsoft.UI.Xaml/DensityStyles/Compact.xaml") }); }
                catch { }
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Before any window: XAML resolves its {u:Loc} strings as it loads.
            Utilities.Locale.Load(new ViewModels.SettingsViewModel().Language);
            window = new MainWindow();
            window.Activate();
        }
    }
}
