using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Typedown.Core;
using Typedown.Core.Controls;
using Typedown.Core.Enums;
using Typedown.Core.Interfaces;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Typedown.Services;
using Typedown.Utilities;
using Typedown.XamlUI;
using Windows.Globalization;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Typedown.Windows
{
    public class MainWindow : XamlWindow
    {
        public IServiceScope ServiceScope { get; private set; } = Injection.ServiceProvider.CreateScope();

        public AppViewModel AppViewModel => ServiceProvider.GetService<AppViewModel>();

        public RootControl RootControl { get; private set; } = new();

        public IServiceProvider ServiceProvider => ServiceScope?.ServiceProvider;

        public KeyboardAccelerator KeyboardAccelerator => ServiceProvider?.GetService<IKeyboardAccelerator>() as KeyboardAccelerator;

        public WindowService WindowService => ServiceProvider?.GetService<IWindowService>() as WindowService;

        public CompositeDisposable disposables = new();

        public Timer checkActiveTimer;

        public MainWindow()
        {
            TrySetPrimaryLanguage();
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
            disposables.Add(AppViewModel.SettingsViewModel.WhenPropertyChanged(nameof(SettingsViewModel.AppTheme)).Cast<AppTheme>().StartWith(AppViewModel.SettingsViewModel.AppTheme).Subscribe(SetTheme));
            disposables.Add(AppViewModel.UIViewModel.WhenPropertyChanged(nameof(UIViewModel.MainWindowTitle)).Cast<string>().StartWith(AppViewModel.UIViewModel.MainWindowTitle).Subscribe(SetTitle));
            disposables.Add(AppViewModel.FileViewModel.NewWindowCommand.OnExecute.Subscribe(path => Utilities.Common.OpenNewWindow(new string[] { path })));
            disposables.Add(AppViewModel.SettingsViewModel.WhenPropertyChanged(nameof(SettingsViewModel.UseMicaEffect)).Cast<bool>().StartWith(AppViewModel.SettingsViewModel.UseMicaEffect).Subscribe(EnableMicaEffect));
            disposables.Add(AppViewModel.SettingsViewModel.WhenPropertyChanged(nameof(SettingsViewModel.Topmost)).Cast<bool>().StartWith(AppViewModel.SettingsViewModel.Topmost).Subscribe(SetTopmost));
        }

        private void SetTheme(AppTheme theme)
        {
            RequestedTheme = theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
        }

        private void SetTitle(string title)
        {
            Title = title;
        }

        private void EnableMicaEffect(bool enable)
        {
            RootControl.Background = enable ? new SystemBackdropBrush(this) : new SolidColorBrush(Colors.Transparent);
        }

        private void OnLoaded(object sender, EventArgs e)
        {
            this.ShowWindowWithSavedPlacement();
            SaveWindowPlacementWithOffset(false);
            AppViewModel.MainWindow = Handle;
        }

        private void OnStateChanged(object sender, StateChangedEventArgs e)
        {
            WindowService?.RaiseWindowStateChanged(Handle);
            if (RootControl?.IsLoaded ?? false)
                SaveWindowPlacementWithOffset();
        }

        private void OnIsActiveChanged(object sender, IsActiveChangedEventArgs e)
        {
            WindowService?.RaiseWindowIsActivedChanged(Handle);
            KeyboardAccelerator.IsEnable = e.NewIsActive;
        }

        private void OnLocationChanged(object sender, LocationChangedEventArgs e)
        {
            if (RootControl?.IsLoaded ?? false)
                SaveWindowPlacementWithOffset();
        }

        private void OnSizeChanged(object sender, XamlUI.SizeChangedEventArgs e)
        {
            if (RootControl?.IsLoaded ?? false)
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
            if (Handle != default)
                Close();
        }

        private void OnClosed(object sender, ClosedEventArgs e)
        {
            var keepRun = AppViewModel.SettingsViewModel.KeepRun;
            checkActiveTimer?.Dispose();
            checkActiveTimer = null;
            ServiceScope?.Dispose();
            ServiceScope = null;
            RootControl = null;
            Content = null;
            DataContext = null;
            disposables.Dispose();
            if (!AppViewModel.GetInstances().Any())
            {
                if (keepRun)
                    Process.GetCurrentProcess().MaxWorkingSet = Process.GetCurrentProcess().MinWorkingSet;
                else
                    XamlApplication.Current.Exit();
            }
        }

        private bool isPlacementSaving = false;

        private async void SaveWindowPlacementWithOffset(bool throttle = true)
        {
            if (!isPlacementSaving && !isCloseable && !isClosing)
            {
                isPlacementSaving = true;
                try
                {
                    if (throttle) await Task.Delay(100);
                    await Dispatcher.RunAsync(() => this.TrySaveWindowPlacement(new(8, 8)));
                }
                finally
                {
                    isPlacementSaving = false;
                }
            }
        }

        private void CheckActiveTimerCallback(object state)
        {
            _ = Dispatcher.RunIdleAsync(() =>
            {
                if (Handle != 0)
                {
                    var isActive = PInvoke.GetForegroundWindow() == Handle;
                    if (isActive != KeyboardAccelerator.IsEnable)
                        KeyboardAccelerator.IsEnable = isActive;
                }
            });
        }

        private void TrySetPrimaryLanguage()
        {
            try
            {
                var settingLanguage = AppViewModel.SettingsViewModel.Language;
                if (Locale.SupportedLangs.ContainsKey(settingLanguage))
                    ApplicationLanguages.PrimaryLanguageOverride = settingLanguage;
                else
                    ApplicationLanguages.PrimaryLanguageOverride = string.Empty;
            }
            catch
            {

            }
        }
    }
}
