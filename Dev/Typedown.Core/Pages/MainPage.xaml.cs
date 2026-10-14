using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Typedown.Core.Pages
{
    public sealed partial class MainPage : Page
    {
        public AppViewModel AppViewModel => this.GetService<AppViewModel>();

        private readonly CompositeDisposable disposables = new();

        // Full screen hides the menu bar; hovering the top edge shows it until the pointer leaves and no menu is open.
        private bool menuBarRevealed;

        private readonly DispatcherTimer hideMenuBarTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

        // The pointer has to rest on the top edge a moment: passing over it on the way to a tab must not reveal.
        private readonly DispatcherTimer revealMenuBarTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };

        public MainPage()
        {
            InitializeComponent();
            hideMenuBarTimer.Tick += OnHideMenuBarTimerTick;
            revealMenuBarTimer.Tick += OnRevealMenuBarTimerTick;
        }

        private void OnExportNoticeOpenClick(object sender, RoutedEventArgs e) => OpenExported(Common.OpenFile);

        private void OnExportNoticeFolderClick(object sender, RoutedEventArgs e) => OpenExported(Common.OpenFileLocation);

        private void OnExportNoticeCloseClick(object sender, RoutedEventArgs e) => AppViewModel.UIViewModel.HideExported();

        private async void OpenExported(Action<string> open)
        {
            var path = AppViewModel.UIViewModel.ExportedPath;
            AppViewModel.UIViewModel.HideExported();
            if (path == null) return;
            try { open(path); }
            catch (Exception ex)
            {
                Log.Debug($"export notice: could not open {path}: {ex.Message}");
                try { await Controls.AppContentDialog.Create(Locale.GetString("Error"), ex.Message, Locale.GetString("Ok")).ShowAsync(XamlRoot); } catch { }
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var uiViewModel = AppViewModel.UIViewModel;
            disposables.Add(uiViewModel.WhenPropertyChanged(nameof(UIViewModel.IsFullScreen)).Cast<bool>().StartWith(uiViewModel.IsFullScreen).Subscribe(OnFullScreenChanged));
            var settings = AppViewModel.SettingsViewModel;
            disposables.Add(settings.WhenPropertyChanged(nameof(settings.CustomTheme)).StartWith(settings.CustomTheme).Subscribe(_ => UpdateThemeColours()));
        }

        /// <summary>
        /// A custom theme colours the strips around the editor as well: the menu bar and the status bar. What
        /// the theme does not name keeps the colours of the built-in theme it builds on, and a panel painted
        /// without a text colour gets one that can be read on it.
        /// </summary>
        private void UpdateThemeColours()
        {
            try
            {
                var theme = ThemeFiles.Find(AppViewModel.SettingsViewModel.CustomTheme);
                var surface = ThemeFiles.Brush(theme?.Surface) ?? ThemeFiles.Brush(theme?.Background);
                var foreground = ThemeFiles.Brush(theme?.Foreground) ?? ThemeFiles.Readable(theme?.Surface ?? theme?.Background);
                MenuBarHost?.ApplyThemeBrushes(surface, foreground);
                // A menu row in its own colour needs no line under it (see MenuBar.SetDivider).
                var panel = ThemeFiles.Brush(theme?.Surface) as Windows.UI.Xaml.Media.SolidColorBrush;
                var editor = ThemeFiles.Brush(theme?.Background) as Windows.UI.Xaml.Media.SolidColorBrush;
                MenuBarHost?.SetDivider(!(panel != null && editor != null && panel.Color != editor.Color));
                StatusBar?.ApplyThemeBrushes(surface, foreground);
            }
            catch (Exception ex)
            {
                Log.Debug($"theme colours: {ex.Message}");
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            hideMenuBarTimer.Stop();
            revealMenuBarTimer.Stop();
            disposables.Clear();
            Bindings?.StopTracking();
        }

        private void OnFullScreenChanged(bool isFullScreen)
        {
            menuBarRevealed = false;
            hideMenuBarTimer.Stop();
            revealMenuBarTimer.Stop();
            UpdateMenuBarVisibility();
        }

        private void UpdateMenuBarVisibility()
        {
            var isFullScreen = AppViewModel?.UIViewModel?.IsFullScreen ?? false;
            MenuBarHost.Visibility = !isFullScreen || menuBarRevealed ? Visibility.Visible : Visibility.Collapsed;
            // Revealed in full screen it takes its own row and pushes the tabs and editor down. It used to float
            // over the content: it then lay on the tabs, and with the pointer still inside it the bar never hid,
            // so the tabs could not be clicked.
            Grid.SetRow(MenuBarHost, 0);
            MenuBarHost.VerticalAlignment = VerticalAlignment.Stretch;
            MenuBarHost.Background = isFullScreen ? GetOpaqueBackground() : null;
            // The drag areas are native windows laid over their elements where those are when the areas are made. Out of
            // full screen the bar has only just been made visible and the window is still being given its size back:
            // made now, the areas lay where nothing was, and the window could not be dragged until something laid the
            // bar out again (a menu opened, the settings visited). They are made once the layout has settled.
            if (isFullScreen) MenuBarHost.SetDragEnabled(false);
            else _ = Dispatcher.RunIdleAsync(_ =>
            {
                if (!(AppViewModel?.UIViewModel?.IsFullScreen ?? false)) MenuBarHost.SetDragEnabled(true);
            });
            // While the bar is out the edge strip would only cover the top of its menus.
            if (FullScreenRevealStrip != null) FullScreenRevealStrip.IsHitTestVisible = !menuBarRevealed;
        }

        private Brush GetOpaqueBackground()
        {
            if (Application.Current.Resources.TryGetValue("SolidBackgroundFillColorBaseBrush", out var brush) && brush is Brush solid)
                return solid;
            return Application.Current.Resources["ApplicationPageBackgroundThemeBrush"] as Brush;
        }

        private void OnRevealStripPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            hideMenuBarTimer.Stop();
            if (!menuBarRevealed) revealMenuBarTimer.Start();
        }

        private void OnRevealStripPointerExited(object sender, PointerRoutedEventArgs e)
        {
            revealMenuBarTimer.Stop();
        }

        private void OnRevealMenuBarTimerTick(object sender, object e)
        {
            revealMenuBarTimer.Stop();
            if (!(AppViewModel?.UIViewModel?.IsFullScreen ?? false)) return;
            menuBarRevealed = true;
            UpdateMenuBarVisibility();
        }

        private void OnMenuBarPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            hideMenuBarTimer.Stop();
        }

        private void OnMenuBarPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (menuBarRevealed)
                hideMenuBarTimer.Start();
        }

        private void OnHideMenuBarTimerTick(object sender, object e)
        {
            if (!menuBarRevealed) { hideMenuBarTimer.Stop(); return; }
            // A menu flyout is open (pointer is in the popup): keep the bar until it closes.
            if (XamlRoot != null && VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot).Any())
                return;
            hideMenuBarTimer.Stop();
            menuBarRevealed = false;
            UpdateMenuBarVisibility();
        }
    }
}
