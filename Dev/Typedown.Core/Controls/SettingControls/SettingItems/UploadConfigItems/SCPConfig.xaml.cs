using Typedown.Core.Models;
using Typedown.Core.Models.UploadConfigModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.SettingControls.SettingItems.UploadConfigItems
{
    public sealed partial class SCPConfig : UserControl
    {
        public static DependencyProperty ImageUploadConfigProperty { get; } = DependencyProperty.Register(nameof(ImageUploadConfig), typeof(ImageUploadConfig), typeof(SCPConfig), null);
        public ImageUploadConfig ImageUploadConfig { get => (ImageUploadConfig)GetValue(ImageUploadConfigProperty); set => SetValue(ImageUploadConfigProperty, value); }

        public static DependencyProperty SCPConfigModelProperty { get; } = DependencyProperty.Register(nameof(SCPConfigModel), typeof(SCPConfigModel), typeof(SCPConfig), null);
        public SCPConfigModel SCPConfigModel { get => (SCPConfigModel)GetValue(SCPConfigModelProperty); set => SetValue(SCPConfigModelProperty, value); }


        public SCPConfig()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SCPConfigModel = ImageUploadConfig.LoadUploadConfig() as SCPConfigModel;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ImageUploadConfig.StoreUploadConfig(SCPConfigModel);
        }
    }
}
