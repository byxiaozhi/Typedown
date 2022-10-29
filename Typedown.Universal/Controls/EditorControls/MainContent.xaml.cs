using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.InteropServices.WindowsRuntime;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Typedown.Universal.Controls
{
    public sealed partial class MainContent : UserControl
    {
        public AppViewModel AppViewModel => this.GetService<AppViewModel>();

        public SettingsViewModel SettingsViewModel => this.GetService<SettingsViewModel>();

        private readonly CompositeDisposable disposables = new();

        public MainContent()
        {
            this.InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            MarkdownEditorPresenter.Content = this.GetService<IMarkdownEditor>();
            disposables.Add(AppViewModel.SettingsViewModel.GetPropertyObservable().Subscribe(x => OnSettingsViewModelPropertyChanged(x.Sender as SettingsViewModel, x.EventArgs)));
            VisualStateManager.GoToState(this, SettingsViewModel.SidePaneOpen ? "SidePaneOpen" : "SidePaneHide", false);
        }

        private void OnSettingsViewModelPropertyChanged(SettingsViewModel sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SettingsViewModel.SidePaneOpen):
                    VisualStateManager.GoToState(this, sender.SidePaneOpen ? "SidePaneOpen" : "SidePaneHide", true);
                    break;
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Splitter.ColumnMaxWidth = ActualWidth - 40;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
        }
    }
}
