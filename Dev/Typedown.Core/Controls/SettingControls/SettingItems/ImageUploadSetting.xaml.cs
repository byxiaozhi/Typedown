using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Typedown.Core.Controls.DialogControls;
using Typedown.Core.Enums;
using Typedown.Core.Models;
using Typedown.Core.Services;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.SettingControls.SettingItems
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
            try
            {
                var result = await AddUploadConfigDialog.OpenAddUploadConfigDialog(XamlRoot);
                if (result == null)
                    return;
                await ImageUpload.AddImageUploadConfig(result.ConfigName, result.UploadMethod);
            }
            catch (Exception ex)
            {
                await ShowError(ex);
            }
        }

        // A configuration the database would not take (locked, damaged) says why, rather than ending the app.
        private async System.Threading.Tasks.Task ShowError(Exception ex)
        {
            Log.WriteLocal("ImageUploadConfig", ex.ToString());
            try { await AppContentDialog.Create(Locale.GetString("Error"), ex.Message, Locale.GetString("Ok")).ShowAsync(XamlRoot); } catch { }
        }

        internal static void OnConfigItemClick(object sender, EventArgs e)
        {
            var buttonItem = sender as ButtonSettingItem;
            if (buttonItem?.GetAncestor<ImageUploadSetting>() is not ImageUploadSetting uploadSetting)
                return;
            var config = (sender as ButtonSettingItem).Tag as ImageUploadConfig;
            uploadSetting.ViewModel.NavigateCommand.Execute($"Settings/UploadConfig?{config.Id}");
        }

        private async void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            var item = (sender as MenuFlyoutItem).DataContext as ImageUploadConfig;
            try
            {
                if (item != null)
                    await ImageUpload.RemoveImageUploadConfig(item.Id);
            }
            catch (Exception ex)
            {
                await ShowError(ex);
            }
        }

        public static string GetConfigItemDescription(ImageUploadMethod method, bool isEnable)
        {
            var list = new List<string>();
            var field = method.GetType().GetField(method.ToString());
            var attribute = field.GetCustomAttribute(typeof(LocaleAttribute)) as LocaleAttribute;
            list.Add(attribute.Text);
            list.Add(isEnable ? Locale.GetString("On") : Locale.GetString("Off"));
            return string.Join(", ", list);
        }

        public Visibility ConfigItemsTitleVisibility(ObservableCollection<ImageUploadConfig> configs)
        {
            return configs.Any() ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings?.StopTracking();
            ConfigItemMenuFlyout.Items.Clear();
        }
    }
}
