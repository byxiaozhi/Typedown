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
            AddEditionItems();
        }

        private bool editionItemsAdded;

        // Marks an edition's entries in the Export submenu, which the export configurations' rebuild leaves in place.
        private static readonly object EditionEntry = new();

        // An edition's own entries (EditionHooks.AddFileMenuItems, AddExportMenuItems): below Save As, and in Export
        // after the export configurations; once per menu, as the View menu's.
        private void AddEditionItems()
        {
            if (editionItemsAdded || ViewModel == null) return;
            editionItemsAdded = true;
            var index = Items.IndexOf(SaveAsItem);
            if (index >= 0)
                foreach (var item in EditionHooks.FileMenuItems(ViewModel))
                    Items.Insert(++index, item);
            var before = ExportSubMenu.Items.IndexOf(ExportSubMenu.Items.OfType<MenuFlyoutSeparator>().FirstOrDefault());
            if (before >= 0)
                foreach (var item in EditionHooks.ExportMenuItems(ViewModel))
                {
                    item.Tag = EditionEntry;
                    ExportSubMenu.Items.Insert(before++, item);
                }
        }

        /// <summary>The Export submenu's entries as shown (the automation test host reads them).</summary>
        public System.Collections.Generic.IReadOnlyList<string> ExportMenu() =>
            ExportSubMenu?.Items.OfType<MenuFlyoutItem>().Where(x => x.Visibility == Visibility.Visible).Select(x => x.Text).ToList() ?? new System.Collections.Generic.List<string>();

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
            while (ExportSubMenu.Items[1] is not MenuFlyoutSeparator && ExportSubMenu.Items[1].Tag != EditionEntry)
                ExportSubMenu.Items.RemoveAt(1);
            foreach (var config in configs.Reverse<ExportConfig>())
                ExportSubMenu.Items.Insert(1, new MenuFlyoutItem() { Text = config.Name, Command = File.ExportCommand, CommandParameter = config });
            NoExportConfigItem.Visibility = configs.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnExportSubMenuLoaded(object sender, RoutedEventArgs e)
        {
            UpdateExportItem();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings?.StopTracking();
        }
    }
}
