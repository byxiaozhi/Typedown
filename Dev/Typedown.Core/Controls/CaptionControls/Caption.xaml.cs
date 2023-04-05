using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls
{
    public sealed partial class Caption : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        private readonly CompositeDisposable disposables = new();

        public Caption()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            disposables.Add(ViewModel.GoBackCommand
                .WhenPropertyChanged(nameof(ViewModel.GoBackCommand.IsExecutable))
                .Cast<bool>()
                .Subscribe(x => UpdateBackButtonState(x)));
            UpdateBackButtonState(ViewModel.GoBackCommand.IsExecutable, false);
        }

        private void UpdateBackButtonState(bool canGoBack, bool useTransitions = true)
        {
            if (IsLoaded)
                VisualStateManager.GoToState(this, canGoBack ? "BackVisible" : "BackCollapsed", useTransitions && Settings.AnimationEnable);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
            Bindings?.StopTracking();
        }
    }
}
