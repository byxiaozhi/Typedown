using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Typedown.Universal.Controls.SettingControls.SettingItems.UploadConfigItems;
using Typedown.Universal.Enums;
using Typedown.Universal.Models;
using Typedown.Universal.Services;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Typedown.Universal.Pages.SettingPages
{
    public sealed partial class UploadConfigPage : Page
    {
        private static DependencyProperty ImageUploadConfigProperty { get; } = DependencyProperty.Register(nameof(ImageUploadConfig), typeof(ImageUploadConfig), typeof(UploadConfigPage), null);
        private ImageUploadConfig ImageUploadConfig { get => (ImageUploadConfig)GetValue(ImageUploadConfigProperty); set => SetValue(ImageUploadConfigProperty, value); }

        public AppViewModel ViewModel => DataContext as AppViewModel;

        public Lazy<ImageUpload> UploadService { get; }

        private int configId;

        private readonly CompositeDisposable disposables = new();

        public UploadConfigPage()
        {
            UploadService = new(() => this.GetService<ImageUpload>());
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            int.TryParse(e.Parameter.ToString(), out configId);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            ImageUploadConfig = await UploadService.Value.GetImageUploadConfig(configId);
            if (ImageUploadConfig != null)
            {
                disposables.Add(ImageUploadConfig.WhenPropertyChanged(nameof(ImageUploadConfig.Name)).Cast<string>().StartWith(ImageUploadConfig.Name).Subscribe(UpdateTitle));
            }
        }

        private async void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (ImageUploadConfig != null)
            {
                await UploadService.Value.SaveImageUploadConfig(ImageUploadConfig);
            }
            disposables.Clear();
        }

        private void UpdateTitle(string title)
        {
            this.GetAncestor<SettingsPage>()?.SetPageTitle(this, title);
        }

        public static FrameworkElement GetUploadConfigItem(ImageUploadMethod method)
        {
            return method switch
            {
                ImageUploadMethod.FTP => new FTPConfig(),
                ImageUploadMethod.Git => new GitConfig(),
                ImageUploadMethod.OSS => new OSSConfig(),
                ImageUploadMethod.SCP => new SCPConfig(),
                ImageUploadMethod.PowerShell => new PowerShellConfig(),
                _ => null
            };
        }
    }
}
