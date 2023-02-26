using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Typedown.Core;
using Typedown.Core.Controls;
using Typedown.Core.Converters;
using Typedown.Core.Interfaces;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Typedown.Services;
using Typedown.Utilities;
using Typedown.XamlUI;
using Windows.UI;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace Typedown.Windows
{
    public class MainWindow : XamlWindow
    {
        public IServiceScope ServiceScope { get; } = Injection.ServiceProvider.CreateScope();

        public AppViewModel AppViewModel => ServiceProvider.GetService<AppViewModel>();

        public RootControl RootControl { get; } = new();

        public IServiceProvider ServiceProvider => ServiceScope?.ServiceProvider;

        public KeyboardAccelerator KeyboardAccelerator => ServiceProvider?.GetService<IKeyboardAccelerator>() as KeyboardAccelerator;

        public WindowService WindowService => ServiceProvider?.GetService<IWindowService>() as WindowService;

        public Timer checkActiveTimer;

        public MainWindow()
        {
            Title = Config.AppName;
            MinWidth = 480;
            MinHeight = 300;
            Width = 1130;
            Height = 700;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            DataContext = AppViewModel;
            Content = RootControl;
            Frame = false;
            Loaded += OnLoaded;
            StateChanged += OnStateChanged;
            IsActiveChanged += OnIsActiveChanged;
            LocationChanged += OnLocationChanged;
            SizeChanged += OnSizeChanged;
            Closing += OnClosing;
            Closed += OnClosed;
            InitializeBinding();
            checkActiveTimer = new(CheckActiveTimerCallback, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1));
        }

        public void InitializeBinding()
        {
            BindingOperations.SetBinding(this, RequestedThemeProperty, new Binding() { Source = AppViewModel.SettingsViewModel, Path = new(nameof(SettingsViewModel.AppTheme)), Converter = new ElementThemeConverter() });
            BindingOperations.SetBinding(this, TopmostProperty, new Binding() { Source = AppViewModel.SettingsViewModel, Path = new(nameof(SettingsViewModel.Topmost)) });
            BindingOperations.SetBinding(this, TitleProperty, new Binding() { Source = AppViewModel.UIViewModel, Path = new(nameof(UIViewModel.MainWindowTitle)) });
            AppViewModel.FileViewModel.NewWindowCommand.OnExecute.Subscribe(path => Utilities.Common.OpenNewWindow(new string[] { path }));
            AppViewModel.SettingsViewModel.WhenPropertyChanged(nameof(SettingsViewModel.UseMicaEffect)).Cast<bool>().StartWith(AppViewModel.SettingsViewModel.UseMicaEffect).Subscribe(EnableMicaEffect);
        }

        private void EnableMicaEffect(bool enable)
        {
            RootControl.Background = enable ? new SystemBackdropBrush(this) : new SolidColorBrush(Colors.Transparent);
        }

        private void OnLoaded(object sender, EventArgs e)
        {
            this.ShowWindowWithSavedPlacement();
            SaveWindowPlacementWithOffset();
            AppViewModel.MainWindow = Handle;
        }

        private void OnStateChanged(object sender, StateChangedEventArgs e)
        {
            WindowService?.RaiseWindowStateChanged(Handle);
            SaveWindowPlacementWithOffset();
        }

        private void OnIsActiveChanged(object sender, IsActiveChangedEventArgs e)
        {
            WindowService?.RaiseWindowIsActivedChanged(Handle);
            KeyboardAccelerator.IsEnable = e.NewIsActive;
        }

        private void OnLocationChanged(object sender, LocationChangedEventArgs e)
        {
            SaveWindowPlacementWithOffset();
        }

        private void OnSizeChanged(object sender, XamlUI.SizeChangedEventArgs e)
        {
            SaveWindowPlacementWithOffset();
        }

        private bool isCloseable = false;

        private bool isClosing = false;

        private async void OnClosing(object sender, ClosingEventArgs e)
        {
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

        private void OnClosed(object sender, ClosedEventArgs e)
        {
            var keepRun = AppViewModel.SettingsViewModel.KeepRun;
            ServiceScope?.Dispose();
            checkActiveTimer?.Dispose();
            if (!AppViewModel.GetInstances().Any())
            {
                if (keepRun)
                    Process.GetCurrentProcess().MaxWorkingSet = Process.GetCurrentProcess().MinWorkingSet;
                else
                    XamlApplication.Current.Exit();
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
                    await Task.Delay(100);
                    await Dispatcher.RunIdleAsync(_ => this.TrySaveWindowPlacement(new(8, 8)));
                }
                finally
                {
                    isPlacementSaving = false;
                }
            }
        }

        private async void CheckActiveTimerCallback(object state)
        {
            await Dispatcher.RunIdleAsync(_ =>
            {
                if (Handle != 0)
                {
                    var isActive = PInvoke.GetForegroundWindow() == Handle;
                    if (isActive != KeyboardAccelerator.IsEnable)
                        KeyboardAccelerator.IsEnable = isActive;
                }
            });
        }
    }
}
