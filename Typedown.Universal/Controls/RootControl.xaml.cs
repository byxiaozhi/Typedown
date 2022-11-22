using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Pages;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace Typedown.Universal.Controls
{
    public sealed partial class RootControl : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        private IWindowService WindowService => ViewModel.ServiceProvider.GetService<IWindowService>();

        private readonly CompositeDisposable disposables = new();

        public RootControl()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.XamlRoot = XamlRoot;
            disposables.Add(ViewModel.NavigateCommand.OnExecute.Subscribe(args => Navigate(args)));
            disposables.Add(WindowService.WindowIsActivedChanged.Merge(WindowService.WindowScaleChanged).Merge(WindowService.WindowStateChanged).StartWith(WindowService.GetWindow(this)).Subscribe(UpdateWindowBorder));
            Frame.Navigate(typeof(MainPage), null);
        }

        private void OnUnloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            disposables.Clear();
        }

        private void Navigate(string args)
        {
            var path = args?.TrimStart('/').Split('/');
            if (path != null && path.Any())
            {
                var type = Route.GetRootPageType(path.First());
                if (type != Frame.SourcePageType)
                    Frame.Navigate(type, string.Join('/', path.Skip(1)), GetTransition());
            }
        }

        public NavigationTransitionInfo GetTransition() => Settings?.AnimationEnable ?? false ? new SlideNavigationTransitionInfo()
        {
            Effect = SlideNavigationTransitionEffect.FromRight
        } : new SuppressNavigationTransitionInfo();

        public static bool GetCaptionIsLoad(bool compactMode, Type currentPage)
        {
            return !compactMode || currentPage != typeof(MainPage);
        }

        private void UpdateWindowBorder(nint hWnd)
        {
            var thickness = PInvoke.IsZoomed(hWnd) ? 0 : 1 / (PInvoke.GetDpiForWindow(hWnd) / 96d);
            RootGrid.BorderThickness = new(0, thickness, 0, 0);
            var isDarkMode = ActualTheme == Windows.UI.Xaml.ElementTheme.Dark;
            var isActived = PInvoke.GetForegroundWindow() == hWnd;
            int alpha = Config.IsMicaSupported ? 0 : 255;
            int color = isDarkMode ? isActived ? 50 : 60 : isActived ? 110 : 170;
            RootGrid.BorderBrush = new SolidColorBrush(Color.FromArgb((byte)alpha, (byte)color, (byte)color, (byte)color));
        }

        private void OnActualThemeChanged(Windows.UI.Xaml.FrameworkElement sender, object args)
        {
            UpdateWindowBorder(WindowService.GetWindow(this));
        }
    }
}
