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
            IsSearchPaneOpen = true;
        }

        private void OnSearchPaneClose(object sender, EventArgs e)
        {
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
