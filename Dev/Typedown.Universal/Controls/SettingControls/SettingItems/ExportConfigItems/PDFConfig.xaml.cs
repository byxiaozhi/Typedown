using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Typedown.Universal.Models;
using Typedown.Universal.Models.ExportConfigModels;
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
    public sealed partial class PDFConfig : UserControl
    {
        public static DependencyProperty ExportConfigProperty { get; } = DependencyProperty.Register(nameof(ExportConfig), typeof(ExportConfig), typeof(PDFConfig), null);
        public ExportConfig ExportConfig { get => (ExportConfig)GetValue(ExportConfigProperty); set => SetValue(ExportConfigProperty, value); }

        public static DependencyProperty PDFConfigModelProperty { get; } = DependencyProperty.Register(nameof(ImageConfigModel), typeof(PDFConfigModel), typeof(PDFConfig), null);
        public PDFConfigModel PDFConfigModel { get => (PDFConfigModel)GetValue(PDFConfigModelProperty); set => SetValue(PDFConfigModelProperty, value); }

        public PDFConfig()
        {
            this.InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PDFConfigModel = ExportConfig.LoadExportConfig() as PDFConfigModel;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ExportConfig.StoreExportConfig(PDFConfigModel);
        }
    }
}
