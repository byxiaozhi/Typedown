using Typedown.Core.Models;
using Typedown.Core.Models.ExportConfigModels;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Typedown.Core.Controls.SettingControls.SettingItems.ExportConfigItems
{
    public sealed partial class ImageConfig : UserControl
    {
        public static StyledProperty ExportConfigProperty { get; } = AvaloniaProperty.Register<ImageConfig, ExportConfig>(nameof(ExportConfig), null);
        public ExportConfig ExportConfig { get => (ExportConfig)GetValue(ExportConfigProperty); set => SetValue(ExportConfigProperty, value); }

        public static StyledProperty ImageConfigModelProperty { get; } = AvaloniaProperty.Register<ImageConfig, HTMLConfigModel>(nameof(ImageConfigModel), null);
        public ImageConfigModel ImageConfigModel { get => (ImageConfigModel)GetValue(ImageConfigModelProperty); set => SetValue(ImageConfigModelProperty, value); }

        public ImageConfig()
        {
            this.InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ImageConfigModel = ExportConfig.LoadExportConfig() as ImageConfigModel;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ExportConfig.StoreExportConfig(ImageConfigModel);
            Bindings?.StopTracking();
        }
    }
}
