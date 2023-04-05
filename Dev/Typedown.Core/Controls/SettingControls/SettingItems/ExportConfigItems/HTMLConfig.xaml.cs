using Typedown.Core.Models;
using Typedown.Core.Models.ExportConfigModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.SettingControls.SettingItems.ExportConfigItems
{
    public sealed partial class HTMLConfig : UserControl
    {
        public static DependencyProperty ExportConfigProperty { get; } = DependencyProperty.Register(nameof(ExportConfig), typeof(ExportConfig), typeof(HTMLConfig), null);
        public ExportConfig ExportConfig { get => (ExportConfig)GetValue(ExportConfigProperty); set => SetValue(ExportConfigProperty, value); }

        public static DependencyProperty HTMLConfigModelProperty { get; } = DependencyProperty.Register(nameof(HTMLConfigModel), typeof(HTMLConfigModel), typeof(HTMLConfig), null);
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
