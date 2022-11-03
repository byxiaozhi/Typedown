using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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

        public KeyboardAccelerator KeyboardAccelerator => ServiceProvider.GetService<IKeyboardAccelerator>() as KeyboardAccelerator;

        public MainWindow()
        {
            AppViewModel = ServiceProvider.GetService<AppViewModel>();
            DataContext = AppViewModel;
            Loaded += OnLoaded;
            Closed += OnClosed;
            Closing += OnClosing;
            Activated += OnActivated;
            Deactivated += OnDeactivated;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            SetWindowPlacement();
            UpdateTitle();
            Content = AppXamlHost = new AppXamlHost(new RootControl());
            SetBinding(ThemeProperty, new Binding() { Source = AppViewModel.SettingsViewModel, Path = new(nameof(SettingsViewModel.AppTheme)) });
            SetBinding(TopmostProperty, new Binding() { Source = AppViewModel.SettingsViewModel, Path = new(nameof(SettingsViewModel.Topmost)) });
            SetBinding(IsMicaEnableProperty, new Binding() { Source = AppViewModel.SettingsViewModel, Path = new(nameof(SettingsViewModel.UseMicaEffect)) });
            AppViewModel.GoBackCommand.CanExecuteChanged += (s, e) => CanGoBackChanged();
            AppViewModel.FileViewModel.WhenPropertyChanged(nameof(FileViewModel.FileName)).Subscribe(_ => UpdateTitle());
            AppViewModel.EditorViewModel.WhenPropertyChanged(nameof(EditorViewModel.DisplaySaved)).Subscribe(_ => UpdateTitle());
        }

        private void SetWindowPlacement()
        {
            Title = "Typedown";
            MinWidth = 480;
            MinHeight = 300;
            if (AppViewModel.SettingsViewModel.WindowPlacement != null)
            {
                this.RestoreWindowPlacement(AppViewModel.SettingsViewModel.WindowPlacement);
            }
            else
            {
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
                Width = 1100;
                Height = 680;
            }
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

        private void UpdateTitle()
        {
            var title = new StringBuilder();
            if (!AppViewModel.EditorViewModel.DisplaySaved)
                title.Append('*');
            if (AppViewModel.FileViewModel.FileName != null)
                title.Append(AppViewModel.FileViewModel.FileName + " - ");
            title.Append(Assembly.GetExecutingAssembly().GetName().Name);
            Title = title.ToString();
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

        private bool isForceClose = false;

        private async void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            AppViewModel.SettingsViewModel.WindowPlacement = this.GetWindowPlacement();
            if (isForceClose) return;
            e.Cancel = true;
            await AppViewModel.FileViewModel.AutoSaveFile();
            if (AppViewModel.EditorViewModel.Saved || await AppViewModel.FileViewModel.AskToSave())
                ForceClose();
        }

        public async void ForceClose()
        {
            await Task.Yield();
            isForceClose = true;
            Close();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            ServiceScope.Dispose();
            GC.Collect();
        }

        private void OnActivated(object sender, EventArgs e)
        {
            KeyboardAccelerator.Start();
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            KeyboardAccelerator.Stop();
        }
    }
}
