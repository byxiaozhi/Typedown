using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Typedown.WinUI.Models;
using Typedown.WinUI.Utilities;
using Typedown.WinUI.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using WinRT.Interop;

namespace Typedown.WinUI
{
    // New since the fork: several documents per window, as tabs in the title bar. The window keeps one
    // editor (one WebView2) and swaps the document it shows: each DocumentTab keeps its own file state,
    // undo history and cursor, so switching brings a document back as it was left and ten open files
    // don't cost ten editors. `file` and `history` in the rest of MainWindow mean the document on screen.
    //
    // With tabs off (Settings), a window holds one document as before; Close document still works and
    // leaves the window on the start page.
    public sealed partial class MainWindow
    {
        private sealed class DocumentTab
        {
            public DocumentTab(FileViewModel file) => File = file;

            public FileViewModel File { get; }

            public ContentHistory History { get; } = new();

            // The undo history starts from the text the editor first shows.
            public bool HistoryReady { get; set; }

            public CursorState Cursor { get; set; }

            // A tab restored from the last session that hasn't been shown yet: its file is only read
            // when it's first opened, so a long list of tabs doesn't slow startup.
            public string PendingPath { get; set; }

            // On its way to another window (or arriving from one): not moved twice, not auto-saved meanwhile.
            public bool InTransfer { get; set; }

            public TabViewItem Item { get; set; }

            public TextBlock HeaderText { get; set; }

            public Ellipse DirtyDot { get; set; }

            public FontIcon FavoriteStar { get; set; }

            public Border ColorBar { get; set; }

            public string Path => PendingPath ?? File.FilePath;

            // Untitled documents are numbered in the window ("Untitled", "Untitled 2"), so two of them
            // can be told apart in the tabs and the prompts.
            public int UntitledNumber { get; set; }

            public string DisplayName => PendingPath != null ? System.IO.Path.GetFileName(PendingPath)
                : !string.IsNullOrEmpty(File.FilePath) || UntitledNumber <= 1 ? File.DisplayName
                : $"{File.DisplayName} {UntitledNumber}";

            public bool IsDirty => PendingPath == null && File.IsDirty;

            // Blank, untitled and untouched: a document opened now can take its place.
            public bool IsBlank => PendingPath == null && string.IsNullOrEmpty(File.FilePath) && !File.IsDirty && string.IsNullOrWhiteSpace(File.Markdown);

            public bool IsEmailThread => PendingPath == null && File.Markdown.StartsWith("---") &&
                File.Markdown.IndexOf("type: \"email-thread\"", 0, Math.Min(200, File.Markdown.Length), StringComparison.Ordinal) >= 0;
        }

        private readonly List<DocumentTab> documents = new();
        private DocumentTab activeDoc;
        private bool startPageShown;
        private bool selectingTab;

        // Switches run one at a time: a second Ctrl+Tab waits for the first to finish.
        private readonly System.Threading.SemaphoreSlim switchLock = new(1, 1);

        // When the editor last reported a change, and whether it has finished starting up.
        private DateTime lastEditorChange;
        private bool editorReady;

        private FileViewModel file => activeDoc.File;

        private ContentHistory history => activeDoc.History;

        private bool TabsEnabled => settings.UseTabs;

        private static MainWindow lastActiveWindow;

        private DocumentTab CreateDocument(string untitledKey = null)
        {
            var doc = new DocumentTab(new FileViewModel(settings, eventCenter, this, untitledKey) { IsActive = false });
            doc.File.FileStateChanged += () =>
            {
                UpdateTabHeader(doc);
                if (doc != activeDoc) return;
                UpdateTitle();
                UpdateFolderSelection();
            };
            doc.History.Changed += () => { if (doc == activeDoc) UpdateUndoRedoItems(); };
            return doc;
        }

        // The window's first document, before anything is loaded (constructor).
        private void SetUpDocuments(string untitledKey = null)
        {
            activeDoc = CreateDocument(untitledKey);
            activeDoc.File.IsActive = true;
            AttachTab(activeDoc);
            ApplyTabsVisibility();
            Activated += (s, e) => { if (e.WindowActivationState != WindowActivationState.Deactivated) lastActiveWindow = this; };
            Closed += (s, e) => { if (lastActiveWindow == this) lastActiveWindow = null; };
        }

        // --- Tab strip ---

