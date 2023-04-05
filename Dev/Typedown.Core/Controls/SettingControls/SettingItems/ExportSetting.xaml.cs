using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Typedown.Core.Controls.DialogControls;
using Typedown.Core.Enums;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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

        internal static void OnConfigItemClick(object sender, EventArgs e)
        {
            var buttonItem = sender as ButtonSettingItem;
            if (buttonItem?.GetAncestor<ExportSetting>() is not ExportSetting exportSetting)
                return;
            var config = buttonItem.DataContext as ExportConfig;
            exportSetting.ViewModel.NavigateCommand.Execute($"Settings/ExportConfig?{config.Id}");
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

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings?.StopTracking();
            ConfigItemMenuFlyout.Items.Clear();
        }
    }
}
