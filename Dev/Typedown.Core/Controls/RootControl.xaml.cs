using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Typedown.Core.Pages;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Typedown.Core.Controls
{
    public sealed partial class RootControl : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        private readonly CompositeDisposable disposables = new();

        public RootControl()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.XamlRoot = XamlRoot;
            disposables.Add(ViewModel.NavigateCommand.OnExecute.Subscribe(args => Navigate(args)));
            RegisterSettingsShortcut();
            // The settings are the one page you are looking at when you change the language, so they are built
            // again straight away. The rest of the interface catches up by itself: leaving the settings
            // navigates to MainPage, and with no NavigationCacheMode that is a new page, so the menus and
            // everything else built from {u:LocaleString} are evaluated again in the new language. What does
            // not catch up is anything a view model holds as a finished string — the view models outlive the
            // navigation — which is why UIViewModel rebuilds the window title on a language change.
            disposables.Add(Settings.WhenPropertyChanged(nameof(Settings.Language)).Subscribe(_ => ReloadSettingsPage()));
            disposables.Add(Settings.WhenPropertyChanged(nameof(Settings.ShortcutSettings)).Subscribe(_ => RegisterSettingsShortcut()));
            Frame.Navigated += OnFrameNavigated;
            Frame.Navigate(typeof(MainPage), null);
        }

        private Type shownPage;

        // The window opening, and back from the settings page (its shortcut, the back button): the reader is in the
        // document, keyboard included. The editor takes it now, and again once its page has loaded. A window just opened
        // gave it to nothing: the first letters went nowhere until a click in the text.
        private void OnFrameNavigated(object sender, NavigationEventArgs e)
        {
            var from = shownPage;
            shownPage = e.SourcePageType;
            if (e.SourcePageType != typeof(MainPage) || (from != null && from != typeof(SettingsPage))) return;
            ViewModel.EditorViewModel.FocusWhenLoaded = true;
            _ = Dispatcher.RunIdleAsync(_ => ViewModel.MarkdownEditor?.FocusEditor());
        }

        private void ReloadSettingsPage()
        {
            if (Frame.SourcePageType != typeof(SettingsPage)) return;
            var route = currentRoute;
            _ = Dispatcher.TryRunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    var path = route?.TrimStart('/').Split('/');
                    var depth = Frame.BackStackDepth;
                    Frame.Navigate(typeof(SettingsPage), path != null ? string.Join('/', path.Skip(1)) : null, new SuppressNavigationTransitionInfo());
                    // Building the page again is not somewhere the user navigated to, so it leaves no entry
                    // behind: otherwise every language change added one more press to the way back out.
                    while (Frame.BackStackDepth > depth && Frame.BackStack.Count > 0)
                        Frame.BackStack.RemoveAt(Frame.BackStack.Count - 1);
                }
                catch (Exception ex)
                {
                    Utilities.Log.Debug($"reload settings after language change: {ex.Message}");
                }
            });
        }

        private IDisposable settingsShortcut;

        /// <summary>
        /// The way out of the settings. The menu entry takes care of opening them, but the menu bar belongs to
        /// the main page and is gone while the settings are up, so the shortcut that opened them would do
        /// nothing — this one lives above the frame and stays registered.
        /// </summary>
        private void RegisterSettingsShortcut()
        {
            settingsShortcut?.Dispose();
            settingsShortcut = null;
            var accelerator = this.GetService<Interfaces.IKeyboardAccelerator>();
            var key = Settings?.ShortcutSettings;
            if (accelerator == null || key == null) return;
            settingsShortcut = accelerator.Register(key, (s, e) =>
            {
                if (Frame.SourcePageType != typeof(SettingsPage)) return;
                if (Utilities.PInvoke.GetForegroundWindow() != ViewModel.MainWindow) return;
                e.Handled = true;
                _ = Dispatcher.TryRunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => Navigate("Main"));
            });
            disposables.Add(settingsShortcut);
        }

        private void OnUnloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            disposables.Clear();
            Bindings?.StopTracking();
        }

        /// <summary>Where the frame is now, so that asking for it again can be read as "close this".</summary>
        private string currentRoute;

        private void Navigate(string args)
        {
            var path = args?.TrimStart('/').Split('/');
            if (path == null || !path.Any())
                return;
            var type = Route.GetRootPageType(path.First());
            // Navigating to the page that is already open, by the same route, closes it: the shortcut that
            // opens the settings is then the one that leaves them again. Another route into the same page (the
            // image settings from the image menu, say) still just moves to that section.
            if (type == Frame.SourcePageType)
            {
                if (type == typeof(MainPage) || args != currentRoute)
                    return;
                currentRoute = "Main";
                Frame.Navigate(typeof(MainPage), null, GetTransition());
                return;
            }
            currentRoute = args;
            Frame.Navigate(type, string.Join('/', path.Skip(1)), GetTransition());
        }

        public NavigationTransitionInfo GetTransition() => Settings?.AnimationEnable ?? false ? new SlideNavigationTransitionInfo()
        {
            Effect = SlideNavigationTransitionEffect.FromRight
        } : new SuppressNavigationTransitionInfo();

        public static double GetCaptionRowMinHeight(bool isFullScreen) => isFullScreen ? 0 : 4;

        public static bool GetCaptionIsLoad(bool compactMode, Type currentPage, bool isFullScreen)
        {
            if (isFullScreen && currentPage == typeof(MainPage))
                return false;
            return !compactMode || currentPage != typeof(MainPage);
        }

        private void OnClosePrintPreviewClick(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel?.UIViewModel?.ClosePrintPreview();
        }
    }
}
