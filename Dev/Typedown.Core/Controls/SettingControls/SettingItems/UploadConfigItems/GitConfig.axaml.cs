using Typedown.Core.Models;
using Typedown.Core.Models.UploadConfigModels;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Typedown.Core.Controls.SettingControls.SettingItems.UploadConfigItems
{
    public sealed partial class GitConfig : UserControl
    {
        public static StyledProperty ImageUploadConfigProperty { get; } = AvaloniaProperty.Register<GitConfig, ImageUploadConfig>(nameof(ImageUploadConfig), null);
        public ImageUploadConfig ImageUploadConfig { get => (ImageUploadConfig)GetValue(ImageUploadConfigProperty); set => SetValue(ImageUploadConfigProperty, value); }

        public static StyledProperty GitConfigModelProperty { get; } = AvaloniaProperty.Register<GitConfig, GitConfigModel>(nameof(GitConfigModel), null);
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
             Bindings?.StopTracking();
        }
    }
}
