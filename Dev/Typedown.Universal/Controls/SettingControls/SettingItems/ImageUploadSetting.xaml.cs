using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Typedown.Universal.Controls.DialogControls;
using Typedown.Universal.Enums;
using Typedown.Universal.Models;
using Typedown.Universal.Models.UploadConfigModels;
using Typedown.Universal.Services;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Typedown.Universal.Controls.SettingControls.SettingItems
{
    public sealed partial class ImageUploadSetting : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        public ImageUpload ImageUpload => this.GetService<ImageUpload>();

        public ImageUploadSetting()
        {
            InitializeComponent();
        }

        private void OnAddButtonClick(object sender, RoutedEventArgs e)
        {
            AddConfigItem();
        }

        private async void AddConfigItem()
        {
            var result = await AddUploadConfigDialog.OpenAddUploadConfigDialog(XamlRoot);
            if (result == null)
                return;
            await ImageUpload.AddImageUploadConfig(result.ConfigName, result.UploadMethod);
        }

        private void OnConfigItemClick(object sender, EventArgs e)
        {
            var config = (sender as ButtonSettingItem).Tag as ImageUploadConfig;
            ViewModel.NavigateCommand.Execute($"Settings/UploadConfig?{config.Id}");
        }

        private async void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            var item = (sender as MenuFlyoutItem).DataContext as ImageUploadConfig;
            if (item != null)
                await ImageUpload.RemoveImageUploadConfig(item.Id);
        }

        public static string GetConfigItemDescription(ImageUploadMethod method, bool isEnable)
        {
            var list = new List<string>();
            list.Add(method switch
            {
                ImageUploadMethod.FTP => "FTP",
                ImageUploadMethod.Git => "Git",
                ImageUploadMethod.OSS => "OSS",
                ImageUploadMethod.SCP => "SCP",
                ImageUploadMethod.PowerShell => "PowerShell",
                _ => "未配置"
            });
            list.Add(isEnable ? "已启用" : "未启用");
            return string.Join(", ", list);
        }

        public Visibility ConfigItemsTitleVisibility(ObservableCollection<ImageUploadConfig> configs)
        {
            return configs.Any() ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
