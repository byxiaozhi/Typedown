using Typedown.Core.Models;
using Typedown.Core.Models.ExportConfigModels;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Typedown.Core.Controls.SettingControls.SettingItems.ExportConfigItems
{
    public sealed partial class HTMLConfig : UserControl
    {
        public static StyledProperty ExportConfigProperty { get; } = AvaloniaProperty.Register<HTMLConfig, ExportConfig>(nameof(ExportConfig), null);
        public ExportConfig ExportConfig { get => (ExportConfig)GetValue(ExportConfigProperty); set => SetValue(ExportConfigProperty, value); }

        public static StyledProperty HTMLConfigModelProperty { get; } = AvaloniaProperty.Register<HTMLConfig, HTMLConfigModel>(nameof(HTMLConfigModel), null);
        public HTMLConfigModel HTMLConfigModel { get => (HTMLConfigModel)GetValue(HTMLConfigModelProperty); set => SetValue(HTMLConfigModelProperty, value); }

        public HTMLConfig()
        {
            this.InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            HTMLConfigModel = ExportConfig.LoadExportConfig() as HTMLConfigModel;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ExportConfig.StoreExportConfig(HTMLConfigModel);
            Bindings?.StopTracking();
        }
    }
}
