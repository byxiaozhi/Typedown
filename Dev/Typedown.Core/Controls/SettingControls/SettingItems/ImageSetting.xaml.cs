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

        /// <summary>Settings > Image > Upload with: the one configuration every upload uses.</summary>
        public UploadConfigOption UploadConfig { get; set; }

        private readonly CompositeDisposable disposables = new();

        /// <summary>Settings > Image > Upload history: what it does and how many pictures it holds.</summary>
        public string UploadHistoryDescription { get; private set; }

        private async void UpdateUploadHistoryDescription()
        {
            try
            {
                UploadHistoryDescription = string.Format(Locale.GetString("View.Image.UploadHistory.Description"), await Typedown.Core.Services.ImageUpload.History.CountAsync());
            }
            catch (Exception ex)
            {
                Log.Debug($"upload history: {ex.Message}");
            }
        }

        private async void OnClearUploadHistoryClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await Typedown.Core.Services.ImageUpload.History.ClearAsync();
            }
            catch (Exception ex)
            {
                Log.Debug($"upload history: clearing failed: {ex.Message}");
            }
            UpdateUploadHistoryDescription();
        }

        public ImageSetting()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            disposables.Add(ImageUpload.ImageUploadConfigs.GetCollectionObservable().Subscribe(_ => UpdateUploadConfigOptions()));
            UpdateUploadConfigOptions();
            UpdateUploadHistoryDescription();
        }

        public Visibility IsCopyImagePathSettingItemVisibility(InsertImageAction action)
        {
            return action == InsertImageAction.CopyToPath ? Visibility.Visible : Visibility.Collapsed;
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
            UploadConfig = UploadConfigOptions.Where(x => x.Id == Settings.DefaultImageUploadConfigId).FirstOrDefault() ?? UploadConfigOption.None;
            ImageUploadConfigsDisposables.Add(this.WhenPropertyChanged(nameof(UploadConfig)).Cast<UploadConfigOption>().Where(x => x != null).Subscribe(x => Settings.DefaultImageUploadConfigId = x.Id));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
            ImageUploadConfigsDisposables.Clear();
            Bindings?.StopTracking();
        }
    }

    public record UploadConfigOption
    {
        public static UploadConfigOption None => new() { Id = null, Name = Locale.GetString("None") };

        public int? Id { get; set; }

        public string Name { get; set; }
    }
}
