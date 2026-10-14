using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Typedown.Core.Models;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using muxc = Microsoft.UI.Xaml.Controls;

namespace Typedown.Core.Controls
{
    /// <summary>Tab strip above the editor; one tab per open document (see <see cref="TabsViewModel"/>).</summary>
    public sealed partial class DocumentTabBar : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public TabsViewModel Tabs => ViewModel?.TabsViewModel;

        private readonly CompositeDisposable disposables = new();

        public DocumentTabBar()
        {
            InitializeComponent();
            // handledEventsToo: once there are more tabs than fit, the strip's own scroll viewer takes the
            // wheel and marks it handled, so a plain handler never sees it and the wheel only slid the strip
            // sideways instead of moving between tabs.
            AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnPointerWheelChanged), true);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Bindings.Update();
            var settings = ViewModel?.SettingsViewModel;
            if (settings != null)
                disposables.Add(settings.WhenPropertyChanged(nameof(settings.CustomTheme)).StartWith(settings.CustomTheme).Subscribe(_ => UpdateThemeColours()));
        }

        /// <summary>
        /// The editor CSS runs in WebView and cannot style this native WinUI control. Mirror the custom theme's
        /// colours here. The strip itself stays transparent, so it shows the same background as the side pane
        /// beside it and the header band (menu row) ends at one height across the window; inactive tabs have no
        /// fill of their own, and the selected tab is a card a little lighter than the editor background, edged
        /// with the theme's border colour. Painting the strip and the inactive tabs with the panel surface made
        /// the header twice as tall on the right as on the left and heavy. The remaining states are derived so
        /// hover, press, close and add buttons stay legible.
        /// </summary>
        private void UpdateThemeColours()
        {
            var theme = ThemeFiles.Find(ViewModel?.SettingsViewModel?.CustomTheme);
            var editor = ThemeFiles.Brush(theme?.Background);
            var surface = ThemeFiles.Brush(theme?.Surface) ?? editor;
            var foreground = ThemeFiles.Brush(theme?.Foreground) ?? ThemeFiles.Readable(theme?.Surface ?? theme?.Background);
            var border = ThemeFiles.Brush(theme?.Border);

            TabView.Background = new SolidColorBrush(Colors.Transparent);

            var transparent = theme == null ? null : new SolidColorBrush(Colors.Transparent);
            var strip = editor ?? surface;
            var card = Card(strip);
            var inactiveForeground = WithOpacity(foreground, .68);
            var disabledForeground = WithOpacity(foreground, .40);
            var hover = Blend(strip, foreground, .06);
            var pressed = Blend(strip, foreground, .12);
            var buttonHover = WithOpacity(foreground, .10);
            var buttonPressed = WithOpacity(foreground, .16);

            SetBrush("TabViewItemHeaderBackground", transparent);
            SetBrush("TabViewItemHeaderBackgroundSelected", card);
            SetBrush("TabViewItemHeaderBackgroundPointerOver", hover);
            SetBrush("TabViewItemHeaderBackgroundPressed", pressed);
            SetBrush("TabViewItemHeaderBackgroundDisabled", transparent);
            SetBrush("TabViewItemHeaderForeground", inactiveForeground);
            SetBrush("TabViewItemHeaderForegroundSelected", foreground);
            SetBrush("TabViewItemHeaderForegroundPointerOver", foreground);
            SetBrush("TabViewItemHeaderForegroundPressed", foreground);
            SetBrush("TabViewItemHeaderForegroundDisabled", disabledForeground);

            SetBrush("TabViewItemIconForeground", inactiveForeground);
            SetBrush("TabViewItemIconForegroundSelected", foreground);
            SetBrush("TabViewItemIconForegroundPointerOver", foreground);
            SetBrush("TabViewItemIconForegroundPressed", foreground);
            SetBrush("TabViewItemSeparator", border);
            SetBrush("TabViewBorderBrush", border);
            SetBrush("TabViewItemBorderBrush", border);
            SetBrush("TabViewSelectedItemBorderBrush", border);
            SetResource("TabViewSelectedItemBorderThickness", null);

            SetButtonBrushes("TabViewButton", transparent, hover, pressed, foreground, disabledForeground, border);
            SetButtonBrushes("TabViewScrollButton", transparent, hover, pressed, foreground, disabledForeground, border);

            SetBrush("TabViewItemHeaderCloseButtonForeground", inactiveForeground);
            SetBrush("TabViewItemHeaderCloseButtonForegroundPointerOver", foreground);
            SetBrush("TabViewItemHeaderCloseButtonForegroundPressed", foreground);
            SetBrush("TabViewItemHeaderPointerOverCloseButtonForeground", foreground);
            SetBrush("TabViewItemHeaderPressedCloseButtonForeground", foreground);
            SetBrush("TabViewItemHeaderSelectedCloseButtonForeground", foreground);
            SetBrush("TabViewItemHeaderDisabledCloseButtonForeground", disabledForeground);
            SetBrush("TabViewItemHeaderCloseButtonBackground", new SolidColorBrush(Colors.Transparent), surface != null);
            SetBrush("TabViewItemHeaderPointerOverCloseButtonBackground", buttonHover);
            SetBrush("TabViewItemHeaderPressedCloseButtonBackground", buttonPressed);
            SetBrush("TabViewItemHeaderSelectedCloseButtonBackground", new SolidColorBrush(Colors.Transparent), surface != null);
            SetBrush("TabViewItemHeaderDisabledCloseButtonBackground", new SolidColorBrush(Colors.Transparent), surface != null);
            RefreshThemeResources();
        }

        /// <summary>
        /// Changing these resources does not reach tabs that already exist: each tab looked its brushes up when it
        /// was created, while its selected/pointer-over states look theirs up again on every change of state. Without
        /// this the first tabs kept the default colours, new tabs got the theme's, and switching tabs mixed the two -
        /// two different "current tab" looks, and a tab left behind with the other one's colours - until something
        /// else (toggling a setting) happened to make the tab strip look everything up again. Flipping the strip's
        /// theme and back makes every tab and every state resolve its theme resources afresh, from these values.
        /// </summary>
        private void RefreshThemeResources()
        {
            var requested = TabView.RequestedTheme;
            TabView.RequestedTheme = TabView.ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
            TabView.RequestedTheme = requested;
        }

        private void SetButtonBrushes(string prefix, Brush background, Brush hover, Brush pressed, Brush foreground, Brush disabledForeground, Brush border)
        {
            SetBrush(prefix + "Background", background);
            SetBrush(prefix + "BackgroundPointerOver", hover);
            SetBrush(prefix + "BackgroundPressed", pressed);
            SetBrush(prefix + "BackgroundDisabled", background);
            SetBrush(prefix + "Foreground", foreground);
            SetBrush(prefix + "ForegroundPointerOver", foreground);
            SetBrush(prefix + "ForegroundPressed", foreground);
            SetBrush(prefix + "ForegroundDisabled", disabledForeground);
            SetBrush(prefix + "BorderBrush", border);
            SetBrush(prefix + "BorderBrushPointerOver", border);
            SetBrush(prefix + "BorderBrushPressed", border);
            SetBrush(prefix + "BorderBrushDisabled", border);
        }

        private void SetBrush(string key, Brush brush, bool apply = true) => SetResource(key, apply ? brush : null);

        private void SetResource(string key, object value)
        {
            if (value == null)
                TabView.Resources.Remove(key);
            else
                TabView.Resources[key] = value;
        }

        private static Brush WithOpacity(Brush brush, double opacity)
        {
            if (!(brush is SolidColorBrush solid)) return brush;
            var colour = solid.Color;
            return new SolidColorBrush(Color.FromArgb((byte)(255 * opacity), colour.R, colour.G, colour.B));
        }

        /// <summary>The selected tab: the background moved toward white (a light theme) or lifted slightly (a dark one).</summary>
        private static Brush Card(Brush background)
        {
            if (!(background is SolidColorBrush bg)) return background;
            var c = bg.Color;
            var light = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) > 128;
            return Blend(background, new SolidColorBrush(Colors.White), light ? .55 : .07);
        }

        private static Brush Blend(Brush background, Brush foreground, double amount)
        {
            if (!(background is SolidColorBrush bg) || !(foreground is SolidColorBrush fg)) return background;
            byte Mix(byte from, byte to) => (byte)Math.Round(from + (to - from) * amount);
            return new SolidColorBrush(Color.FromArgb(255, Mix(bg.Color.R, fg.Color.R), Mix(bg.Color.G, fg.Color.G), Mix(bg.Color.B, fg.Color.B)));
        }

        /// <summary>
        /// The wheel over the tab strip moves between tabs: up goes left, down goes right. The strip scrolls
        /// itself when there are more tabs than fit, which is never what the wheel is wanted for here.
        /// </summary>
        private void OnPointerWheelChanged(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (Tabs == null) return;
            var delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
            if (delta == 0) return;
            e.Handled = true;
            (delta > 0 ? Tabs.PreviousTabCommand : Tabs.NextTabCommand).Execute(default);
            // The strip may have scrolled itself before this ran; following the selection puts it right.
            TabView.SelectedItem = Tabs.ActiveTab;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Tabs == null) return;
            if (TabView.SelectedItem is DocumentTab tab && tab != Tabs.ActiveTab)
            {
                Tabs.SwitchTabCommand.Execute(tab);
                // A tab clicked in the strip keeps the keyboard on the tab; the reader came to write in the document.
                // Not when the selection moved because a tab was closed: handed over while the next document was
                // being loaded, the keys disturbed its loading, and the outline stayed on the closed document's
                // headings (E2E TC02). The keyboard is where it was after a close anyway.
                if (!closing) _ = Dispatcher.RunIdleAsync(_ => ViewModel?.MarkdownEditor?.FocusEditor());
            }
        }

        private void OnAddTabButtonClick(muxc.TabView sender, object args)
        {
            ViewModel?.FileViewModel.NewFileCommand.Execute(default);
        }

        private bool closing;

        private void OnTabCloseRequested(muxc.TabView sender, muxc.TabViewTabCloseRequestedEventArgs args)
        {
            if (args.Item is DocumentTab tab)
            {
                closing = true;
                Tabs?.CloseTabCommand.Execute(tab);
                _ = Dispatcher.RunIdleAsync(_ => closing = false);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
            Bindings?.StopTracking();
        }
    }
}