        private void AttachTab(DocumentTab doc, int index = -1)
        {
            if (documents.Contains(doc)) return;
            if (index < 0 || index > documents.Count) index = documents.Count;
            if (doc.UntitledNumber == 0 && string.IsNullOrEmpty(doc.Path))
            {
                var used = documents.Where(d => string.IsNullOrEmpty(d.Path)).Select(d => d.UntitledNumber).ToHashSet();
                doc.UntitledNumber = Enumerable.Range(1, documents.Count + 1).First(n => !used.Contains(n));
            }
            documents.Insert(index, doc);
            var text = new TextBlock { MaxWidth = 180, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            var dot = new Ellipse { Width = 7, Height = 7, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Fill = (Brush)Application.Current.Resources["CaretPrimaryBrush"], Visibility = Visibility.Collapsed };
            var star = new FontIcon { Glyph = "\uE735", FontSize = 10, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"], Visibility = Visibility.Collapsed };
            AutomationProperties.SetName(star, Locale.GetString("Favorites"));
            // The tab's colour (right-click > Tab color): a short bar before the name, like Edge's tab groups.
            var bar = new Border { Width = 3, Height = 14, CornerRadius = new CornerRadius(1.5), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(bar);
            header.Children.Add(text);
            header.Children.Add(star);
            header.Children.Add(dot);
            doc.HeaderText = text;
            doc.DirtyDot = dot;
            doc.FavoriteStar = star;
            doc.ColorBar = bar;
            doc.Item = new TabViewItem { Header = header, Tag = doc, ContextFlyout = BuildTabMenu(doc) };
            BeginTabChange();
            DocumentTabView.TabItems.Insert(index, doc.Item);
            UpdateTabHeader(doc);
            ApplyTabsVisibility();
        }

        private void DetachTab(DocumentTab doc)
        {
            if (!documents.Remove(doc)) return;
            BeginTabChange();
            DocumentTabView.TabItems.Remove(doc.Item);
            ApplyTabsVisibility();
        }

        // The strip changes selection by itself when tabs come and go, sometimes a moment later; until
        // the queue has caught up, selections aren't the user's, and the tab on screen is re-selected.
        private void BeginTabChange()
        {
            selectingTab = true;
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                selectingTab = false;
                if (activeDoc?.Item != null && !ReferenceEquals(DocumentTabView.SelectedItem, activeDoc.Item)) SelectTab(activeDoc);
            });
        }

        private void UpdateTabHeader(DocumentTab doc)
        {
            if (doc.Item == null) return;
            doc.HeaderText.Text = doc.DisplayName;
            AutomationProperties.SetName(doc.Item, doc.DisplayName);
            doc.HeaderText.FontWeight = doc == activeDoc && !startPageShown ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
            doc.DirtyDot.Visibility = doc.IsDirty ? Visibility.Visible : Visibility.Collapsed;
            doc.FavoriteStar.Visibility = favoritesService.Contains(doc.Path) ? Visibility.Visible : Visibility.Collapsed;
            var color = TabColorOf(doc);
            doc.ColorBar.Visibility = color == null ? Visibility.Collapsed : Visibility.Visible;
            if (color != null) doc.ColorBar.Background = new SolidColorBrush(ColorSchemes.Parse(color));
            doc.Item.IconSource = new FontIconSource { Glyph = doc.IsEmailThread ? "" : "", FontSize = 14 };
            ToolTipService.SetToolTip(doc.Item, doc.Path ?? Locale.GetString("NotSavedYet"));
        }

        // With tabs off the strip is hidden; documents already open in tabs stay visible until
        // they're closed, rather than disappearing behind the setting.
        private void ApplyTabsVisibility()
        {
            var show = TabsEnabled || documents.Count > 1;
            DocumentTabView.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            TitleTextBlock.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            UpdateTabStripInputRegion();
        }

        private MenuFlyout BuildTabMenu(DocumentTab doc)
        {
            var menu = new MenuFlyout();
            MenuFlyoutItem Item(string key, Action action)
            {
                var item = new MenuFlyoutItem { Text = Locale.GetString(key) };
                item.Click += (s, e) => action();
                menu.Items.Add(item);
                return item;
            }
            Item("CloseDocumentMenuItem", () => _ = CloseDocument(doc));
            Item("CloseOtherDocuments", () => _ = CloseDocuments(documents.Where(d => d != doc).ToList()));
            Item("CloseDocumentsToTheRight", () => _ = CloseDocuments(documents.Skip(documents.IndexOf(doc) + 1).ToList()));
            menu.Items.Add(new MenuFlyoutSeparator());
            Item("MoveToNewWindow", () => _ = MoveToNewWindow(doc));
            var moveToWindow = new MenuFlyoutSubItem { Text = Locale.GetString("MoveToWindow") };
            menu.Items.Add(moveToWindow);
            menu.Items.Add(new MenuFlyoutSeparator());
            var favorite = new ToggleMenuFlyoutItem { Text = Locale.GetString("FavoriteMenuItem") };
            favorite.Click += (s, e) => ToggleFavorite(doc.Path);
            menu.Items.Add(favorite);
            var colorMenu = BuildTabColorMenu(doc);
            menu.Items.Add(colorMenu);
            var copyPath = Item("CopyAsPath", () =>
            {
                var package = new DataPackage();
                package.SetText(doc.Path);
                Clipboard.SetContent(package);
            });
            var reveal = Item("RevealInFileExplorer", () => System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{doc.Path}\""));
            menu.Opening += (s, e) =>
            {
                // The other windows that take tabs, by what they show: Move to window > name.
                moveToWindow.Items.Clear();
                foreach (var other in openWindows.Where(w => w != this && w.TabsEnabled && w.editorReady))
                {
                    var item = new MenuFlyoutItem { Text = other.WindowLabel };
                    item.Click += (s2, e2) => _ = other.AdoptDocument(this, doc);
                    moveToWindow.Items.Add(item);
                }
                moveToWindow.Visibility = moveToWindow.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                copyPath.IsEnabled = reveal.IsEnabled = favorite.IsEnabled = colorMenu.IsEnabled = !string.IsNullOrEmpty(doc.Path);
                var current = TabColorOf(doc);
                foreach (var item in colorMenu.Items.OfType<RadioMenuFlyoutItem>()) item.IsChecked = (string)item.Tag == current;
                favorite.IsChecked = favoritesService.Contains(doc.Path);
            };
            return menu;
        }

