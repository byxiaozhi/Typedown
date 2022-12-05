using System;
using System.Linq;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Services;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.EditorControls.MenuBarItems
{
    public sealed partial class FileItem : MenuBarItemBase
    {
        public FileViewModel File => ViewModel?.FileViewModel;

        public AccessHistory FileHistory => this.GetService<AccessHistory>();

        public IFileExport FileExport => this.GetService<IFileExport>();

        public FileItem()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateOpenRecentItem();
            UpdateExportItem();
        }

        protected override void OnRegisterShortcut()
        {
            RegisterWindowShortcut(Settings.ShortcutNewFile, NewFileItem);
            RegisterWindowShortcut(Settings.ShortcutNewWindow, NewWindowItem);
            RegisterWindowShortcut(Settings.ShortcutOpenFile, OpenFileItem);
            RegisterWindowShortcut(Settings.ShortcutOpenFolder, OpenFolderItem);
            RegisterWindowShortcut(Settings.ShortcutClearRecentFiles, ClearRecentFilesItem);
            RegisterWindowShortcut(Settings.ShortcutSave, SaveItem);
            RegisterWindowShortcut(Settings.ShortcutSaveAs, SaveAsItem);
            RegisterWindowShortcut(Settings.ShortcutExportSettings, ExportSettingsItem);
            RegisterWindowShortcut(Settings.ShortcutPrint, PrintItem);
            RegisterWindowShortcut(Settings.ShortcutSettings, SettingItem);
            RegisterWindowShortcut(Settings.ShortcutClose, CloseItem);
        }

        private void UpdateOpenRecentItem()
        {
            var files = FileHistory.FileRecentlyOpened.ToList();
            while (OpenRecentSubMenu.Items[1] is not MenuFlyoutSeparator)
                OpenRecentSubMenu.Items.RemoveAt(1);
            foreach (var file in files.Reverse<string>())
                OpenRecentSubMenu.Items.Insert(1, new MenuFlyoutItem() { Text = file, Command = File.OpenFileCommand, CommandParameter = file });
            NoRecentFilesItem.Visibility = files.Any() ? Visibility.Collapsed : Visibility.Visible;
            ClearRecentFilesItem.IsEnabled = files.Any();
        }

        private void OnOpenRecentSubMenuLoaded(object sender, RoutedEventArgs e)
        {
            UpdateOpenRecentItem();
        }

        private void UpdateExportItem()
        {
            var configs = FileExport.ExportConfigs.ToList();
            while (ExportSubMenu.Items[1] is not MenuFlyoutSeparator)
                ExportSubMenu.Items.RemoveAt(1);
            foreach (var config in configs.Reverse<ExportConfig>())
                ExportSubMenu.Items.Insert(1, new MenuFlyoutItem() { Text = config.Name, Command = File.ExportCommand, CommandParameter = config });
            NoExportConfigItem.Visibility = configs.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnExportSubMenuLoaded(object sender, RoutedEventArgs e)
        {
            UpdateExportItem();
        }
    }
}
