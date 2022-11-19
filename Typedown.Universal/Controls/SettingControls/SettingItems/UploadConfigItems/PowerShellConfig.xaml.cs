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
    public sealed partial class PowerShellConfig : UserControl
    {
        public static DependencyProperty ImageUploadConfigProperty { get; } = DependencyProperty.Register(nameof(ImageUploadConfig), typeof(ImageUploadConfig), typeof(PowerShellConfig), null);
        public ImageUploadConfig ImageUploadConfig { get => (ImageUploadConfig)GetValue(ImageUploadConfigProperty); set => SetValue(ImageUploadConfigProperty, value); }

        public static DependencyProperty PowerShellConfigModelProperty { get; } = DependencyProperty.Register(nameof(PowerShellConfigModel), typeof(PowerShellModel), typeof(PowerShellConfig), null);
        public PowerShellModel PowerShellConfigModel { get => (PowerShellModel)GetValue(PowerShellConfigModelProperty); set => SetValue(PowerShellConfigModelProperty, value); }

        public PowerShellConfig()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PowerShellConfigModel = ImageUploadConfig.LoadUploadConfig<PowerShellModel>();
            PowerShellConfigModel.PropertyChanged += OnPowerShellConfigModelPropertyChanged;
        }

        private void OnPowerShellConfigModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            ImageUploadConfig.StoreUploadConfig(PowerShellConfigModel);
        }
    }
}
