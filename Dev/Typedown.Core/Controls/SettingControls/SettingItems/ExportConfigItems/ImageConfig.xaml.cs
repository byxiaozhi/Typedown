using Typedown.Core.Models;
using Typedown.Core.Models.ExportConfigModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.SettingControls.SettingItems.ExportConfigItems
{
    public sealed partial class ImageConfig : UserControl
    {
        public static DependencyProperty ExportConfigProperty { get; } = DependencyProperty.Register(nameof(ExportConfig), typeof(ExportConfig), typeof(ImageConfig), null);
        public ExportConfig ExportConfig { get => (ExportConfig)GetValue(ExportConfigProperty); set => SetValue(ExportConfigProperty, value); }

        public static DependencyProperty ImageConfigModelProperty { get; } = DependencyProperty.Register(nameof(ImageConfigModel), typeof(HTMLConfigModel), typeof(ImageConfig), null);
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
