using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Typedown.Core.Enums;
using Typedown.Core.Services;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.SettingControls.SettingItems
{
    public sealed partial class ImageSetting : UserControl, INotifyPropertyChanged
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        public ImageUpload ImageUpload => this.GetService<ImageUpload>();

        public ObservableCollection<UploadConfigOption> UploadConfigOptions { get; } = new();

        public UploadConfigOption ClipboardImageUploadConfig { get; set; }

        public UploadConfigOption LocalImageUploadConfig { get; set; }

        public UploadConfigOption WebImageUploadConfig { get; set; }

        private readonly CompositeDisposable disposables = new();

        public ImageSetting()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            disposables.Add(ImageUpload.ImageUploadConfigs.GetCollectionObservable().Subscribe(_ => UpdateUploadConfigOptions()));
            UpdateUploadConfigOptions();
        }

        public Visibility IsCopyImagePathSettingItemVisibility(InsertImageAction action)
        {
            return action == InsertImageAction.CopyToPath ? Visibility.Visible : Visibility.Collapsed;
        }

        public Visibility IsSelectUploadConfigSettingItemVisibility(InsertImageAction action)
        {
            return action == InsertImageAction.Upload ? Visibility.Visible : Visibility.Collapsed;
        }

        private readonly CompositeDisposable ImageUploadConfigsDisposables = new();

        private async void UpdateUploadConfigOptions()
        {
            ImageUploadConfigsDisposables.Clear();
            foreach (var config in ImageUpload.ImageUploadConfigs)
                ImageUploadConfigsDisposables.Add(config.WhenPropertyChanged(nameof(config.IsEnable)).Subscribe(_ => UpdateUploadConfigOptions()));
            UploadConfigOptions.UpdateCollection(ImageUpload.ImageUploadConfigs
                .Where(x => x.IsEnable)
                .Select(x => new UploadConfigOption() { Id = x.Id, Name = x.Name })
                .Append(UploadConfigOption.None)
                .ToList(),
                (a, b) => a.Id == b.Id);
            await Task.Yield();
            ClipboardImageUploadConfig = UploadConfigOptions.Where(x => x.Id == Settings.InsertClipboardImageUseUploadConfigId).FirstOrDefault() ?? UploadConfigOption.None;
            LocalImageUploadConfig = UploadConfigOptions.Where(x => x.Id == Settings.InsertLocalImageUseUploadConfigId).FirstOrDefault() ?? UploadConfigOption.None;
            WebImageUploadConfig = UploadConfigOptions.Where(x => x.Id == Settings.InsertWebImageUseUploadConfigId).FirstOrDefault() ?? UploadConfigOption.None;
            ImageUploadConfigsDisposables.Add(this.WhenPropertyChanged(nameof(ClipboardImageUploadConfig)).Cast<UploadConfigOption>().Subscribe(x => Settings.InsertClipboardImageUseUploadConfigId = x.Id));
            ImageUploadConfigsDisposables.Add(this.WhenPropertyChanged(nameof(LocalImageUploadConfig)).Cast<UploadConfigOption>().Subscribe(x => Settings.InsertLocalImageUseUploadConfigId = x.Id));
            ImageUploadConfigsDisposables.Add(this.WhenPropertyChanged(nameof(WebImageUploadConfig)).Cast<UploadConfigOption>().Subscribe(x => Settings.InsertWebImageUseUploadConfigId = x.Id));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
            ImageUploadConfigsDisposables.Clear();
            Bindings.StopTracking();
        }
    }

    public record UploadConfigOption
    {
        public static UploadConfigOption None => new() { Id = null, Name = Locale.GetString("None") };

        public int? Id { get; set; }

        public string Name { get; set; }
    }
}
