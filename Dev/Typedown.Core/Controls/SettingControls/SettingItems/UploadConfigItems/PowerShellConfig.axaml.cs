using System;
using System.Collections.ObjectModel;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using System.Collections.Generic;
using System.IO;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Models.UploadConfigModels;
using Typedown.Core.Utilities;
using Avalonia.Platform.Storage;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Typedown.Core.Controls.SettingControls.SettingItems.UploadConfigItems
{
    public sealed partial class PowerShellConfig : UserControl
    {
        public static StyledProperty ImageUploadConfigProperty { get; } = AvaloniaProperty.Register<PowerShellConfig, ImageUploadConfig>(nameof(ImageUploadConfig), null);
        public ImageUploadConfig ImageUploadConfig { get => (ImageUploadConfig)GetValue(ImageUploadConfigProperty); set => SetValue(ImageUploadConfigProperty, value); }

        public static StyledProperty PowerShellConfigModelProperty { get; } = AvaloniaProperty.Register<PowerShellConfig, PowerShellModel>(nameof(PowerShellConfigModel), null);
        public PowerShellModel PowerShellConfigModel { get => (PowerShellModel)GetValue(PowerShellConfigModelProperty); set => SetValue(PowerShellConfigModelProperty, value); }

        public PowerShellConfig()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PowerShellConfigModel = ImageUploadConfig.LoadUploadConfig() as PowerShellModel;
            PowerShellConfigModel.PropertyChanged += OnPowerShellConfigModelPropertyChanged;
        }

        private void OnPowerShellConfigModelPropertyChanged(object sender, global::System.ComponentModel.PropertyChangedEventArgs e)
        {
            ImageUploadConfig.StoreUploadConfig(PowerShellConfigModel);
        }

        private async void OnImportButtonClick(object sender, RoutedEventArgs e)
        {
            var filePicker = new FileOpenPicker();
            filePicker.FileTypeFilter.Add(".ps1");
            filePicker.SetOwnerWindow(this.GetService<IWindowService>().GetWindow(this));
            var file = await filePicker.PickSingleFileAsync();
            if (file != null)
                PowerShellConfigModel.Script = File.ReadAllText(file.Path);
        }

        private async void OnExportButtonClick(object sender, RoutedEventArgs e)
        {
            var filePicker = new FileSavePicker();
            filePicker.SuggestedFileName = ImageUploadConfig.Name;
            filePicker.FileTypeChoices.Add("PowerShell Cmdlet File", new List<string>() { ".ps1" });
            filePicker.SetOwnerWindow(this.GetService<IWindowService>().GetWindow(this));
            var file = await filePicker.PickSaveFileAsync();
            if (file != null)
                File.WriteAllText(file.Path, PowerShellConfigModel.Script);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
             Bindings?.StopTracking();
        }
    }
}
