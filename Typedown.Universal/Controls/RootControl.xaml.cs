using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Typedown.Universal.Pages;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace Typedown.Universal.Controls
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
            disposables.Add(ViewModel.WhenPropertyChanged(nameof(ViewModel.NavPagePath)).Subscribe(_ => Navigate()));
            Navigate();
        }

        private void OnUnloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            disposables.Clear();
        }

        private void Navigate()
        {
            if (ViewModel.NavPagePath.Count > 0)
            {
                var type = GetPageType(ViewModel.NavPagePath[0]);
                if (type != Frame.SourcePageType)
                    Frame.Navigate(type, null, GetTransition(true));
            }
            else
            {
                Frame.Navigate(typeof(MainPage), null, GetTransition(false));
            }
        }

        public Type GetPageType(string pageName) => pageName switch
        {
            "Settings" => typeof(SettingsPage),
            _ => typeof(MainPage)
        };

        public NavigationTransitionInfo GetTransition(bool fromRight) => Settings?.AnimationEnable ?? false ? new SlideNavigationTransitionInfo()
        {
            Effect = fromRight ? SlideNavigationTransitionEffect.FromRight : SlideNavigationTransitionEffect.FromLeft
        } : new SuppressNavigationTransitionInfo();
    }
}
