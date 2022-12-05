using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Typedown.Core.Controls.DialogControls;
using Typedown.Core.Enums;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Services;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Typedown.Core.Controls.SettingControls.SettingItems
{
    public sealed partial class ExportSetting : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        public IFileExport FileExport => this.GetService<IFileExport>();

        public ExportSetting()
        {
            InitializeComponent();
        }

        private void OnAddButtonClick(object sender, RoutedEventArgs e)
        {
            AddConfigItem();
        }

        private async void AddConfigItem()
        {
            var result = await AddExportConfigDialog.OpenAddExportConfigDialog(XamlRoot);
            if (result == null)
                return;
            await FileExport.AddExportConfig(result.ConfigName, result.ExportType);
        }

        private void OnConfigItemClick(object sender, EventArgs e)
        {
            var config = (sender as ButtonSettingItem).Tag as ExportConfig;
            ViewModel.NavigateCommand.Execute($"Settings/ExportConfig?{config.Id}");
        }

        private async void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            var item = (sender as MenuFlyoutItem).DataContext as ExportConfig;
            if (item != null)
                await FileExport.RemoveExportConfig(item.Id);
        }

        public static string GetConfigItemDescription(ExportType method)
        {
            var list = new List<string>();
            var field = method.GetType().GetField(method.ToString());
            var attribute = field.GetCustomAttribute(typeof(LocaleAttribute)) as LocaleAttribute;
            list.Add(attribute.Text);
            return string.Join(", ", list);
        }

        public Visibility ConfigItemsTitleVisibility(ObservableCollection<ExportConfig> configs)
        {
            return configs.Any() ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
