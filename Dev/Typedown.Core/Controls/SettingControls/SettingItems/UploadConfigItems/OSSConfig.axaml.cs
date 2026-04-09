using Typedown.Core.Models;
using Typedown.Core.Models.UploadConfigModels;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Typedown.Core.Controls.SettingControls.SettingItems.UploadConfigItems
{
    public sealed partial class OSSConfig : UserControl
    {
        public static StyledProperty ImageUploadConfigProperty { get; } = AvaloniaProperty.Register<OSSConfig, ImageUploadConfig>(nameof(ImageUploadConfig), null);
        public ImageUploadConfig ImageUploadConfig { get => (ImageUploadConfig)GetValue(ImageUploadConfigProperty); set => SetValue(ImageUploadConfigProperty, value); }

        public static StyledProperty OSSConfigModelProperty { get; } = AvaloniaProperty.Register<OSSConfig, OSSConfigModel>(nameof(OSSConfigModel), null);
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
