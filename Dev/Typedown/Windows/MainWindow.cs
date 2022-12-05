using Microsoft.Extensions.DependencyInjection;
using PropertyChanged;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Typedown.Services;
using Typedown.Core;
using Typedown.Core.Controls;
using Typedown.Core.Interfaces;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Typedown.Utilities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace Typedown.Windows
{
    public class MainWindow : AppWindow
    {
        public IServiceScope ServiceScope { get; } = Injection.ServiceProvider.CreateScope();

        public AppViewModel AppViewModel => ServiceProvider.GetService<AppViewModel>();

        public RootControl RootControl { get; } = new();

        public IServiceProvider ServiceProvider => ServiceScope?.ServiceProvider;

        public KeyboardAccelerator KeyboardAccelerator => ServiceProvider?.GetService<IKeyboardAccelerator>() as KeyboardAccelerator;

        public WindowService WindowService => ServiceProvider?.GetService<IWindowService>() as WindowService;

        public MainWindow()
        {
            Title = Config.AppName;
            MinWidth = 480;
            MinHeight = 300;
            Width = 1130;
            Height = 700;
            StartupLocation = WindowStartupLocation.CenterScreen;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            DataContext = AppViewModel;
            Content = RootControl;
            InitializeBinding();
        }

        public void InitializeBinding()
        {
            BindingOperations.SetBinding(this, ThemeProperty, new Binding() { Source = AppViewModel.SettingsViewModel, Path = new(nameof(SettingsViewModel.AppTheme)) });
            BindingOperations.SetBinding(this, TopmostProperty, new Binding() { Source = AppViewModel.SettingsViewModel, Path = new(nameof(SettingsViewModel.Topmost)) });
            BindingOperations.SetBinding(this, IsMicaEnableProperty, new Binding() { Source = AppViewModel.SettingsViewModel, Path = new(nameof(SettingsViewModel.UseMicaEffect)) });
            BindingOperations.SetBinding(this, TitleProperty, new Binding() { Source = AppViewModel.UIViewModel, Path = new(nameof(UIViewModel.MainWindowTitle)) });
            BindingOperations.SetBinding(this, CaptionHeightProperty, new Binding() { Source = AppViewModel.UIViewModel, Path = new(nameof(UIViewModel.CaptionHeight)) });
            BindingOperations.SetBinding(KeyboardAccelerator, KeyboardAccelerator.IsEnableProperty, new Binding() { Source = this, Path = new(nameof(IsActived)) });
            AppViewModel.FileViewModel.NewWindowCommand.OnExecute.Subscribe(path => Utilities.Common.OpenNewWindow(new string[] { path }));
        }

        protected override void OnCreated(EventArgs args)
        {
            base.OnCreated(args);
            this.TryRestoreWindowPlacement();
            SaveWindowPlacementWithOffset();
            AppViewModel.MainWindow = Handle;
        }

        [SuppressPropertyChangedWarnings]
        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            WindowService?.RaiseWindowStateChanged(Handle);
            SaveWindowPlacementWithOffset();
        }

        [SuppressPropertyChangedWarnings]
        protected override void OnIsActivedChanged(EventArgs args)
        {
            base.OnIsActivedChanged(args);
            WindowService?.RaiseWindowIsActivedChanged(Handle);
        }

        [SuppressPropertyChangedWarnings]
        protected override void OnScaleChanged(EventArgs args)
        {
            base.OnScaleChanged(args);
            WindowService?.RaiseWindowScaleChanged(Handle);
        }

        [SuppressPropertyChangedWarnings]
        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            SaveWindowPlacementWithOffset();
        }

        [SuppressPropertyChangedWarnings]
        protected override void OnSizeChanged(EventArgs args)
        {
            base.OnSizeChanged(args);
            SaveWindowPlacementWithOffset();
        }

        private void CloseMenuFlyout()
        {
            if (Content is FrameworkElement element && element.IsLoaded)
            {
                VisualTreeHelper.GetOpenPopupsForXamlRoot(element.XamlRoot)
                    .Where(x => x.Child is MenuFlyoutPresenter)
                    .ToList()
                    .ForEach(x => x.IsOpen = false);
            }
        }

        private void UpdatePopupPos()
        {
            if (Content is FrameworkElement element && element.IsLoaded)
            {
                VisualTreeHelper.GetOpenPopupsForXamlRoot(element.XamlRoot)
                    .ToList()
                    .ForEach(x => x.InvalidateMeasure());
            }
        }

        protected override IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
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
            return base.WndProc(hWnd, msg, wParam, lParam);
        }

        private bool isCloseable = false;

        private bool isClosing = false;

        protected async override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            try
            {
                if (!isCloseable)
                {
                    e.Cancel = true;
                    if (!isClosing)
                    {
                        isClosing = true;
                        await AppViewModel.FileViewModel.AutoSaveFile();
                        if (AppViewModel.EditorViewModel.Saved || await AppViewModel.FileViewModel.AskToSave())
                            ForceClose();
                    }
                }
            }
            finally
            {
                isClosing = false;
            }
        }

        public async void ForceClose()
        {
            this.TrySaveWindowPlacement();
            isCloseable = true;
            await Task.Yield();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            var keepRun = AppViewModel.SettingsViewModel.KeepRun;
            ServiceScope?.Dispose();
            if (!AppViewModel.GetInstances().Any())
            {
                if (keepRun)
                    Process.GetCurrentProcess().MaxWorkingSet = Process.GetCurrentProcess().MinWorkingSet;
                else
                    Program.Dispatcher.Shutdown();
            }
        }

        private bool isPlacementSaving = false;

        private async void SaveWindowPlacementWithOffset()
        {
            if (!isPlacementSaving && !isCloseable && !isClosing)
            {
                isPlacementSaving = true;
                try
                {
                    await Dispatcher.RunIdleAsync(_ => this.TrySaveWindowPlacement(new(8, 8)));
                }
                finally
                {
                    isPlacementSaving = false;
                }
            }
        }
    }
}
