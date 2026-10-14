using PropertyChanged;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Typedown.Core.Controls
{
    public sealed partial class MainContent : UserControl, INotifyPropertyChanged
    {
        private static DependencyProperty IsLeftPaneLoadProperty = DependencyProperty.Register(nameof(IsLeftPaneLoad), typeof(bool), typeof(MainContent), new(false));
        private bool IsLeftPaneLoad { get => (bool)GetValue(IsLeftPaneLoadProperty); set => SetValue(IsLeftPaneLoadProperty, value); }

        private static DependencyProperty LeftPaneMaxWidthProperty = DependencyProperty.Register(nameof(LeftPaneMaxWidth), typeof(double), typeof(MainContent), new(0d));
        private double LeftPaneMaxWidth { get => (double)GetValue(LeftPaneMaxWidthProperty); set => SetValue(LeftPaneMaxWidthProperty, value); }

        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        private readonly CompositeDisposable disposables = new();

        public MainContent()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            disposables.Add(Settings.WhenPropertyChanged(nameof(Settings.SidePaneOpen)).Cast<bool>().Subscribe(x => UpdateSidePaneState(x, true)));
            disposables.Add(Settings.WhenPropertyChanged(nameof(Settings.UseEditorMicaEffect)).Cast<bool>().StartWith(Settings.UseEditorMicaEffect).Subscribe(x => UpdateBackground(x)));
            disposables.Add(Settings.WhenPropertyChanged(nameof(Settings.CustomTheme)).StartWith(Settings.CustomTheme).Subscribe(_ => UpdateThemeColours()));
            UpdateSidePaneState(Settings.SidePaneOpen, false);
        }

        private void UpdateSidePaneState(bool sidePaneOpen, bool useTransitions = true)
        {
            VisualStateManager.GoToState(this, sidePaneOpen ? "SidePaneExpand" : "SidePaneCollapse", useTransitions && Settings.AnimationEnable);
        }

        private void UpdateBackground(bool useMica)
        {
            if (themedBackground != null) return; // a custom theme paints this instead
            MainContentGrid.Background = Resources[useMica ? "MicaContentBackgroundBrush" : "SolidContentBackgroundBrush"] as Brush;
        }

        private Brush themedBackground;

        /// <summary>
        /// A custom theme may colour the window around the editor as well; what it leaves out keeps the colours
        /// of the built-in theme it builds on. Clearing a value puts the resource-driven colour back.
        /// </summary>
        private void UpdateThemeColours()
        {
            var theme = ThemeFiles.Find(Settings.CustomTheme);
            themedBackground = ThemeFiles.Brush(theme?.Background);
            var surface = ThemeFiles.Brush(theme?.Surface) ?? themedBackground;
            if (themedBackground != null)
                MainContentGrid.Background = themedBackground;
            else
                UpdateBackground(Settings.UseEditorMicaEffect);
            if (SplitterLine != null)
            {
                var border = ThemeFiles.Brush(theme?.Border);
                if (border != null) SplitterLine.Background = border;
                else SplitterLine.ClearValue(Border.BackgroundProperty);
            }
            if (LeftPane != null)
            {
                var foreground = ThemeFiles.Brush(theme?.Foreground) ?? ThemeFiles.Readable(theme?.Surface ?? theme?.Background);
                if (surface != null) LeftPane.Background = surface;
                else LeftPane.ClearValue(Control.BackgroundProperty);
                if (foreground != null) LeftPane.Foreground = foreground;
                else LeftPane.ClearValue(Control.ForegroundProperty);
            }
        }

        [SuppressPropertyChangedWarnings]
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            LeftPaneMaxWidth = ActualWidth - 40;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
            Bindings?.StopTracking();
        }

        public static bool IsTabBarLoad(int tabCount, bool alwaysShow) => alwaysShow || tabCount > 1;

        public static double GetColumnWidthNegative(GridLength length)
        {
            return -length.Value;
        }
    }
}
