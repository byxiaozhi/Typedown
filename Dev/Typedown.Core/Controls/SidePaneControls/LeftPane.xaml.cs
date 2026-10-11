using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using muxc = Microsoft.UI.Xaml.Controls;

namespace Typedown.Core.Controls
{
    public sealed partial class LeftPane : UserControl
    {
        public static readonly DependencyProperty IsSearchPaneOpenProperty = DependencyProperty.Register(nameof(IsSearchPaneOpen), typeof(bool), typeof(LeftPane), new(false));
        public bool IsSearchPaneOpen { get => (bool)GetValue(IsSearchPaneOpenProperty); set => SetValue(IsSearchPaneOpenProperty, value); }

        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        private readonly CompositeDisposable disposables = new();

        public LeftPane()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            disposables.Add(Settings.WhenPropertyChanged(nameof(SettingsViewModel.SidePaneIndex))
                .Cast<int>()
                .StartWith(Settings.SidePaneIndex)
                .Subscribe(UpdateSelectedItem));
            var uiViewModel = ViewModel.UIViewModel;
            disposables.Add(uiViewModel.WhenPropertyChanged(nameof(UIViewModel.FolderSearchOpen))
                .Cast<bool>()
                .StartWith(uiViewModel.FolderSearchOpen)
                .Subscribe(open => IsSearchPaneOpen = open));
            // The tabs read their text when the pane loads. Changing the language on the settings page builds a new
            // main page; a change from another window or the automation API leaves this one, so the tabs follow it
            // here, once the language has been applied.
            disposables.Add(Settings.WhenPropertyChanged(nameof(SettingsViewModel.Language))
                .Subscribe(language => _ = Dispatcher.RunIdleAsync(args =>
                {
                    Folder.Content = Locale.GetString("Files");
                    Toc.Content = Locale.GetString("Outline");
                })));
            disposables.Add(Settings.WhenPropertyChanged(nameof(SettingsViewModel.CustomTheme))
                .StartWith(Settings.CustomTheme)
                .Subscribe(_ => UpdateAccent()));
        }

        // The marks of what is chosen here - the bar under Files/Outline, the pill beside the current heading in the
        // outline and beside the open file in the folder tree - are drawn in the accent colour, which WinUI takes
        // from Windows, not from the custom theme: with any theme they stayed the system blue. A theme with an
        // accent of its own has it put here, where the Files/Outline bar, the outline and the folder tree all look it up; a
        // theme without one leaves the system accent. Each window has its own pane and so its own brush.
        private static readonly string[] AccentKeys = { "NavigationViewSelectionIndicatorForeground", "TreeViewItemSelectionIndicatorForeground" };

        // A row under the pointer or pressed draws its pill from keys of its own: the current heading turned the system
        // blue whenever the mouse was over it. The same colour there, a little lighter, as WinUI's own are.
        private static readonly (string Key, double Opacity)[] TreeStateKeys =
        {
            ("TreeViewItemSelectionIndicatorForegroundPointerOver", 0.9),
            ("TreeViewItemSelectionIndicatorForegroundPressed", 0.8),
        };

        private void UpdateAccent()
        {
            var accent = ThemeFiles.Brush(ThemeFiles.Find(Settings?.CustomTheme)?.Accent);
            foreach (var key in AccentKeys)
            {
                if (accent == null) Resources.Remove(key);
                else Resources[key] = accent;
            }
            foreach (var (key, opacity) in TreeStateKeys)
            {
                if (accent is not Windows.UI.Xaml.Media.SolidColorBrush solid) Resources.Remove(key);
                else Resources[key] = new Windows.UI.Xaml.Media.SolidColorBrush(solid.Color) { Opacity = opacity };
            }
            // What is already on screen looked its brushes up when it was built; flipping the theme and back makes
            // it look them up again (as in the tab bar).
            var requested = Root.RequestedTheme;
            Root.RequestedTheme = Root.ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
            Root.RequestedTheme = requested;
        }

        private void UpdateSelectedItem(int index)
        {
            NavigationView.SelectedItem = NavigationView.MenuItems[index];
        }

        private void OnSelectionChanged(muxc.NavigationView sender, muxc.NavigationViewSelectionChangedEventArgs args)
        {
            var pageName = (args.SelectedItem as muxc.NavigationViewItem).Tag as string;
            var pageType = SidePaneControls.Pages.Route.GetSidePanePageType(pageName);
            var animation = Settings.AnimationEnable && Frame.SourcePageType != null;
            var transition = animation ? args.RecommendedNavigationTransitionInfo : new SuppressNavigationTransitionInfo();
            Frame.Navigate(pageType, null, transition);
        }

        private void OnSearchButtonClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.UIViewModel != null) ViewModel.UIViewModel.FolderSearchOpen = true;
            IsSearchPaneOpen = true;
        }

        private void OnSearchPaneClose(object sender, EventArgs e)
        {
            if (ViewModel?.UIViewModel != null) ViewModel.UIViewModel.FolderSearchOpen = false;
            IsSearchPaneOpen = false;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
            Bindings?.StopTracking();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            FrameClip.Rect = new(0, 0, Frame.ActualWidth, Frame.ActualHeight);
        }
    }
}
