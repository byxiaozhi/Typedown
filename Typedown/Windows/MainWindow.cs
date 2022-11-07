using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Typedown.Controls;
using Typedown.Services;
using Typedown.Universal.Controls;
using Typedown.Universal.Enums;
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
        public IServiceScope ServiceScope { get; private set; }

        public AppViewModel AppViewModel { get; private set; }

        public AppXamlHost AppXamlHost { get; private set; }

        public System.Windows.Controls.Grid RootLayout { get; private set; }

        public RootControl RootControl { get; private set; }

        public IServiceProvider ServiceProvider => ServiceScope?.ServiceProvider;

        public KeyboardAccelerator KeyboardAccelerator => ServiceProvider?.GetService<IKeyboardAccelerator>() as KeyboardAccelerator;

        public WindowService WindowService => ServiceProvider?.GetService<IWindowService>() as WindowService;

        public MainWindow()
        {
            Theme = (AppTheme)Properties.Settings.Default.StartupTheme;
            Title = "Typedown";
            MinWidth = 480;
            MinHeight = 300;
            Loaded += OnLoaded;
            Closed += OnClosed;
            Closing += OnClosing;
            Activated += OnActivated;
            Deactivated += OnDeactivated;
            LocationChanged += OnLocationChanged;
            this.RestoreWindowPlacement();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            RootLayout ??= new() { Children = { new Controls.SplashScreen() } };
            Content ??= RootLayout;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            SaveWindowPlacementWithOffset();
            Universal.App.InitializeXAMLIsland();
            InitializeComponent();
            AppViewModel.MainWindow = Handle;
        }

        public void InitializeComponent()
        {
            if (ServiceScope != null)
                return;
            ServiceScope = Injection.ServiceProvider.CreateScope();
            DataContext = AppViewModel = ServiceProvider.GetService<AppViewModel>();
            RootControl = new RootControl();
            AppXamlHost = new AppXamlHost(RootControl);
            RootControl.Loaded += OnRootControlLoaded;
            RootLayout ??= new();
            Content ??= RootLayout;
            RootLayout.Children.Add(AppXamlHost);
            InitializeBinding();
            Dispatcher.InvokeAsync(SetMaxWorkingSetSize, DispatcherPriority.SystemIdle);
        }

        public void InitializeBinding()
        {
            SetBinding(ThemeProperty, new Binding() { Source = AppViewModel.SettingsViewModel, Path = new(nameof(SettingsViewModel.AppTheme)) });
            SetBinding(TopmostProperty, new Binding() { Source = AppViewModel.SettingsViewModel, Path = new(nameof(SettingsViewModel.Topmost)) });
            SetBinding(IsMicaEnableProperty, new Binding() { Source = AppViewModel.SettingsViewModel, Path = new(nameof(SettingsViewModel.UseMicaEffect)) });
            SetBinding(TitleProperty, new Binding() { Source = AppViewModel.UIViewModel, Path = new(nameof(UIViewModel.MainWindowTitle)) });
            AppViewModel.SettingsViewModel.WhenPropertyChanged(nameof(SettingsViewModel.AppTheme)).Subscribe(_ => UpdateStartupTheme());
            KeyboardAccelerator.IsEnable = IsActive;
        }

        private async void OnRootControlLoaded(object sender, global::Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (RootLayout.Children.OfType<Controls.SplashScreen>().FirstOrDefault() is Controls.SplashScreen splashScreen)
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                RootLayout.Children.Remove(splashScreen);
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            WindowService?.RaiseWindowStateChanged(Handle);
            SaveWindowPlacementWithOffset();
        }

        private void CloseMenuFlyout()
        {
            if (AppXamlHost?.GetUwpInternalObject() is AppXamlHostRootLayout rootLayout)
            {
                VisualTreeHelper.GetOpenPopupsForXamlRoot(rootLayout.XamlRoot)
                    .Where(x => x.Child is MenuFlyoutPresenter)
                    .ToList()
                    .ForEach(x => x.IsOpen = false);
            }
        }

        private void UpdatePopupPos()
        {
            if (AppXamlHost?.GetUwpInternalObject() is AppXamlHostRootLayout rootLayout)
            {
                VisualTreeHelper.GetOpenPopupsForXamlRoot(rootLayout.XamlRoot)
                    .ToList()
                    .ForEach(x => x.InvalidateMeasure());
            }
        }

        private void UpdateStartupTheme()
        {
            Properties.Settings.Default.StartupTheme = (int)AppViewModel.SettingsViewModel.AppTheme;
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

        private bool isCloseable = false;

        private async void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!isCloseable)
            {
                e.Cancel = true;
                await AppViewModel.FileViewModel.AutoSaveFile();
                if (AppViewModel.EditorViewModel.Saved || await AppViewModel.FileViewModel.AskToSave())
                    ForceClose();
            }
        }

        public async void ForceClose()
        {
            this.SaveWindowPlacement();
            await Task.Yield();
            isCloseable = true;
            Close();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            var keepRun = AppViewModel.SettingsViewModel.KeepRun;
            ServiceScope?.Dispose();
            if (!AppViewModel.GetInstances().Any() && !keepRun)
                Application.Current.Shutdown();
            SetMaxWorkingSetSize();
        }

        private void OnActivated(object sender, EventArgs e)
        {
            KeyboardAccelerator?.Start();
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            KeyboardAccelerator?.Stop();
        }

        private bool savingWindowPlacement = false;

        private async void OnLocationChanged(object sender, EventArgs e)
        {
            if (!savingWindowPlacement)
            {
                savingWindowPlacement = true;
                await Task.Delay(1000);
                if (IsLoaded) SaveWindowPlacementWithOffset();
                savingWindowPlacement = false;
            }
        }

        private void SaveWindowPlacementWithOffset()
        {
            this.SaveWindowPlacement(new(8, 8));
        }

        private void SetMaxWorkingSetSize()
        {
            var baseSize = 10 * 1024 * 1024;
            var instanceSize = 20 * 1024 * 1024;
            var totalSize = baseSize + AppViewModel.GetInstances().Count * instanceSize;
            Process.GetCurrentProcess().MaxWorkingSet = new(totalSize);
        }
    }
}