        private async void DocumentTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (selectingTab) return;
            if (!editorReady)
            {
                SelectTab(activeDoc); // nothing switches before the editor is up
                return;
            }
            if (DocumentTabView.SelectedItem is TabViewItem { Tag: DocumentTab doc } && doc != activeDoc)
                await ActivateDocument(doc);
        }

        private async void DocumentTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            if (args.Tab.Tag is DocumentTab doc) await CloseDocument(doc);
        }

        private void DocumentTabView_AddTabButtonClick(TabView sender, object args) => NewMenuItem_Click(this, null);

        // The strip sits in the title bar, which Windows treats as the window's handle: pressing on a tab
        // and moving would drag the window, not the tab. Telling Windows the strip isn't part of the
        // handle lets tabs be dragged, to reorder them or to tear one out. Clicks worked without this.
        private void DocumentTabView_Loaded(object sender, RoutedEventArgs e)
        {
            DocumentTabView.XamlRoot.Changed += (root, args) => UpdateTabStripInputRegion();
            UpdateTabStripInputRegion();
            // Even when a tab handles the press itself (selecting it, starting a drag)
            DocumentTabView.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(DocumentTabView_PointerPressed), true);
        }

        // The tab under the pointer when the button went down. The drag events don't say which tab is being
        // dragged (their Tab is always the first one), so a tear-out relies on this and does nothing without it.
        private TabViewItem pressedTab;

        private void DocumentTabView_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            pressedTab = null;
            for (var element = e.OriginalSource as DependencyObject; element != null; element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element))
            {
                if (element is TabViewItem item) { pressedTab = item; break; }
            }
        }

        private void DocumentTabView_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateTabStripInputRegion();

        private void UpdateTabStripInputRegion()
        {
            try
            {
                var source = Microsoft.UI.Input.InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
                // Even a lone tab can be dragged: onto another window's strip, or out to move this window
                // (DocumentTabView_TabDroppedOutside). The empty title bar beside it still drags the window.
                if (DocumentTabView.Visibility != Visibility.Visible || DocumentTabView.XamlRoot == null || DocumentTabView.ActualWidth == 0)
                {
                    source.ClearRegionRects(Microsoft.UI.Input.NonClientRegionKind.Passthrough);
                    return;
                }
                var scale = DocumentTabView.XamlRoot.RasterizationScale;
                var bounds = DocumentTabView.TransformToVisual(null).TransformBounds(new Windows.Foundation.Rect(0, 0, DocumentTabView.ActualWidth, DocumentTabView.ActualHeight));
                source.SetRegionRects(Microsoft.UI.Input.NonClientRegionKind.Passthrough, new[]
                {
                    new Windows.Graphics.RectInt32((int)(bounds.X * scale), (int)(bounds.Y * scale), (int)Math.Ceiling(bounds.Width * scale), (int)Math.Ceiling(bounds.Height * scale)),
                });
            }
            catch (Exception ex)
            {
                Log($"Tabs: couldn't update the tab strip's input region: {ex.Message}");
            }
        }

        // A tab dragged out of the strip and dropped outside any tab strip becomes a window of its own,
        // opened where it was dropped. With a single document there is nothing to tear it out of: the
        // window itself goes where the tab was dropped, as if it had been dragged by its title bar.
        private async void DocumentTabView_TabDroppedOutside(TabView sender, TabViewTabDroppedOutsideEventArgs args)
        {
            var doc = pressedTab?.Tag as DocumentTab;
            pressedTab = null;
            if (doc == null || !documents.Contains(doc)) return;
            if (!GetCursorPos(out var pointer)) return;
            var at = new Windows.Graphics.PointInt32(pointer.X, pointer.Y);
            if (documents.Count <= 1) PlaceUnderPointer(at);
            else await MoveToNewWindow(doc, at);
        }

        // --- Tabs between windows ---

        // What a dragged tab carries: its item, whose Tag is the document. The window that has it is found
        // among the open ones (all in this process), so the drop needs nothing else.
        private const string TabDragKey = "Caret.Tab";

        // Set while a tab of this window is being dragged, completed when the drag is over: the window
        // that takes the tab waits for that before this one lets go of it.
        private TaskCompletionSource tabDragEnded;

        private void DocumentTabView_TabDragStarting(TabView sender, TabViewTabDragStartingEventArgs args)
        {
            tabDragEnded = new TaskCompletionSource();
            if (pressedTab == null || pressedTab.Tag is not DocumentTab) return;
            args.Data.Properties[TabDragKey] = pressedTab;
            args.Data.RequestedOperation = DataPackageOperation.Move;
        }

        private void DocumentTabView_TabDragCompleted(TabView sender, TabViewTabDragCompletedEventArgs args) => tabDragEnded?.TrySetResult();

        private async Task WaitForTabDragToEnd()
        {
            var ended = tabDragEnded;
            if (ended != null) await Task.WhenAny(ended.Task, Task.Delay(2000));
        }

        // A tab of another window over this strip: it can be dropped here (a tab of this window is
        // reordered by the strip itself).
        private void DocumentTabView_TabStripDragOver(object sender, DragEventArgs e)
        {
            if (DraggedDocument(e, out _) != null)
                e.AcceptedOperation = DataPackageOperation.Move;
        }

        private void DocumentTabView_TabStripDrop(object sender, DragEventArgs e)
        {
            var doc = DraggedDocument(e, out var from);
            if (doc == null) return;
            e.Handled = true;
            // Before the first tab whose middle the pointer hasn't passed.
            var index = DocumentTabView.TabItems.Count;
            for (var i = 0; i < DocumentTabView.TabItems.Count; i++)
            {
                if (DocumentTabView.ContainerFromIndex(i) is TabViewItem tab && e.GetPosition(tab).X < tab.ActualWidth / 2)
                {
                    index = i;
                    break;
                }
            }
            // After the drop has returned, so the strip the tab came from isn't changed under its own drag.
            DispatcherQueue.TryEnqueue(async () => await AdoptDocument(from, doc, index));
        }

        // The document in a drag from another window's tab strip, and that window.
        private DocumentTab DraggedDocument(DragEventArgs e, out MainWindow from)
        {
            from = null;
            if (!e.DataView.Properties.TryGetValue(TabDragKey, out var value) || value is not TabViewItem { Tag: DocumentTab doc }) return null;
            from = openWindows.FirstOrDefault(w => w != this && w.documents.Contains(doc));
            return from == null ? null : doc;
        }

        // What this window is called in another one's "Move to window" list: the document in front,
        // and how many others it holds.
        private string WindowLabel => (startPageShown || activeDoc == null ? "Caret" : activeDoc.DisplayName) + (documents.Count > 1 ? $" (+{documents.Count - 1})" : "");

        // A document from another window, opened here as a tab at `index`: it arrives with its unsaved
        // text and its recovery-backup slot, as in Move to new window, and the other window lets go of
        // it once this one has it. The other window closes if that was its last document.
        private async Task AdoptDocument(MainWindow from, DocumentTab doc, int index = -1)
        {
            if (from == this || !TabsEnabled || !editorReady) return;
            // Its strip settles first: nothing changes under a drag that is still finishing.
            await from.WaitForTabDragToEnd();
            var transfer = await from.PrepareTransfer(doc); // also marks the document as on its way
            if (transfer == null) return;
            var previous = activeDoc;
            var hadStartPage = startPageShown;
            DocumentTab adopted = null;
            // Letting the other window go of the document is the point of no return: before it, a failure
            // takes this window back; after it, the document lives here and must stay here.
            var released = false;
            try
            {
                if (transfer.Path != null && FindDocument(transfer.Path) != null) return; // never one file twice
                var sent = doc.File.Markdown;
                HideStartPage();
                adopted = CreateDocument(transfer.UntitledKey);
                adopted.InTransfer = true;
                AttachTab(adopted, index);
                await ActivateDocument(adopted, show: false); // loaded next, as in a new tab
                if (transfer.Path != null) await adopted.File.OpenFile(transfer.Path);
                else adopted.File.NewFile();
                if (transfer.UnsavedText != null) adopted.File.ApplyRecoveredBackup(transfer.UnsavedText);
                // The other window's editor stays live while this one loads: what was typed there in the
                // meantime comes along, so the release below can't drop it.
                if (doc == from.activeDoc && from.editorReady)
                {
                    await from.WaitForEditorQuiet();
                    await from.FlushEditor();
                }
                if (doc.PendingPath == null && doc.File.Markdown != sent) adopted.File.ApplyRecoveredBackup(doc.File.Markdown);
                Activate();
                Log($"Tabs: took {adopted.DisplayName} from another window");
                released = true;
                await from.ReleaseMovedDocument(doc, closeWhenEmpty: true);
            }
            catch (Exception ex)
            {
                if (released)
                {
                    // Only what the other window does next (showing its next tab) failed.
                    Log($"Tabs: the other window couldn't settle after giving up a document: {ex}");
                }
                else
                {
                    // The document stays where it was; this window goes back to what it showed.
                    Log($"Tabs: couldn't take a document from another window: {ex}");
                    await RollBackAdoption(adopted, previous, hadStartPage);
                }
            }
            finally
            {
                doc.InTransfer = false;
                if (adopted != null) adopted.InTransfer = false;
            }
        }

        private async Task RollBackAdoption(DocumentTab adopted, DocumentTab previous, bool hadStartPage)
        {
            if (adopted != null)
            {
                DetachTab(adopted);
                adopted.File.IsActive = false;
            }
            if (previous == null) return;
            if (documents.Contains(previous)) await ActivateDocument(previous);
            else
            {
                activeDoc = previous;
                previous.File.IsActive = true;
            }
            if (hadStartPage) ShowStartPage();
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct CursorPoint
        {
            public int X, Y;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out CursorPoint point);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left, Top, Right, Bottom;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect Monitor, Work;
            public uint Flags;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(CursorPoint point, uint flags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        // Reordering by drag: keep `documents` in the order the strip shows.
        private void DocumentTabView_TabItemsChanged(TabView sender, Windows.Foundation.Collections.IVectorChangedEventArgs args)
        {
            var order = DocumentTabView.TabItems.OfType<TabViewItem>().Select(i => i.Tag).OfType<DocumentTab>().ToList();
            if (order.Count == documents.Count) { documents.Clear(); documents.AddRange(order); }
        }

        // --- Switching ---

        private async Task ActivateDocument(DocumentTab doc, bool show = true)
        {
            await switchLock.WaitAsync();
            try
            {
                await ActivateDocumentCore(doc, show);
            }
            finally
            {
                switchLock.Release();
            }
        }

        private async Task ActivateDocumentCore(DocumentTab doc, bool show)
        {
            if (show) HideSettingsPage();
            var previous = activeDoc;
            if (previous != null && previous != doc && editorReady)
            {
                await WaitForEditorQuiet();
                await FlushEditor();
            }
            if (previous != null && previous != doc) previous.File.IsActive = false;
            activeDoc = doc;
            doc.File.IsActive = true;
            HideStartPage();
            SelectTab(doc);
            if (doc.PendingPath != null)
            {
                // First look at a restored tab: load it now, the same way as opening it.
                var path = doc.PendingPath;
                doc.PendingPath = null;
                if (File.Exists(path))
                {
                    await doc.File.OpenFile(path);
                    await OfferBackupRecoveryIfAny(path);
                }
                else
                {
                    Log($"Tabs: restored {path} no longer exists");
                    doc.File.NewFile();
                }
            }
            else if (previous != doc && show)
            {
                if (!doc.HistoryReady)
                {
                    doc.History.InitHistory(doc.File.Markdown);
                    doc.HistoryReady = true;
                }
                // Not LoadFile: that resets the cursor and the undo history. SetMarkdown is what undo
                // uses to put a text back with its cursor; the echo it may cause isn't an edit.
                historyUpdating = true;
                doc.File.ExpectEcho(previous?.File.Markdown);
                PostMessage("SetMarkdown", new { text = doc.File.Markdown, cursor = doc.Cursor, basePath = doc.File.ImageBasePath });
            }
            UpdateTitle();
            UpdateFolderSelection();
            UpdateUndoRedoItems();
            UpdateTabHeader(doc);
            if (previous != null && previous != doc) UpdateTabHeader(previous);
        }

        private void SelectTab(DocumentTab doc)
        {
            if (doc.Item == null || ReferenceEquals(DocumentTabView.SelectedItem, doc.Item)) return;
            var wasSelecting = selectingTab;
            selectingTab = true;
            DocumentTabView.SelectedItem = doc.Item;
            selectingTab = wasSelecting;
        }

        // The editor reports a change a moment after the keystroke. Before the document on screen
        // changes, let those reports arrive, so the last words typed land in the document they were
        // typed in rather than in the next one.
        private async Task WaitForEditorQuiet()
        {
            var waited = 0;
            while ((DateTime.UtcNow - lastEditorChange).TotalMilliseconds < 300 && waited < 1500)
            {
                await Task.Delay(50);
                waited += 50;
            }
        }

        // Makes sure the tab on screen has every edit the editor has made, before it stops listening:
        // the editor answers "Flush" with its latest text, after any MarkdownChange still on its way.
        // A text the host hasn't seen yet is applied as the MarkdownChange it would have been (to
        // the document and its undo history). An editor that doesn't answer in time (an older
        // bundle) leaves the quiet wait above as the only guard.
        private int flushCount;

        private async Task FlushEditor()
        {
            var id = $"flush-{++flushCount}";
            var answer = new TaskCompletionSource<string>();
            using var subscription = eventCenter.GetObservable<EditorEventArgs>("Flushed").Subscribe(x =>
            {
                if (x.Args?["id"]?.ToString() == id) answer.TrySetResult(x.Args["text"]?.ToString());
            });
            PostMessage("Flush", new { id });
            if (await Task.WhenAny(answer.Task, Task.Delay(1000)) != answer.Task)
            {
                Log("Tabs: the editor didn't answer Flush");
                return;
            }
            var text = answer.Task.Result;
            if (text != null && text != activeDoc.File.Markdown)
            {
                Log("Tabs: applied an edit that was still on its way");
                eventCenter.EmitEvent("MarkdownChange", new EditorEventArgs("MarkdownChange", JToken.FromObject(new { text })));
            }
        }

        private void UpdateUndoRedoItems()
        {
            UndoMenuItem.IsEnabled = history.Undoable;
            RedoMenuItem.IsEnabled = history.Redoable;
        }

        private async Task SwitchTabRelative(int delta)
        {
            if (documents.Count < 2 || startPageShown) return;
            var index = (documents.IndexOf(activeDoc) + delta + documents.Count) % documents.Count;
            await ActivateDocument(documents[index]);
        }

        // --- Opening ---

        // Where the next document goes: the blank document on screen if there is one, otherwise a new
        // tab — or, with tabs off, this window's document once any unsaved changes are dealt with.
        // False when the user cancelled.
        private async Task<bool> MakeRoomForDocument()
        {
            if (startPageShown)
            {
                if (!documents.Contains(activeDoc)) AttachTab(activeDoc);
                HideStartPage();
                SelectTab(activeDoc);
                return true;
            }
            if (!TabsEnabled) return await ConfirmDiscardChangesIfNeeded();
            if (activeDoc.IsBlank) return true;
            var doc = CreateDocument();
            AttachTab(doc, documents.IndexOf(activeDoc) + 1);
            await ActivateDocument(doc, show: false); // the caller loads it next
            return true;
        }

        // Every "open this file" in the app ends here: a file that's already open comes to the front
        // (its tab here, or the window that has it), anything else opens in a new tab.
        private async Task<bool> OpenDocument(string path, string source)
        {
            if (FocusIfOpenElsewhere(path)) return true;
            if (!await MakeRoomForDocument()) return false;
            await file.OpenFile(path);
            await OfferBackupRecoveryIfAny(path);
            recentFiles.Record(path);
            RefreshRecentFilesMenu();
            UpdateTitle();
            Log($"{source}: {path}");
            return true;
        }

        // A file or folder renamed in the folder tree: every open document at or under it, in every
        // window and restored-but-unopened tabs too, follows it; otherwise the next save would write
        // to the old path (a duplicate) and a restored tab would open blank.
        private static void FollowRename(string oldPath, string newPath, bool isFolder)
        {
            var folder = oldPath.TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar;
            foreach (var window in openWindows.ToList())
            {
                foreach (var doc in window.documents.ToList())
                {
                    var path = doc.Path;
                    string moved = null;
                    if (string.Equals(path, oldPath, StringComparison.OrdinalIgnoreCase)) moved = newPath;
                    else if (isFolder && path != null && path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                        moved = System.IO.Path.Combine(newPath, path.Substring(folder.Length));
                    if (moved == null) continue;
                    AutoBackup.MoveBackup(path, moved);
                    if (doc.PendingPath != null) doc.PendingPath = moved;
                    else doc.File.RenamePathOnly(moved);
                    window.UpdateTabHeader(doc);
                    if (doc == window.activeDoc) window.UpdateTitle();
                }
            }
        }

        private DocumentTab FindDocument(string path) =>
            string.IsNullOrEmpty(path) ? null : documents.FirstOrDefault(d => string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase));

        // --- Closing ---

        private async void CloseDocumentMenuItem_Click(object sender, RoutedEventArgs e) => await CloseDocument(activeDoc);

        private async void CloseOtherDocumentsMenuItem_Click(object sender, RoutedEventArgs e) =>
            await CloseDocuments(documents.Where(d => d != activeDoc).ToList());

        private async void MoveToNewWindowMenuItem_Click(object sender, RoutedEventArgs e) => await MoveToNewWindow(activeDoc);

        private async Task<bool> CloseDocuments(List<DocumentTab> docs)
        {
            foreach (var doc in docs)
                if (!await CloseDocument(doc)) return false;
            return true;
        }

        private async Task<bool> CloseDocument(DocumentTab doc)
        {
            if (doc == null || (startPageShown && doc == activeDoc)) return true;
            var original = activeDoc;
            if (doc.IsDirty)
            {
                await ActivateDocument(doc);
                if (!await ConfirmDiscardChangesIfNeeded()) return false;
                doc.File.CompleteDiscard(); // "Don't Save": its recovery backup goes with it
            }
            var wasActive = doc == activeDoc;
            var index = documents.IndexOf(doc);
            if (!TabsEnabled && documents.Count <= 1)
            {
                // One document per window: the window stays, on the start page.
                doc.File.NewFile();
                ShowStartPage();
                Log("Tabs: closed the document");
                return true;
            }
            DetachTab(doc);
            doc.File.IsActive = false;
            if (documents.Count == 0)
                ShowBlankStartPage();
            else if (original != doc && documents.Contains(original))
                await ActivateDocument(original); // it was only brought forward for its prompt
            else if (wasActive)
                await ActivateDocument(documents[Math.Min(index, documents.Count - 1)]);
            Log($"Tabs: closed {doc.DisplayName}");
            return true;
        }

        // Nothing open any more: a fresh untitled document stands behind the start page, ready for
        // whatever the user opens or creates next.
        private void ShowBlankStartPage()
        {
            var blank = CreateDocument();
            activeDoc = blank;
            blank.File.IsActive = true;
            blank.File.NewFile();
            blank.HistoryReady = true;
            ShowStartPage();
        }

        // Window closing: one prompt for all unsaved documents rather than one after another.
        private async Task<bool> ConfirmCloseAllDocuments()
        {
            var dirty = documents.Where(d => d.IsDirty).ToList();
            if (dirty.Count == 0) return true;
            if (dirty.Count == 1)
            {
                await ActivateDocument(dirty[0]);
                return await ConfirmDiscardChangesIfNeeded();
            }
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = Locale.GetString("UnsavedChanges"),
                Content = Locale.Format("UnsavedDocumentsPrompt", string.Join("\n", dirty.Select(d => "• " + d.DisplayName))),
                PrimaryButtonText = Locale.GetString("SaveAll"),
                SecondaryButtonText = Locale.GetString("DontSave"),
                CloseButtonText = Locale.GetString("Cancel"),
                DefaultButton = ContentDialogButton.Primary,
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                foreach (var doc in dirty)
                {
                    await ActivateDocument(doc);
                    if (!await file.Save()) await SaveAsInternal();
                    if (file.IsDirty) return false; // Save As cancelled: the window stays
                }
                return true;
            }
            if (result != ContentDialogResult.Secondary) return false;
            foreach (var doc in dirty) doc.File.DiscardOnSwitch();
            return true;
        }

        // Moves a document, unsaved changes included, into a window of its own, opened over the
        // pointer's position when there is one (a tab dragged out of the strip).
        private async Task MoveToNewWindow(DocumentTab doc, Windows.Graphics.PointInt32? at = null)
        {
            var transfer = await PrepareTransfer(doc);
            if (transfer == null) return;
            try
            {
                var window = new MainWindow(transfer);
                if (at is Windows.Graphics.PointInt32 pointer) window.PlaceUnderPointer(pointer);
                window.Activate();
                await ReleaseMovedDocument(doc);
                Log($"Tabs: moved {doc.DisplayName} to a new window");
            }
            finally
            {
                doc.InTransfer = false;
            }
        }

        // What another window needs to take the document over, unsaved text included; null when it can't
        // be moved now (or is already being moved). The document is marked InTransfer; the caller clears
        // that once it is done, whatever happened.
        private async Task<DocumentTransfer> PrepareTransfer(DocumentTab doc)
        {
            if (doc == null || doc.InTransfer || (startPageShown && doc == activeDoc)) return null;
            if (doc.PendingPath != null && !File.Exists(doc.PendingPath)) return null;
            doc.InTransfer = true;
            try
            {
                // The editor's last keystrokes may still be on their way, as in a tab switch.
                if (doc == activeDoc && editorReady)
                {
                    await WaitForEditorQuiet();
                    await FlushEditor();
                    // A close may have taken it while we waited.
                    if (!documents.Contains(doc))
                    {
                        doc.InTransfer = false;
                        return null;
                    }
                }
            }
            catch
            {
                doc.InTransfer = false;
                throw;
            }
            return new DocumentTransfer(doc.Path, doc.PendingPath == null && doc.IsDirty ? doc.File.Markdown : null, doc.File.UntitledKey);
        }

        // Once another window has the document: it goes from here without a prompt, backup slot included.
        // Detached, not just emptied: it would keep the moved document's untitled backup slot, and its next
        // backup tick (clean now) would delete the backup the other window relies on. The window closes
        // when nothing is left and `closeWhenEmpty` (a tab merged into another window), otherwise it
        // stays on the start page.
        private async Task ReleaseMovedDocument(DocumentTab doc, bool closeWhenEmpty = false)
        {
            if (!documents.Contains(doc)) return;
            var wasActive = doc == activeDoc;
            var index = documents.IndexOf(doc);
            DetachTab(doc);
            doc.File.IsActive = false;
            if (documents.Count == 0)
            {
                if (closeWhenEmpty)
                {
                    // Not RequestClose: nothing to ask, and it would save an empty session over the real one.
                    allowClose = true;
                    Close();
                }
                else ShowBlankStartPage();
            }
            else if (wasActive)
                await ActivateDocument(documents[Math.Min(index, documents.Count - 1)]);
        }

        // The window's tab strip under the pointer, on the screen the pointer is on and kept inside its work
        // area (the screen without the taskbar). The work area comes from Win32, which gives it in virtual-screen
        // coordinates, the same as the pointer, on any arrangement of screens.
        // It keeps the size the window had (a maximized source gives a normal window, not a second maximized one).
        private void PlaceUnderPointer(Windows.Graphics.PointInt32 pointer)
        {
            if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter { State: Microsoft.UI.Windowing.OverlappedPresenterState.Maximized } presenter)
                presenter.Restore();
            var monitor = MonitorFromPoint(new CursorPoint { X = pointer.X, Y = pointer.Y }, 2 /* MONITOR_DEFAULTTONEAREST */);
            var info = new MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<MonitorInfo>() };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return; // stays where its saved placement put it
            var area = info.Work;
            var width = Math.Min(settings.WindowWidth ?? AppWindow.Size.Width, area.Right - area.Left);
            var height = Math.Min(settings.WindowHeight ?? AppWindow.Size.Height, area.Bottom - area.Top);
            var x = Math.Clamp(pointer.X - 120, area.Left, area.Right - width);
            var y = Math.Clamp(pointer.Y - 24, area.Top, area.Bottom - height);
            AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));
        }

        private sealed record DocumentTransfer(string Path, string UnsavedText, string UntitledKey);

        // --- Start page ---

        private void ShowStartPage()
        {
            startPageShown = true;
            StartPage.Visibility = Visibility.Visible;
            RefreshStartPageList();
            UpdateTitle();
        }

        private void HideStartPage()
        {
            if (!startPageShown) return;
            startPageShown = false;
            StartPage.Visibility = Visibility.Collapsed;
            UpdateTitle();
        }

        private void StartNewNote_Click(object sender, RoutedEventArgs e) => NewMenuItem_Click(this, null);

        private void StartOpenFile_Click(object sender, RoutedEventArgs e) => OpenMenuItem_Click(this, null);

        private async void StartPageRecent_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StartPageEntry entry) await OpenRecentFile(entry.FullPath);
        }

        // Favourites first, then the recent files that aren't favourites: ten rows at most.
        private void RefreshStartPageList()
        {
            favoritesService.Reload(); // another window may have changed them
            var favorites = favoritesService.Files.Where(File.Exists).Select(p => new StartPageEntry(p, true));
            var recent = recentFiles.Files.Where(File.Exists).Where(p => !favoritesService.Contains(p)).Select(p => new StartPageEntry(p, false));
            StartPageRecentList.ItemsSource = favorites.Concat(recent).Take(10).ToList();
            StartPageRecentHeader.Visibility = StartPageRecentList.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void StartPageFavorite_Click(object sender, RoutedEventArgs e)
        {
            ToggleFavorite((string)((FrameworkElement)sender).Tag);
            RefreshStartPageList();
        }

        private void StartPageRecentList_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Delete || (e.OriginalSource as FrameworkElement)?.DataContext is not StartPageEntry entry) return;
            e.Handled = true;
            var index = StartPageRecentList.Items.IndexOf(StartPageRecentList.Items.OfType<StartPageEntry>().First(i => i.FullPath == entry.FullPath));
            // Off the list either way: a favourite stops being one (the file itself is never touched).
            if (entry.IsFavorite) favoritesService.Remove(entry.FullPath);
            recentFiles.Remove(entry.FullPath);
            RefreshStartPageList();
            UpdateFavoriteButton();
            if (StartPageRecentList.Items.Count > 0)
                DispatcherQueue.TryEnqueue(() => (StartPageRecentList.ContainerFromIndex(System.Math.Min(index, StartPageRecentList.Items.Count - 1)) as Control)?.Focus(FocusState.Keyboard));
        }

        // --- Session ---

        // The documents open in this window, for next time. Untitled ones aren't listed: closing
        // prompted to save them, and a crash leaves their recovery backups.
        private void SaveSession()
        {
            var saved = documents.Select(d => d.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
            settings.OpenTabs = string.Join("\n", saved);
            settings.ActiveTab = Math.Max(0, saved.IndexOf(activeDoc.Path));
        }

        // Startup of the first window, after its first document is loaded (a file from the command
        // line, or nothing): the other documents from last time come back as tabs, loaded when opened.
        private async Task RestoreSession()
        {
            if (!TabsEnabled || !settings.RestoreTabs) return;
            var paths = (settings.OpenTabs ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(File.Exists).ToList();
            if (paths.Count == 0) return;
            var startupDoc = activeDoc;
            if (startupDoc.IsBlank)
            {
                // Nothing on the command line: the document that was in front comes back in front,
                // loaded now; the others as tabs that load when opened.
                var activeIndex = Math.Clamp(settings.ActiveTab, 0, paths.Count - 1);
                DetachTab(startupDoc);
                for (var i = 0; i < paths.Count; i++)
                    AttachTab(i == activeIndex ? startupDoc : CreatePendingDocument(paths[i]));
                SelectTab(startupDoc);
                await startupDoc.File.OpenFile(paths[activeIndex]);
            }
            else
            {
                // Opened with a file: the others come back in front of it, and it stays on screen.
                var index = 0;
                foreach (var path in paths.Where(p => !string.Equals(p, startupDoc.Path, StringComparison.OrdinalIgnoreCase)))
                    AttachTab(CreatePendingDocument(path), index++);
                SelectTab(startupDoc);
            }
            Log($"Tabs: restored {paths.Count} documents");
        }

        private DocumentTab CreatePendingDocument(string path)
        {
            var doc = CreateDocument();
            doc.PendingPath = path;
            return doc;
        }

        // Untitled documents left behind (a crash): offered back once, as tabs.
        private async Task RecoverUntitledBackups()
        {
            var open = openWindows.SelectMany(w => w.documents).Select(d => AutoBackup.GetBackupFilePath(d.File.BackupKey))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var orphans = AutoBackup.UntitledBackups().Where(f => !open.Contains(f)).OrderBy(File.GetLastWriteTimeUtc).ToList();
            if (orphans.Count == 0) return;
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = Locale.GetString("RecoverUnsavedChanges"),
                Content = orphans.Count == 1 ? Locale.GetString("RecoverUntitledPrompt") : Locale.Format("RecoverUntitledMany", orphans.Count),
                PrimaryButtonText = Locale.GetString("Recover"),
                SecondaryButtonText = Locale.GetString("Discard"),
                DefaultButton = ContentDialogButton.Primary,
            };
            var recover = await dialog.ShowAsync() == ContentDialogResult.Primary;
            foreach (var backup in orphans)
            {
                if (recover)
                {
                    string text;
                    try { text = await File.ReadAllTextAsync(backup); }
                    catch (Exception ex) { Log($"AutoBackup: couldn't read {backup}: {ex.Message}"); continue; }
                    // The recovered text is written to its new document's backup slot straight away,
                    // before the orphan is deleted: until the next backup tick it would otherwise exist
                    // only in memory, and a crash in between would lose it for good.
                    // Tabs off: into this window only if it's empty, never over a file it already has.
                    if (TabsEnabled || (backup == orphans[0] && activeDoc.IsBlank))
                    {
                        if (!await MakeRoomForDocument()) break;
                        file.NewFile();
                        file.ApplyRecoveredBackup(text);
                        if (!await AutoBackup.Backup(file.BackupKey, text)) continue;
                    }
                    else
                    {
                        var key = AutoBackup.NewUntitledKey();
                        if (!await AutoBackup.Backup(key, text)) continue;
                        var window = new MainWindow(new DocumentTransfer(null, text, key));
                        window.Activate();
                    }
                }
                try { File.Delete(backup); } catch { }
            }
            UpdateTitle();
            Log($"AutoBackup: {(recover ? "recovered" : "discarded")} {orphans.Count} untitled backups");
        }

        // --- Keyboard ---

        private async void NextTabAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            await SwitchTabRelative(+1);
        }

        private async void PreviousTabAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            await SwitchTabRelative(-1);
        }

        private void NewTabAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            NewMenuItem_Click(this, null);
        }

        // Settings: tabs switched on or off take effect straight away.
        private void UseTabsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            settings.UseTabs = UseTabsToggle.IsOn;
            RestoreTabsToggle.IsEnabled = UseTabsToggle.IsOn;
            ApplyTabsVisibility();
        }

        private void RestoreTabsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!suppressSettingsEvents) settings.RestoreTabs = RestoreTabsToggle.IsOn;
        }
    }
}
