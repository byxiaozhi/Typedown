using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Text;
using Typedown.Universal.Utilities;
using Windows.ApplicationModel;

namespace Typedown.Universal.ViewModels
{
    public sealed partial class UIViewModel : INotifyPropertyChanged, IDisposable
    {
        public IServiceProvider ServiceProvider { get; }

        public AppViewModel AppViewModel => ServiceProvider.GetService<AppViewModel>();

        public EditorViewModel EditorViewModel => ServiceProvider.GetService<EditorViewModel>();

        public FileViewModel FileViewModel => ServiceProvider.GetService<FileViewModel>();

        public string MainWindowTitle { get; private set; }

        public double MenuBarWidth { get; set; }

        private readonly CompositeDisposable disposables = new();

        public UIViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            _ = App.Dispatcher.RunIdleAsync(_ => InitializeBinding());
        }

        private void InitializeBinding()
        {
            EditorViewModel.WhenPropertyChanged(nameof(EditorViewModel.DisplaySaved)).Subscribe(_ => UpdateTitle());
            FileViewModel.WhenPropertyChanged(nameof(FileViewModel.FileName)).Subscribe(_ => UpdateTitle());
            UpdateTitle();
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
