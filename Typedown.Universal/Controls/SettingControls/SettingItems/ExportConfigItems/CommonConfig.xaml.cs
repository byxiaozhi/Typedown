﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Xml.Linq;
using Typedown.Universal.Models;
using Typedown.Universal.Pages.SettingPages;
using Typedown.Universal.Utilities;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Typedown.Universal.Controls.SettingControls.SettingItems.ExportConfigItems
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
    }
}
