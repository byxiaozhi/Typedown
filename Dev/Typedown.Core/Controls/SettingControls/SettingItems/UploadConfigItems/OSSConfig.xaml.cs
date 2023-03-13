using Typedown.Core.Models;
using Typedown.Core.Models.UploadConfigModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.SettingControls.SettingItems.UploadConfigItems
{
    public sealed partial class OSSConfig : UserControl
    {
        public static DependencyProperty ImageUploadConfigProperty { get; } = DependencyProperty.Register(nameof(ImageUploadConfig), typeof(ImageUploadConfig), typeof(OSSConfig), null);
        public ImageUploadConfig ImageUploadConfig { get => (ImageUploadConfig)GetValue(ImageUploadConfigProperty); set => SetValue(ImageUploadConfigProperty, value); }

        public static DependencyProperty OSSConfigModelProperty { get; } = DependencyProperty.Register(nameof(OSSConfigModel), typeof(OSSConfigModel), typeof(OSSConfig), null);
        public OSSConfigModel OSSConfigModel { get => (OSSConfigModel)GetValue(OSSConfigModelProperty); set => SetValue(OSSConfigModelProperty, value); }

        public OSSConfig()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            OSSConfigModel = ImageUploadConfig.LoadUploadConfig() as OSSConfigModel;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ImageUploadConfig.StoreUploadConfig(OSSConfigModel);
             Bindings?.StopTracking();
        }
    }
}
