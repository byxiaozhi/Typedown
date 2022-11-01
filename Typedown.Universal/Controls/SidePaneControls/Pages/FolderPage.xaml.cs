using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Models;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using muxc = Microsoft.UI.Xaml.Controls;

namespace Typedown.Universal.Controls.SidePanelControls.Pages
{
    public sealed partial class FolderPage : Page, INotifyPropertyChanged
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        public IFileOperation FileOperation => this.GetService<IFileOperation>();

        public IClipboard Clipboard => this.GetService<IClipboard>();

        public ExplorerItem WorkFolderExplorerItem { get; private set; }

        private readonly CompositeDisposable disposables = new();

        public FolderPage()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WorkFolderExplorerItem = new ExplorerItem() { IsExpanded = true };
            disposables.Add(Settings.WhenPropertyChanged(nameof(Settings.WorkFolder)).Cast<string>().StartWith(Settings.WorkFolder).Subscribe(UpdateWorkFolder));
        }

        private void UpdateWorkFolder(string workFolder)
        {
            WorkFolderExplorerItem.FullPath = workFolder;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            WorkFolderExplorerItem?.Dispose();
            WorkFolderExplorerItem = null;
            disposables.Clear();
        }

        private void OnItemContextFlyoutOpened(object sender, object e)
        {
            SetItemFocusState(sender, true);
        }

        private void OnItemContextFlyoutClosed(object sender, object e)
        {
            SetItemFocusState(sender, false);
        }

        private void SetItemFocusState(object menuFlyout, bool isFocus)
        {
            var container = GetContainerFromMenuFlyout(menuFlyout);
            container.BorderBrush = Resources[isFocus ? "FocusStrokeColorOuterBrush" : "NormalStrokeColorOuterBrush"] as Brush;
        }

        private muxc.TreeViewItem GetContainerFromMenuFlyout(object menuFlyout)
        {
            var item = GetExplorerItemFromMenuFlyout(menuFlyout);
            return TreeView.ContainerFromItem(item) as muxc.TreeViewItem;
        }

        private ExplorerItem GetExplorerItemFromMenuFlyout(object menuFlyout)
        {
            var flyout = menuFlyout as MenuFlyout;
            return flyout.Items[0].DataContext as ExplorerItem;
        }

        private ExplorerItem GetExplorerItemFromMenuFlyoutItem(object menuFlyoutItem)
        {
            return (menuFlyoutItem as MenuFlyoutItem).DataContext as ExplorerItem;
        }

        private void OnNewFileClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
        }

        private void OnNewFolderClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
        }

        private void OnOpenFileLocationClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
        }

        private void OnCutClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
        }

        private void OnPasteClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
        }

        private void OnCopyAsPathClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
            Clipboard.SetText(item.FullPath);
        }

        private void OnRenameClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
        }
    }

    class ExplorerItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate FolderTemplate { get; set; }
        public DataTemplate FileTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            var explorerItem = (ExplorerItem)item;
            return explorerItem.Type == ExplorerItem.ExplorerItemType.Folder ? FolderTemplate : FileTemplate;
        }
    }
}
