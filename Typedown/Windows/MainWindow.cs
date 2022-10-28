using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Data;
using Typedown.Controls;
using Typedown.Services;
using Typedown.Universal.Controls;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Typedown.Utilities;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Typedown.Windows
{
    public class MainWindow : AppWindow
    {
        public IServiceScope ServiceScope { get; } = Injection.ServiceProvider.CreateScope();

        public IServiceProvider ServiceProvider => ServiceScope.ServiceProvider;

        public AppViewModel AppViewModel { get; }

        public AppXamlHost AppXamlHost { get; private set; }

        public MainWindow()
        {
            AppViewModel = ServiceProvider.GetService<AppViewModel>();
            DataContext = AppViewModel;
            Loaded += OnLoaded;
            Closed += OnClosed;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Width = 1500;
            Height = 900;
            MinWidth = 480;
            MinHeight = 300;
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            Content = AppXamlHost = new AppXamlHost(new RootControl());
            SetBinding(ThemeProperty, new Binding() { Source = AppViewModel.SettingsViewModel, Path = new(nameof(SettingsViewModel.AppTheme)) });
            AppViewModel.GoBackCommand.CanExecuteChanged += (s, e) => CanGoBackChanged();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            (ServiceProvider.GetService<IWindowService>() as WindowService).RaiseWindowStateChanged(Handle);
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            CanGoBackChanged();
            AppViewModel.MainWindow = Handle;
        }

        private void CanGoBackChanged()
        {
            if (DragBar != null)
            {
                var leftSpace = AppViewModel.GoBackCommand.IsExecutable ? 32 : 0;
                var rightSpace = Universal.Config.IsMicaSupported ? 0 : 46 * 3;
                DragBar.Margin = new(leftSpace, 0, rightSpace, 0);
            }
        }

        private void CloseMenuFlyout()
        {
            if (AppXamlHost.GetUwpInternalObject() is AppXamlHostRootLayout rootLayout)
            {
                VisualTreeHelper.GetOpenPopupsForXamlRoot(rootLayout.XamlRoot)
                    .Where(x => x.Child is MenuFlyoutPresenter)
                    .ToList()
                    .ForEach(x => x.IsOpen = false);
            }
        }

        private void UpdatePopupPos()
        {
            if (AppXamlHost.GetUwpInternalObject() is AppXamlHostRootLayout rootLayout)
            {
                VisualTreeHelper.GetOpenPopupsForXamlRoot(rootLayout.XamlRoot)
                    .ToList()
                    .ForEach(x => x.InvalidateMeasure());
            }
        }

        protected override IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch ((PInvoke.WindowMessage)msg)
            {
                case PInvoke.WindowMessage.WM_NCLBUTTONDOWN:
                    CloseMenuFlyout();
                    break;
                case PInvoke.WindowMessage.WM_WINDOWPOSCHANGED:
                    UpdatePopupPos();
                    break;
            }
            return base.WndProc(hWnd, msg, wParam, lParam, ref handled);
        }

        private void OnClosed(object sender, EventArgs e)
        {
            ServiceScope.Dispose();
            GC.Collect();
        }
    }
}
