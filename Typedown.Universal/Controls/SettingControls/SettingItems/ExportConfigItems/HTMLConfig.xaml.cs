using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Typedown.Universal.Controls.SettingControls.SettingItems.UploadConfigItems;
using Typedown.Universal.Models;
using Typedown.Universal.Models.ExportConfigModels;
using Typedown.Universal.Models.UploadConfigModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Typedown.Universal.Controls.SettingControls.SettingItems.ExportConfigItems
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
            HTMLConfigModel = ExportConfig.LoadExportConfig<HTMLConfigModel>();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ExportConfig.StoreExportConfig(HTMLConfigModel);
        }
    }
}
