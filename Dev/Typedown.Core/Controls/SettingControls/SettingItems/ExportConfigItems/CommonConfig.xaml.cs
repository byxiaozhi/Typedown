using Typedown.Core.Models;
using Typedown.Core.Models.UploadConfigModels;
using Typedown.Core.Pages.SettingPages;
using Typedown.Core.Utilities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;

namespace Typedown.Core.Controls.SettingControls.SettingItems.ExportConfigItems
{
    [ContentProperty(Name = nameof(Detail))]
    public sealed partial class CommonConfig : UserControl
    {
        public static DependencyProperty ExportConfigProperty { get; } = DependencyProperty.Register(nameof(ExportConfig), typeof(ExportConfig), typeof(CommonConfig), null);
        public ExportConfig ExportConfig { get => (ExportConfig)GetValue(ExportConfigProperty); set => SetValue(ExportConfigProperty, value); }

        public static DependencyProperty DetailProperty { get; } = DependencyProperty.Register(nameof(Detail), typeof(UIElement), typeof(CommonConfig), null);
        public UIElement Detail { get => (UIElement)GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

        public CommonConfig()
        {
            this.InitializeComponent();
        }

        private void OnDeleteButtonClick(object sender, RoutedEventArgs e)
        {
            this.GetAncestor<ExportConfigPage>()?.DeleteConfigAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings?.StopTracking();
        }
    }
}
