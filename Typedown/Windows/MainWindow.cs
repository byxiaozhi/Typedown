using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Data;
using Typedown.Controls;
using Typedown.Services;
using Typedown.Universal.Controls;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Typedown.Utilities;
using Windows.Media.Devices;
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

        private readonly CompositeDisposable disposables = new();

        public MainWindow()
        {
            AppViewModel = ServiceProvider.GetService<AppViewModel>();
            DataContext = AppViewModel;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Width = 1500;
            Height = 900;
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            Content = AppXamlHost = new AppXamlHost() { InitialTypeName = "Typedown.Universal.Controls.RootControl" };
            SetBinding(ThemeProperty, new Binding() { Source = AppViewModel.SettingsViewModel, Path = new(nameof(SettingsViewModel.AppTheme)) });
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            (ServiceProvider.GetService<IWindowService>() as WindowService).RaiseWindowStateChanged(Handle);
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            CanGoBackChanged();
            disposables.Add(Observable.FromEventPattern(AppViewModel.GoBackCommand, nameof(AppViewModel.GoBackCommand.CanExecuteChanged)).Subscribe(_ => CanGoBackChanged()));
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            disposables.Clear();
        }

        private void CanGoBackChanged()
        {
            if (DragBar != null)
            {
                var leftSpace = AppViewModel.GoBackCommand.IsExecutable ? 32 : 0;
                var rightSpace = Universal.Config.IsMicaEnable ? 0 : 46 * 3;
                DragBar.Margin = new(leftSpace, 0, rightSpace, 0);
            }
        }

        private void CloseMenuFlyout()
        {
            if (AppXamlHost.GetUwpInternalObject() is RootControl rootControl)
            {
                VisualTreeHelper.GetOpenPopupsForXamlRoot(rootControl.XamlRoot)
                    .Where(x => x.Child is MenuFlyoutPresenter)
                    .ToList()
                    .ForEach(x => x.IsOpen = false);
            }
        }

        private void UpdatePopupPos()
        {
            if (AppXamlHost.GetUwpInternalObject() is RootControl rootControl)
            {
                VisualTreeHelper.GetOpenPopupsForXamlRoot(rootControl.XamlRoot)
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
    }
}
