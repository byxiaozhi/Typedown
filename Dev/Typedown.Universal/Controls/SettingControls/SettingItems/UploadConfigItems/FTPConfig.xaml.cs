using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Typedown.Universal.Models;
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

namespace Typedown.Universal.Controls.SettingControls.SettingItems.UploadConfigItems
{
    public sealed partial class FTPConfig : UserControl
    {
        public static DependencyProperty ImageUploadConfigProperty { get; } = DependencyProperty.Register(nameof(ImageUploadConfig), typeof(ImageUploadConfig), typeof(FTPConfig), null);
        public ImageUploadConfig ImageUploadConfig { get => (ImageUploadConfig)GetValue(ImageUploadConfigProperty); set => SetValue(ImageUploadConfigProperty, value); }

        public static DependencyProperty FTPConfigModelProperty { get; } = DependencyProperty.Register(nameof(FTPConfigModel), typeof(FTPConfigModel), typeof(FTPConfig), null);
        public FTPConfigModel FTPConfigModel { get => (FTPConfigModel)GetValue(FTPConfigModelProperty); set => SetValue(FTPConfigModelProperty, value); }

        public FTPConfig()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            FTPConfigModel = ImageUploadConfig.LoadUploadConfig() as FTPConfigModel;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ImageUploadConfig.StoreUploadConfig(FTPConfigModel);
        }
    }
}
