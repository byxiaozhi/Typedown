using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Models;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using muxc = Microsoft.UI.Xaml.Controls;

namespace Typedown.Universal.Controls.SidePanelControls.Pages
{
    public sealed partial class FolderPage : Page, INotifyPropertyChanged
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public FileViewModel FileViewModel => ViewModel?.FileViewModel;

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
            disposables.Add(FileViewModel.WhenPropertyChanged(nameof(FileViewModel.WorkFolder)).Cast<string>().StartWith(FileViewModel.WorkFolder).Subscribe(UpdateWorkFolder));
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
            if (container != null)
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

        private ExplorerItem GetExplorerItemFromTreeViewItem(object menuFlyoutItem)
        {
            return (menuFlyoutItem as muxc.TreeViewItem).DataContext as ExplorerItem;
        }

        private async void OnNewFileClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
            try
            {
                var filename = Locale.GetString("UntitledDocument");
                var extension = ".md";
                var fullname = filename + extension;
                if (FileOperation.IsFilenameValid(item.FullPath, fullname))
                {
                    File.Create(Path.Combine(item.FullPath, fullname)).Close();
                }
                else
                {
                    var flag = true;
                    for (var i = 2; i < 10000; i++)
                    {
                        fullname = $"{filename} ({i}){extension}";
                        if (FileOperation.IsFilenameValid(item.FullPath, fullname))
                        {
                            File.Create(Path.Combine(item.FullPath, fullname)).Close();
                            flag = false;
                            break;
                        }
                    }
                    if (flag)
                    {
                        return;
                    }
                }
                await Task.Yield();
                item.IsExpanded = true;
                await Task.Delay(100);
                RenameFile(item.Children.Where(x => Path.GetFileName(x.FullPath) == fullname).FirstOrDefault());
            }
            catch (Exception ex)
            {
                await AppContentDialog.Create(Locale.GetDialogString("CreateDocErrorTitle"), ex.Message, Locale.GetDialogString("Ok")).ShowAsync(XamlRoot);
            }
        }

        private async void OnNewFolderClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
            try
            {
                var foldername = Locale.GetString("UntitledFolder");
                var fullname = foldername;
                if (FileOperation.IsFilenameValid(item.FullPath, fullname))
                {
                    Directory.CreateDirectory(Path.Combine(item.FullPath, fullname));
                }
                else
                {
                    var flag = true;
                    for (var i = 2; i < 10000; i++)
                    {
                        fullname = $"{foldername} ({i})";
                        if (FileOperation.IsFilenameValid(item.FullPath, fullname))
                        {
                            try
                            {
                                Directory.CreateDirectory(Path.Combine(item.FullPath, fullname));
                            }
                            catch
                            {
                                return;
                            }
                            flag = false;
                            break;
                        }
                    }
                    if (flag)
                    {
                        return;
                    }
                }
                await Task.Yield();
                item.IsExpanded = true;
                await Task.Delay(100);
                RenameFile(item.Children.Where(x => Path.GetFileName(x.FullPath) == fullname).FirstOrDefault());
            }
            catch (Exception ex)
            {
                await AppContentDialog.Create(Locale.GetDialogString("CreateFolderErrorTitle"), ex.Message, Locale.GetDialogString("Ok")).ShowAsync(ViewModel.XamlRoot);
            }
        }

        private void OnOpenFileLocationClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
            string argument = "/select, \"" + item?.FullPath + "\"";
            Process.Start("explorer.exe", argument);
        }

        private void OnCutClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
            FileOperation.CutToClipboard(new StringCollection() { item?.FullPath });
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
            FileOperation.CopyToClipboard(new StringCollection() { item?.FullPath });
        }

        private void OnPasteClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
            FileOperation.PasteFromClipboard(item?.FullPath);
        }

        private void OnCopyAsPathClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
            Clipboard.SetText(item?.FullPath);
        }

        private void OnRenameClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
            RenameFile(item);
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
            FileOperation.Delete(new StringCollection() { item?.FullPath });
        }

        private async void RenameFile(ExplorerItem item)
        {
            if (item == null) return;
            var treeViewItem = TreeView.ContainerFromItem(item) as muxc.TreeViewItem;
            if (treeViewItem == null) return;
            var container = treeViewItem.FindName("TextBoxContainer") as ContentPresenter;
            var textblock = treeViewItem.FindName("NameTextBlock") as TextBlock;
            var textBox = new TextBox()
            {
                Style = Resources["RenameTextBoxStyle"] as Style,
                Text = Path.GetFileName(item.FullPath),
            };
            var source = new TaskCompletionSource<string>();
            container.Content = textBox;
            textBox.Loaded += (s, e) =>
            {
                textblock.Visibility = Visibility.Collapsed;
                textBox.Focus(FocusState.Programmatic);
                if (item.Type == ExplorerItem.ExplorerItemType.Folder || !textBox.Text.Contains(".")) textBox.SelectAll();
                else textBox.Select(0, textBox.Text.LastIndexOf("."));
            };
            textBox.LostFocus += (s, e) =>
            {
                if (!source.Task.IsCompleted) source.SetResult(textBox.Text);
                container.Content = null;
            };
            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    if (!source.Task.IsCompleted) source.SetResult(textBox.Text);
                    container.Content = null;
                }
            };
            await source.Task;
            textblock.Visibility = Visibility.Visible;
            if (source.Task.Result != Path.GetFileName(item.FullPath))
                FileOperation.Rename(item.FullPath, Path.Combine(Path.GetDirectoryName(item.FullPath), source.Task.Result));
        }

        private readonly Windows.UI.Xaml.Input.PointerEventHandler TreeViewItemPointerPressedEventHandler = new(OnTreeViewItemPointerPressed);

        private void OnTreeViewItemLoaded(object sender, RoutedEventArgs e)
        {
            var grid = VisualTreeHelper.GetChild(sender as DependencyObject, 0) as UIElement;
            grid.AddHandler(PointerPressedEvent, TreeViewItemPointerPressedEventHandler, true);
        }

        private void OnTreeViewItemUnloaded(object sender, RoutedEventArgs e)
        {
            var grid = VisualTreeHelper.GetChild(sender as DependencyObject, 0) as UIElement;
            grid.RemoveHandler(PointerPressedEvent, TreeViewItemPointerPressedEventHandler);
        }

        private static void OnTreeViewItemPointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement).Name != "ExpandCollapseChevron" && (e.OriginalSource as FrameworkElement).GetAncestor<TextBox>() == null)
            {
                var item = (sender as Grid).DataContext as ExplorerItem;
                if (item.Type == ExplorerItem.ExplorerItemType.Folder)
                {
                    e.Handled = true;
                    if (e.GetCurrentPoint(sender as Grid).Properties.IsLeftButtonPressed)
                        item.IsExpanded = !item.IsExpanded;
                }
            }
        }

        private void OnOpenClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
            ViewModel.FileViewModel.OpenFileCommand.Execute(item.FullPath);
        }

        private void OnOpenInNewWindowClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
            ViewModel.FileViewModel.NewWindowCommand.Execute(item.FullPath);
        }

        private async void OnItemDragStarting(UIElement sender, DragStartingEventArgs args)
        {
            var item = GetExplorerItemFromTreeViewItem(sender);
            if (item.Type == ExplorerItem.ExplorerItemType.File)
            {
                var file = await StorageFile.GetFileFromPathAsync(item.FullPath);
                args.Data.SetStorageItems(new List<IStorageItem>() { file });
            }
            if (item.Type == ExplorerItem.ExplorerItemType.Folder)
            {
                var folder = await StorageFolder.GetFolderFromPathAsync(item.FullPath);
                args.Data.SetStorageItems(new List<IStorageItem>() { folder });
            }
        }

        private void OnItemDragOver(object sender, DragEventArgs e)
        {
            var target = GetExplorerItemFromTreeViewItem(sender);
            if (e.DataView.Contains(StandardDataFormats.StorageItems) &&
                target.Type == ExplorerItem.ExplorerItemType.Folder)
            {
                e.AcceptedOperation = DataPackageOperation.Move;
            }
        }

        private async void OnFolderItemDrop(object sender, DragEventArgs e)
        {
            var target = GetExplorerItemFromTreeViewItem(sender);
            if (e.DataView.Contains(StandardDataFormats.StorageItems) &&
                target.Type == ExplorerItem.ExplorerItemType.Folder)
            {
                var items = await e.DataView.GetStorageItemsAsync();
                e.AcceptedOperation = DataPackageOperation.Move;
                var collection = new StringCollection();
                items.ToList().ForEach(x => collection.Add(x.Path));
                FileOperation.Move(collection, target.FullPath);
            }
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
