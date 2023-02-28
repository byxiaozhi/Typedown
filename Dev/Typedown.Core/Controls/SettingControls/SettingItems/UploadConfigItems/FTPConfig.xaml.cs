using Typedown.Core.Models;
using Typedown.Core.Models.UploadConfigModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.SettingControls.SettingItems.UploadConfigItems
{
    public sealed partial class FTPConfig : UserControl
    {
        public static DependencyProperty ImageUploadConfigProperty { get; } = DependencyProperty.Register(nameof(ImageUploadConfig), typeof(ImageUploadConfig), typeof(FTPConfig), null);
        public ImageUploadConfig ImageUploadConfig { get => (ImageUploadConfig)GetValue(ImageUploadConfigProperty); set => SetValue(ImageUploadConfigProperty, value); }

        public static DependencyProperty FTPConfigModelProperty { get; } = DependencyProperty.Register(nameof(FTPConfigModel), typeof(FTPConfigModel), typeof(FTPConfig), null);
        public FTPConfigModel FTPConfigModel { get => (FTPConfigModel)GetValue(FTPConfigModelProperty); set => SetValue(FTPConfigModelProperty, value); }

        public FTPConfig()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            FTPConfigModel = ImageUploadConfig.LoadUploadConfig() as FTPConfigModel;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ImageUploadConfig.StoreUploadConfig(FTPConfigModel);
        }
    }
}
