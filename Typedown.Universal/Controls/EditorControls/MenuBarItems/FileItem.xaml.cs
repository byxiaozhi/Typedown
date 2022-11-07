using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.InteropServices.WindowsRuntime;
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

namespace Typedown.Universal.Controls.EditorControls.MenuBarItems
{
    public sealed partial class FileItem : MenuBarItemBase
    {
        public FileViewModel File => ViewModel?.FileViewModel;

        public AccessHistory FileHistory => this.GetService<AccessHistory>();

        private readonly CompositeDisposable disposables = new();

        public FileItem()
        {
            InitializeComponent();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await Dispatcher.RunIdleAsync(_ => { });
            if (!IsLoaded) return;
            disposables.Add(FileHistory.FileRecentlyOpened.GetCollectionObservable().Subscribe(_ => UpdateOpenRecentItem()));
            UpdateOpenRecentItem();
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

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
        }
    }
}
