using Typedown.Core.Models;
using Typedown.Core.Models.UploadConfigModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.SettingControls.SettingItems.UploadConfigItems
{
    public sealed partial class GitConfig : UserControl
    {
        public static DependencyProperty ImageUploadConfigProperty { get; } = DependencyProperty.Register(nameof(ImageUploadConfig), typeof(ImageUploadConfig), typeof(GitConfig), null);
        public ImageUploadConfig ImageUploadConfig { get => (ImageUploadConfig)GetValue(ImageUploadConfigProperty); set => SetValue(ImageUploadConfigProperty, value); }

        public static DependencyProperty GitConfigModelProperty { get; } = DependencyProperty.Register(nameof(GitConfigModel), typeof(GitConfigModel), typeof(GitConfig), null);
        public GitConfigModel GitConfigModel { get => (GitConfigModel)GetValue(GitConfigModelProperty); set => SetValue(GitConfigModelProperty, value); }

        public GitConfig()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            GitConfigModel = ImageUploadConfig.LoadUploadConfig() as GitConfigModel;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ImageUploadConfig.StoreUploadConfig(GitConfigModel);
            Bindings.StopTracking();
        }
    }
}
