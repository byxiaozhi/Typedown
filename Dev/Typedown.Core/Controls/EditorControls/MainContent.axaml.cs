using PropertyChanged;
using System;
using System.Collections.ObjectModel;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Typedown.Core.Controls
{
    public sealed partial class MainContent : UserControl, INotifyPropertyChanged
    {
        private static StyledProperty IsLeftPaneLoadProperty = AvaloniaProperty.Register<MainContent, bool>(nameof(IsLeftPaneLoad), new(false));
        private bool IsLeftPaneLoad { get => GetValue(IsLeftPaneLoadProperty); set => SetValue(IsLeftPaneLoadProperty, value); }

        private static StyledProperty LeftPaneMaxWidthProperty = AvaloniaProperty.Register<MainContent, double>(nameof(LeftPaneMaxWidth), new(0d));
        private double LeftPaneMaxWidth { get => GetValue(LeftPaneMaxWidthProperty); set => SetValue(LeftPaneMaxWidthProperty, value); }

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
            UpdateSidePaneState(Settings.SidePaneOpen, false);
        }

        private void UpdateSidePaneState(bool sidePaneOpen, bool useTransitions = true)
        {
            VisualStateManager.GoToState(this, sidePaneOpen ? "SidePaneExpand" : "SidePaneCollapse", useTransitions && Settings.AnimationEnable);
        }

        private void UpdateBackground(bool useMica)
        {
            MainContentGrid.Background = Resources[useMica ? "MicaContentBackgroundBrush" : "SolidContentBackgroundBrush"] as Brush;
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

        public static double GetColumnWidthNegative(GridLength length)
        {
            return -length.Value;
        }
    }
}
