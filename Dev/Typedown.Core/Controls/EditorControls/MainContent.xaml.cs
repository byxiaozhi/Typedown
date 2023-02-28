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
            UpdateSidePaneState(Settings.SidePaneOpen, false);
        }

        private void UpdateSidePaneState(bool sidePaneOpen, bool useTransitions = true)
        {
            VisualStateManager.GoToState(this, sidePaneOpen ? "SidePaneExpand" : "SidePaneCollapse", useTransitions && Settings.AnimationEnable);
        }

        [SuppressPropertyChangedWarnings]
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            LeftPaneMaxWidth = ActualWidth - 40;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
            Bindings.StopTracking();
        }

        public static double GetColumnWidthNegative(GridLength length)
        {
            return -length.Value;
        }
    }
}
