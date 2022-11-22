using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime;
using System.Text;
using Typedown.Universal.Utilities;
using Windows.ApplicationModel;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Typedown.Universal.ViewModels
{
    public sealed partial class UIViewModel : INotifyPropertyChanged, IDisposable
    {
        public IServiceProvider ServiceProvider { get; }

        public AppViewModel AppViewModel => ServiceProvider.GetService<AppViewModel>();

        public EditorViewModel EditorViewModel => ServiceProvider.GetService<EditorViewModel>();

        public FileViewModel FileViewModel => ServiceProvider.GetService<FileViewModel>();

        public SettingsViewModel SettingsViewModel => ServiceProvider.GetService<SettingsViewModel>();

        public string MainWindowTitle { get; private set; }

        public double MenuBarWidth { get; set; }

        public ElementTheme ActualTheme { get; private set; }

        private readonly CompositeDisposable disposables = new();

        private readonly UISettings uiSettings = new();

        public UIViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            _ = App.Dispatcher.RunIdleAsync(_ => InitializeBinding());
        }

        private void InitializeBinding()
        {
            EditorViewModel.WhenPropertyChanged(nameof(EditorViewModel.DisplaySaved)).Subscribe(_ => UpdateTitle());
            FileViewModel.WhenPropertyChanged(nameof(FileViewModel.FileName)).Subscribe(_ => UpdateTitle());
            Observable.FromEventPattern(uiSettings, nameof(uiSettings.ColorValuesChanged)).Merge(SettingsViewModel.WhenPropertyChanged(nameof(SettingsViewModel.AppTheme))).Subscribe(_ => UpdataActualTheme());
            UpdateTitle();
            UpdataActualTheme();
        }

        private async void UpdataActualTheme()
        {
            await App.Dispatcher.TryRunIdleAsync(_ =>
            {
                if (SettingsViewModel.AppTheme == Enums.AppTheme.Default)
                    ActualTheme = App.Current.RequestedTheme == ApplicationTheme.Light ? ElementTheme.Light : ElementTheme.Dark;
                else
                    ActualTheme = SettingsViewModel.AppTheme == Enums.AppTheme.Light ? ElementTheme.Light : ElementTheme.Dark;
            });
        }

        private void UpdateTitle()
        {
            var title = new StringBuilder();
            if (!AppViewModel.EditorViewModel.DisplaySaved)
                title.Append('*');
            if (AppViewModel.FileViewModel.FileName != null)
                title.Append(AppViewModel.FileViewModel.FileName + " - ");
            title.Append(AppInfo.Current.DisplayInfo.DisplayName);
            MainWindowTitle = title.ToString();
        }

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
