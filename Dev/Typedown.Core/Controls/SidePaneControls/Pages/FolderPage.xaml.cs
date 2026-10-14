using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using muxc = Microsoft.UI.Xaml.Controls;

namespace Typedown.Core.Controls.SidePanelControls.Pages
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
            Log.Debug($"FolderPage.OnLoaded: item={(WorkFolderExplorerItem != null)} vm={(FileViewModel != null)}");
            if (WorkFolderExplorerItem != null || FileViewModel == null)
                return;
            WorkFolderExplorerItem = new ExplorerItem(FileViewModel) { IsExpanded = true };
            disposables.Add(FileViewModel.WhenPropertyChanged(nameof(FileViewModel.WorkFolder)).Subscribe(_ => UpdateWorkFolder()));
            disposables.Add(FileViewModel.WhenPropertyChanged(nameof(FileViewModel.FilePath)).Subscribe(_ =>
            {
                UpdateWorkFolder();
                UpdateSelectedItem(WorkFolderExplorerItem);
                RevealActiveFile();
            }));
            // A real (non-preview) open of a file outside the current root moves the tree there; previews never
            // move it, so browsing the tree with single clicks cannot lose the folder.
            // Capture the instance: by the time Unloaded runs the DataContext (and so FileViewModel) is already null.
            var fileViewModel = FileViewModel;
            fileViewModel.FileOpened += OnFileOpened;
            disposables.Add(System.Reactive.Disposables.Disposable.Create(() => fileViewModel.FileOpened -= OnFileOpened));
            UpdateWorkFolder();
            Bindings.Update();
        }

        // The folder the tree is currently rooted at. It is sticky: switching tabs or previewing files elsewhere must
        // not yank the tree away (the user could not navigate back). It changes only when a folder is opened explicitly,
        // when there is no root yet, or via the "reveal current file" button.
        private string stickyRoot;

        private void UpdateWorkFolder()
        {
            var explicitFolder = FileViewModel.WorkFolderIsExplicit ? FileViewModel.WorkFolder : null;
            var filePath = FileViewModel.FilePath;
            string workFolder;
            if (!string.IsNullOrEmpty(explicitFolder))
                workFolder = explicitFolder;
            // The tree follows the document in front of you whenever that document is somewhere else. Deciding
            // it here rather than in a handler means it does not matter what triggered the update: a page that
            // was on the outline while the tabs were switched, or one rebuilt since, lands on the same answer.
            else if (!string.IsNullOrEmpty(filePath) && (string.IsNullOrEmpty(stickyRoot) || !IsInsideFolder(filePath, stickyRoot)))
                workFolder = Path.GetDirectoryName(filePath);
            else if (!string.IsNullOrEmpty(stickyRoot))
                workFolder = stickyRoot;
            else
                workFolder = FileViewModel.WorkFolder;
            if (!string.IsNullOrEmpty(workFolder))
                stickyRoot = workFolder;
            Log.Debug($"FolderPage.UpdateWorkFolder: WorkFolder='{FileViewModel.WorkFolder}' FilePath='{FileViewModel.FilePath}' -> '{workFolder}'");
            WorkFolderExplorerItem.FullPath = workFolder;
            WorkFolderExplorerItem.IsExpanded = true;
        }

        private void OnFileOpened(string path, bool preview)
        {
            if (preview || FileViewModel == null || FileViewModel.WorkFolderIsExplicit || string.IsNullOrEmpty(path)) return;
            UpdateWorkFolder();
            UpdateSelectedItem(WorkFolderExplorerItem);
            RevealActiveFile();
        }

        private void OnRevealCurrentFileClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(FileViewModel?.FilePath)) return;
            stickyRoot = Path.GetDirectoryName(FileViewModel.FilePath);
            UpdateWorkFolder();
            UpdateSelectedItem(WorkFolderExplorerItem);
        }

        public static Visibility RevealVisibility(string filePath, string root)
        {
            return !string.IsNullOrEmpty(filePath) && !IsInsideFolder(filePath, root ?? "") ? Visibility.Visible : Visibility.Collapsed;
        }

        private static bool IsInsideFolder(string filePath, string folder)
        {
            try
            {
                var full = Path.GetFullPath(filePath);
                var root = Path.GetFullPath(folder).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
                return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static Visibility IsVisible(string path) => string.IsNullOrEmpty(path) ? Visibility.Collapsed : Visibility.Visible;

        public static string GetStatusText(string path, int childCount, Exception exception)
        {
            if (exception != null) return exception.Message;
            if (string.IsNullOrEmpty(path)) return Locale.GetString("FolderPane.NoFolderOpen");
            if (!Directory.Exists(path)) return Locale.GetString("FolderPane.NotFound");
            return childCount == 0 ? Locale.GetString("FolderPane.NoMarkdownFiles") : "";
        }

        /// <summary>
        /// Opens the way down to the document in front of you and selects it. A document in a subfolder of the
        /// root leaves the root where it is — that folder is the workspace — but a collapsed subfolder meant
        /// nothing visibly happened on a tab switch, which read as "the tree did not follow".
        /// </summary>
        private async void RevealActiveFile()
        {
            try
            {
                var path = FileViewModel?.FilePath;
                var root = WorkFolderExplorerItem?.FullPath;
                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root) || !IsInsideFolder(path, root)) return;
                var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var relative = Path.GetFullPath(path).Substring(rootFull.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var item = WorkFolderExplorerItem;
                var current = rootFull;
                foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                {
                    if (item == null) return;
                    item.IsExpanded = true;
                    current = Path.Combine(current, segment);
                    item = await FindChild(item, current);
                }
                UpdateSelectedItem(WorkFolderExplorerItem);
            }
            catch (Exception ex)
            {
                Log.Debug($"FolderPage.RevealActiveFile: {ex.Message}");
            }
        }

        /// <summary>A folder is enumerated in the background, so the child may take a moment to appear.</summary>
        private static async System.Threading.Tasks.Task<ExplorerItem> FindChild(ExplorerItem parent, string fullPath)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var found = parent.Children.FirstOrDefault(x => string.Equals(x.FullPath, fullPath, StringComparison.OrdinalIgnoreCase));
                if (found != null) return found;
                await System.Threading.Tasks.Task.Delay(100);
            }
            return null;
        }

        private void UpdateSelectedItem(ExplorerItem item)
        {
            if (item == null || FileViewModel == null)
                return;
            item.IsSelected = FileViewModel.FilePath == item.FullPath;
            foreach (var next in item.Children)
                UpdateSelectedItem(next);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            pendingSingleClickOpen?.Cancel();
            disposables.Clear();
            WorkFolderExplorerItem?.Dispose();
            WorkFolderExplorerItem = null;
            // Keep the XAML bindings, templates and flyouts for the next load.
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
                var filename = "Untitled";
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
                var foldername = "New Folder";
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
            Common.OpenFileLocation(item?.FullPath);
        }

        // A file operation that fails (a file gone, the clipboard held by another program) says why, rather than ending
        // the app (an exception out of a click handler) or doing nothing.
        private async void Try(string what, Func<System.Threading.Tasks.Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                Log.Debug($"folder pane: {what} failed: {ex}");
                try { await AppContentDialog.Create(Locale.GetString("Error"), ex.Message, Locale.GetString("Ok")).ShowAsync(XamlRoot); } catch { }
            }
        }

        private void OnCutClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
            Try("cut", () => FileOperation.CutToClipboardAsync(new StringCollection() { item?.FullPath }));
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
            Try("copy", () => FileOperation.CopyToClipboardAsync(new StringCollection() { item?.FullPath }));
        }

        private void OnPasteClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
            Try("paste", () => FileOperation.PasteFromClipboardAsync(item?.FullPath));
        }

        private void OnCopyAsPathClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
            Try("copy as path", () => { Clipboard.SetText(item?.FullPath); return System.Threading.Tasks.Task.CompletedTask; });
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
            if (item == null || TreeView.ContainerFromItem(item) is not muxc.TreeViewItem treeViewItem)
                return;
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
            {
                Try("rename", () =>
                {
                    var newPath = Path.Combine(Path.GetDirectoryName(item.FullPath), source.Task.Result);
                    if (item.FullPath == ViewModel.FileViewModel.FilePath)
                        ViewModel.FileViewModel.RenameFile(newPath);
                    else
                        FileOperation.Rename(item.FullPath, newPath);
                    return System.Threading.Tasks.Task.CompletedTask;
                });
                _ = Dispatcher.RunIdleAsync(() => UpdateSelectedItem(WorkFolderExplorerItem));
            }
        }

        internal static void OnTreeViewItemLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not muxc.TreeViewItem item || !item.IsLoaded || VisualTreeHelper.GetChild(item, 0) is not UIElement grid || item.GetAncestor<FolderPage>() is not FolderPage page)
                return;
            var treeViewItemPointerPressedEventHandler = new PointerEventHandler(OnTreeViewItemPointerPressed);
            var treeViewItemPointerReleasedEventHandler = new PointerEventHandler(page.OnTreeViewItemPointerReleased);
            grid.AddHandler(PointerPressedEvent, treeViewItemPointerPressedEventHandler, true);
            item.AddHandler(PointerReleasedEvent, treeViewItemPointerReleasedEventHandler, true);
            var treeViewItemDoubleTappedEventHandler = new Windows.UI.Xaml.Input.DoubleTappedEventHandler(page.OnTreeViewItemDoubleTapped);
            item.AddHandler(DoubleTappedEvent, treeViewItemDoubleTappedEventHandler, true);
            if (item.DataContext is ExplorerItem explorerItem)
                page.UpdateSelectedItem(explorerItem);

            item.Unloaded += OnTreeViewItemUnloaded;
            void OnTreeViewItemUnloaded(object sender, RoutedEventArgs e)
            {
                item.Unloaded -= OnTreeViewItemUnloaded;
                var page = item.GetAncestor<FolderPage>();
                grid.RemoveHandler(PointerPressedEvent, treeViewItemPointerPressedEventHandler);
                item.RemoveHandler(PointerReleasedEvent, treeViewItemPointerReleasedEventHandler);
                item.RemoveHandler(DoubleTappedEvent, treeViewItemDoubleTappedEventHandler);
            }
        }

        private static void OnTreeViewItemPointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement).Name != "ExpandCollapseChevron" && (e.OriginalSource as FrameworkElement).GetAncestor<TextBox>() == null)
            {
                if ((sender as Grid)?.DataContext is ExplorerItem item && item.Type == ExplorerItem.ExplorerItemType.Folder)
                {
                    e.Handled = true;
                    if (e.GetCurrentPoint(sender as UIElement).Properties.IsLeftButtonPressed)
                        item.IsExpanded = !item.IsExpanded;
                }
            }
        }

        // Single click = open in the current tab, double click = open in a new tab. The single-click action is
        // deferred by the system double-click interval so a double click can cancel it (otherwise the first click
        // has already replaced the current document and the double click has nothing left to do).
        private System.Threading.CancellationTokenSource pendingSingleClickOpen;

        private async void OnTreeViewItemPointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            try
            {
                var kind = e.GetCurrentPoint(sender as UIElement).Properties.PointerUpdateKind;
                if (kind == Windows.UI.Input.PointerUpdateKind.LeftButtonReleased &&
                    (sender as muxc.TreeViewItem).DataContext is ExplorerItem item &&
                    item.Type == ExplorerItem.ExplorerItemType.File && !item.IsNotice &&
                    item.FullPath != ViewModel.FileViewModel.FilePath)
                {
                    pendingSingleClickOpen?.Cancel();
                    var cts = pendingSingleClickOpen = new System.Threading.CancellationTokenSource();
                    try
                    {
                        await Task.Delay((int)Math.Min(Math.Max(PInvoke.GetDoubleClickTime(), 150), 600), cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    if (cts.IsCancellationRequested || ViewModel?.FileViewModel == null) return; // page may have unloaded meanwhile
                    await ViewModel.FileViewModel.OpenFile(item.FullPath, preview: true);
                }
                UpdateSelectedItem(WorkFolderExplorerItem);
            }
            catch
            {
                // Ignore
            }
        }

        private async void OnTreeViewItemDoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            try
            {
                if ((sender as muxc.TreeViewItem)?.DataContext is ExplorerItem item && item.Type == ExplorerItem.ExplorerItemType.File && ViewModel?.FileViewModel != null)
                {
                    e.Handled = true;
                    pendingSingleClickOpen?.Cancel();
                    await ViewModel.FileViewModel.OpenFile(item.FullPath, preview: false);
                    UpdateSelectedItem(WorkFolderExplorerItem);
                }
            }
            catch
            {
                // Ignore
            }
        }

        private async void OnOpenClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
            if (item != null)
                await ViewModel.FileViewModel.OpenFile(item.FullPath);
            UpdateSelectedItem(WorkFolderExplorerItem);
        }

        private void OnOpenInNewWindowClick(object sender, RoutedEventArgs e)
        {
            var item = GetExplorerItemFromMenuFlyoutItem(sender);
            if (item != null)
                ViewModel.FileViewModel.NewWindowCommand.Execute(item.FullPath);
        }

        internal static async void OnItemDragStarting(UIElement sender, DragStartingEventArgs args)
        {
            try
            {
                if (sender is not muxc.TreeViewItem treeViewItem || treeViewItem.GetAncestor<FolderPage>() is not FolderPage page)
                    return;
                var item = page.GetExplorerItemFromTreeViewItem(sender);
                if (item?.Type == ExplorerItem.ExplorerItemType.File)
                {
                    var file = await StorageFile.GetFileFromPathAsync(item.FullPath);
                    args.Data.SetStorageItems(new List<IStorageItem>() { file });
                }
                if (item?.Type == ExplorerItem.ExplorerItemType.Folder)
                {
                    var folder = await StorageFolder.GetFolderFromPathAsync(item.FullPath);
                    args.Data.SetStorageItems(new List<IStorageItem>() { folder });
                }
            }
            catch
            {
                // Ignore
            }
        }

        internal static void OnItemDragOver(object sender, DragEventArgs e)
        {
            try
            {
                if (sender is not muxc.TreeViewItem treeViewItem || treeViewItem.GetAncestor<FolderPage>() is not FolderPage page)
                    return;
                var target = page.GetExplorerItemFromTreeViewItem(sender);
                if (e.DataView.Contains(StandardDataFormats.StorageItems) &&
                    target.Type == ExplorerItem.ExplorerItemType.Folder)
                {
                    e.AcceptedOperation = DataPackageOperation.Move;
                }
            }
            catch
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }

        internal static async void OnFolderItemDrop(object sender, DragEventArgs e)
        {
            if (sender is not muxc.TreeViewItem treeViewItem || treeViewItem.GetAncestor<FolderPage>() is not FolderPage page)
                return;
            try
            {
                var target = page.GetExplorerItemFromTreeViewItem(sender);
                if (e.DataView.Contains(StandardDataFormats.StorageItems) &&
                    target?.Type == ExplorerItem.ExplorerItemType.Folder)
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    e.AcceptedOperation = DataPackageOperation.Move;
                    var collection = new StringCollection();
                    items.ToList().ForEach(x => collection.Add(x.Path));
                    page.FileOperation.Move(collection, target.FullPath);
                }
            }
            catch (Exception ex)
            {
                await AppContentDialog.Create(Locale.GetString("Error"), ex.Message, Locale.GetDialogString("Ok")).ShowAsync(page.XamlRoot);
            }
        }

        private void OnTreeViewContextFlyoutOpening(object sender, object e)
        {
            if (!Directory.Exists(WorkFolderExplorerItem?.FullPath) && sender is MenuFlyout flyout)
                flyout.Hide();
        }
    }

    class ExplorerItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate FolderTemplate { get; set; }
        public DataTemplate FileTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            return item is ExplorerItem explorerItem && explorerItem.Type == ExplorerItem.ExplorerItemType.Folder ? FolderTemplate : FileTemplate;
        }
    }
}
