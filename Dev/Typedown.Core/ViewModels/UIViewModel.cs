using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using Typedown.Core.Services;
using Typedown.Core.Utilities;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Typedown.Core.ViewModels
{
    public sealed partial class UIViewModel : INotifyPropertyChanged, IDisposable
    {
        public IServiceProvider ServiceProvider { get; }

        public AppViewModel AppViewModel => ServiceProvider.GetService<AppViewModel>();

        public EditorViewModel EditorViewModel => ServiceProvider.GetService<EditorViewModel>();

        public FileViewModel FileViewModel => ServiceProvider.GetService<FileViewModel>();

        public SettingsViewModel SettingsViewModel => ServiceProvider.GetService<SettingsViewModel>();

        public RemoteInvoke RemoteInvoke => ServiceProvider.GetService<RemoteInvoke>();

        public string MainWindowTitle { get; private set; }

        public ElementTheme ActualTheme { get; private set; }

        public double CaptionHeight { get; set; } = 32;

        private readonly CompositeDisposable disposables = new();

        private readonly UISettings uiSettings = new();

        private readonly CoreDispatcher dispatcher;

        public UIViewModel(IServiceProvider serviceProvider)
        {
            dispatcher = CoreApplication.GetCurrentView().CoreWindow.Dispatcher;
            ServiceProvider = serviceProvider;
            disposables.Add(RemoteInvoke.Handle<JToken, object>("GetStringResources", GetStringResources));
            _ = CoreApplication.GetCurrentView().CoreWindow.Dispatcher.RunIdleAsync(_ => InitializeBinding());
        }

        private void InitializeBinding()
        {
            if (disposables.IsDisposed)
                return;
            disposables.Add(EditorViewModel.WhenPropertyChanged(nameof(EditorViewModel.DisplaySaved)).Subscribe(_ => UpdateTitle()));
            disposables.Add(FileViewModel.WhenPropertyChanged(nameof(FileViewModel.FileName)).Subscribe(_ => UpdateTitle()));
            disposables.Add(Observable.FromEventPattern(uiSettings, nameof(uiSettings.ColorValuesChanged)).Merge(SettingsViewModel.WhenPropertyChanged(nameof(SettingsViewModel.AppTheme))).Subscribe(_ => UpdateActualTheme()));
            UpdateTitle();
            UpdateActualTheme();
        }

        private object GetStringResources(JToken args)
        {
            try
            {
                return args["names"].ToObject<List<string>>().ToDictionary(x => x, x => Locale.GetString(x));
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        private async void UpdateActualTheme()
        {
            await dispatcher?.TryRunIdleAsync(_ =>
            {
                try
                {
                    if (SettingsViewModel.AppTheme == Enums.AppTheme.Default)
                        ActualTheme = Application.Current.RequestedTheme == ApplicationTheme.Light ? ElementTheme.Light : ElementTheme.Dark;
                    else
                        ActualTheme = SettingsViewModel.AppTheme == Enums.AppTheme.Light ? ElementTheme.Light : ElementTheme.Dark;
                }
                catch
                {
                    // Ignore
                }
            });
        }

        private void UpdateTitle()
        {
            try
            {
                var title = new StringBuilder();
                if (!AppViewModel.EditorViewModel.DisplaySaved)
                    title.Append('*');
                if (AppViewModel.FileViewModel.FileName != null)
                    title.Append(AppViewModel.FileViewModel.FileName + " - ");
                title.Append(Config.AppName);
                MainWindowTitle = title.ToString();
            }
            catch
            {
                // Ignore
            }
        }

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
