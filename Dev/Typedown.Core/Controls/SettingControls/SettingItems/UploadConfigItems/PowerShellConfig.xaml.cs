using System;
using System.Collections.Generic;
using System.IO;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Models.UploadConfigModels;
using Typedown.Core.Utilities;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.SettingControls.SettingItems.UploadConfigItems
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
            var file = await FilePickersExtensions.ShowAsync(() => filePicker.PickSingleFileAsync());
            if (file != null)
                await Guarded(() => PowerShellConfigModel.Script = File.ReadAllText(file.Path));
        }

        private async void OnExportButtonClick(object sender, RoutedEventArgs e)
        {
            var filePicker = new FileSavePicker();
            filePicker.SuggestedFileName = ImageUploadConfig.Name;
            filePicker.FileTypeChoices.Add("PowerShell Cmdlet File", new List<string>() { ".ps1" });
            filePicker.SetOwnerWindow(this.GetService<IWindowService>().GetWindow(this));
            var file = await FilePickersExtensions.ShowAsync(() => filePicker.PickSaveFileAsync());
            if (file != null)
                await Guarded(() => File.WriteAllText(file.Path, PowerShellConfigModel.Script));
        }

        // A script file that cannot be read or written (locked, no access) says why, rather than ending the app.
        private async System.Threading.Tasks.Task Guarded(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Utilities.Log.Debug($"PowerShell script file: {ex}");
                await AppContentDialog.Create(Utilities.Locale.GetString("Error"), ex.Message, Utilities.Locale.GetString("Ok")).ShowAsync(XamlRoot);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
             Bindings?.StopTracking();
        }
    }
}
