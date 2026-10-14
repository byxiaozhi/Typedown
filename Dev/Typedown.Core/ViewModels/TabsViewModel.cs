using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Services;
using Typedown.Core.Utilities;

namespace Typedown.Core.ViewModels
{
    /// <summary>
    /// Document tabs for one window (upstream #30, #73, #85). There is a single editor; switching tabs snapshots
    /// the current document into its <see cref="DocumentTab"/> and restores the target one into the editor.
    /// </summary>
    public sealed partial class TabsViewModel : INotifyPropertyChanged, IDisposable
    {
        public IServiceProvider ServiceProvider { get; }

        public FileViewModel FileViewModel => ServiceProvider.GetService<FileViewModel>();

        public EditorViewModel EditorViewModel => ServiceProvider.GetService<EditorViewModel>();

        public IMarkdownEditor MarkdownEditor => ServiceProvider.GetService<IMarkdownEditor>();

        public ObservableCollection<DocumentTab> Tabs { get; } = new();

        public DocumentTab ActiveTab { get; private set; }

        public Command<DocumentTab> SwitchTabCommand { get; } = new();

        public Command<DocumentTab> CloseTabCommand { get; } = new();

        public Command<Unit> CloseActiveTabCommand { get; } = new();

        public Command<Unit> NextTabCommand { get; } = new();

        public Command<Unit> PreviousTabCommand { get; } = new();

        /// <summary>Picks a tab by position, counted from zero; anything past the last tab means the last one.</summary>
        public Command<int> SwitchTabIndexCommand { get; } = new();

        /// <summary>Back to the tab used before this one — two tabs out of many, swapped between.</summary>
        public Command<Unit> LastUsedTabCommand { get; } = new();

        private readonly CompositeDisposable disposables = new();

        private bool switching;

        private DocumentTab tabBeforeNew;

        public event PropertyChangedEventHandler PropertyChanged;

        public TabsViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            ActiveTab = new DocumentTab();
            Tabs.Add(ActiveTab);
            SwitchTabCommand.OnExecute.Subscribe(async tab => await SwitchTo(tab));
            CloseTabCommand.OnExecute.Subscribe(async tab => await CloseTab(tab));
            CloseActiveTabCommand.OnExecute.Subscribe(async _ => await CloseTab(ActiveTab));
            NextTabCommand.OnExecute.Subscribe(async _ => await SwitchRelative(1));
            PreviousTabCommand.OnExecute.Subscribe(async _ => await SwitchRelative(-1));
            SwitchTabIndexCommand.OnExecute.Subscribe(async index => await SwitchToIndex(index));
            LastUsedTabCommand.OnExecute.Subscribe(async _ => await SwitchToLastUsed());
            // Keep the active tab's label in sync with the editor state.
            disposables.Add(FileViewModel.WhenPropertyChanged(nameof(FileViewModel.FilePath)).Subscribe(_ => { if (!switching) ActiveTab.FilePath = FileViewModel.FilePath; }));
            disposables.Add(EditorViewModel.WhenPropertyChanged(nameof(EditorViewModel.Saved)).Subscribe(_ =>
            {
                if (switching) return;
                ActiveTab.IsDirty = !EditorViewModel.Saved;
                if (ActiveTab.IsDirty) ActiveTab.IsPreview = false;
            }));
        }

        public DocumentTab FindByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string full;
            try { full = Path.GetFullPath(path); } catch { full = path; }
            return Tabs.FirstOrDefault(t => !string.IsNullOrEmpty(t.FilePath) && string.Equals(SafeFullPath(t.FilePath), full, StringComparison.OrdinalIgnoreCase));
        }

        private static string SafeFullPath(string path)
        {
            try { return Path.GetFullPath(path); } catch { return path; }
        }

        /// <summary>True when the active tab is a pristine untitled document that a new/open action may reuse.</summary>
        public bool IsActiveTabBlank => FileViewModel.FilePath == null && EditorViewModel.Saved && EditorViewModel.CurrentHash == Common.SimpleHash(Common.DefaultMarkdwn);

        /// <summary>
        /// Single-click open navigates within the current tab: it may replace the active document whenever it has no
        /// unsaved changes (a dirty document gets a new tab instead).
        /// </summary>
        public bool CanReuseActiveTabForPreview => IsActiveTabBlank || EditorViewModel.Saved;

        /// <summary>Copies the live editor state into the active tab.</summary>
        public void SnapshotActive()
        {
            var tab = ActiveTab;
            var editor = EditorViewModel;
            tab.FilePath = FileViewModel.FilePath;
            tab.FileFormat = FileViewModel.FileFormat;
            tab.Markdown = editor.Markdown;
            tab.CurrentHash = editor.CurrentHash;
            tab.FileHash = editor.FileHash;
            tab.DiskHash = FileViewModel.DiskHash;
            tab.Saved = editor.Saved;
            tab.AutoSavedSucc = editor.AutoSavedSucc;
            tab.FileLoaded = editor.FileLoaded;
            tab.Cursor = editor.CurrentCursor;
            tab.History = editor.History;
            tab.IsDirty = !editor.Saved;
        }

        /// <summary>
        /// Snapshots the current document and makes a fresh tab active; the caller then loads content into the editor
        /// through the normal file flows (which set FilePath, hashes and post LoadFile).
        /// </summary>
        public DocumentTab BeginNewTab()
        {
            SnapshotActive();
            tabBeforeNew = ActiveTab;
            var tab = new DocumentTab();
            Tabs.Add(tab);
            ActiveTab = tab;
            // Bind the editor to the new tab's own (fresh) history. SnapshotActive just stored the live history
            // object into the previous tab; without this the editor would keep that same object, and the load
            // that follows (History.InitHistory) would reinitialise it — wiping the previous tab's undo history
            // and mixing the new document's content into it.
            EditorViewModel.History = tab.History;
            return tab;
        }

        /// <summary>Undo <see cref="BeginNewTab"/> when loading the new document failed.</summary>
        public void AbortNewTab(DocumentTab tab)
        {
            if (tab == null || !Tabs.Contains(tab) || tab != ActiveTab) return;
            Tabs.Remove(tab);
            var back = tabBeforeNew != null && Tabs.Contains(tabBeforeNew) ? tabBeforeNew : Tabs.FirstOrDefault();
            ActiveTab = back;
            tabBeforeNew = null;
            // BeginNewTab pointed the editor's history at the tab we just removed (and the failed load may have
            // posted some content), so restoring only ActiveTab would leave the editor on the wrong history and
            // buffer — the previous tab's undo would be broken. Restore the full document state of the tab we fell
            // back to.
            if (back != null) Restore(back);
        }

        /// <summary>The tab that was active before this one, for the shortcut that jumps back and forth.</summary>
        private DocumentTab previousTab;

        /// <summary>
        /// Back to the tab used before this one, the way Alt+Tab switches between two windows. With no history
        /// yet it behaves as "next tab".
        /// </summary>
        private async Task SwitchToLastUsed()
        {
            if (previousTab != null && previousTab != ActiveTab && Tabs.Contains(previousTab))
                await SwitchTo(previousTab);
            else
                await SwitchRelative(1);
        }

        public async Task SwitchTo(DocumentTab tab)
        {
            if (tab == null || tab == ActiveTab || !Tabs.Contains(tab)) return;
            // Bring the outgoing document's latest edits in and let its pending change reports resolve before we
            // snapshot it and load the next one. Without this a throttled MarkdownChange for the tab being left
            // can arrive after the switch and land on the tab now shown — one document's text crossing into
            // another. Flushing captures the accurate text and drains those reports first.
            await EditorViewModel.FlushContentAsync();
            // Looked at again after the wait: the tab may have been shown in the meantime. Closing a tab removes it from
            // the strip, the strip selects the neighbour and asks for it here, and the close then shows that very tab
            // itself - loaded twice, the first load's reports were dropped as stale and the page, given the same text
            // again, did not report it again: the outline stayed on the closed document (E2E TC02).
            if (tab == ActiveTab || !Tabs.Contains(tab)) return;
            switching = true;
            try
            {
                SnapshotActive();
                previousTab = ActiveTab;
                ActiveTab = tab;
                Restore(tab);
            }
            finally
            {
                switching = false;
            }
            await FileViewModel.CheckExternalChangeAfterSwitch();
        }

        private async Task SwitchToIndex(int index)
        {
            if (Tabs.Count == 0) return;
            var tab = index >= Tabs.Count ? Tabs[Tabs.Count - 1] : Tabs[Math.Max(index, 0)];
            if (tab != ActiveTab) await SwitchTo(tab);
        }

        private async Task SwitchRelative(int delta)
        {
            if (Tabs.Count < 2) return;
            var index = (Tabs.IndexOf(ActiveTab) + delta + Tabs.Count) % Tabs.Count;
            await SwitchTo(Tabs[index]);
        }

        private void Restore(DocumentTab tab)
        {
            var editor = EditorViewModel;
            FileViewModel.SetFilePathFromTab(tab.FilePath);
            FileViewModel.FileFormat = tab.FileFormat ?? Utilities.TextFileFormat.Default;
            editor.History = tab.History ?? new ContentHistory();
            editor.Markdown = tab.Markdown ?? Common.DefaultMarkdwn;
            editor.FileHash = tab.FileHash;
            FileViewModel.DiskHash = tab.DiskHash;
            editor.CurrentHash = tab.CurrentHash;
            editor.Saved = tab.Saved;
            editor.AutoSavedSucc = tab.AutoSavedSucc;
            // A tab that was shown before keeps its history/hash (the handshake must not reset them); one loaded
            // in the background (session restore) has never been through the editor and still needs it.
            editor.FileLoaded = tab.FileLoaded;
            editor.CurrentCursor = tab.Cursor;
            tab.IsDirty = !tab.Saved;
            var cursor = tab.Cursor ?? (ServiceProvider.GetService<SettingsViewModel>().RememberCursorPosition ? CursorMemory.Get(tab.FilePath) : null);
            editor.PostLoadFile(editor.Markdown, cursor);
        }

        public async Task<bool> CloseTab(DocumentTab tab)
        {
            if (tab == null || !Tabs.Contains(tab)) return false;
            if (Tabs.Count == 1)
            {
                // Closing the only document closes the window (the window's own close flow asks to save).
                FileViewModel.ExitCommand.Execute(default);
                return true;
            }
            if (tab != ActiveTab) await SwitchTo(tab);
            if (!await FileViewModel.AskToSave()) return false;
            // Closed on purpose: an untitled document's backup must not come back at the next start.
            if (string.IsNullOrEmpty(FileViewModel.FilePath)) FileViewModel.AutoBackup.DeleteBackup(null, tab.DocumentId);
            // Saved or explicitly discarded: nothing in the editor needs to survive.
            EditorViewModel.Saved = true;
            var index = Tabs.IndexOf(tab);
            Tabs.Remove(tab);
            var next = Tabs[Math.Min(index, Tabs.Count - 1)];
            switching = true;
            try
            {
                ActiveTab = next;
                Restore(next);
            }
            finally
            {
                switching = false;
            }
            await FileViewModel.CheckExternalChangeAfterSwitch();
            return true;
        }

        /// <summary>Asks to save every dirty tab (window close). Returns false if the user cancels.</summary>
        public async Task<bool> AskToSaveAll()
        {
            // Bring the live editor's latest text in before judging what is dirty: an edit made within the report
            // throttle would otherwise still read as saved, and the window would close without asking about it.
            // If the editor did not answer, treat the active document as dirty rather than closing it silently.
            var flushed = await EditorViewModel.FlushContentAsync();
            foreach (var tab in Tabs.ToList())
            {
                var dirty = tab == ActiveTab ? (!EditorViewModel.Saved || !flushed) : !tab.Saved;
                if (!dirty) continue;
                await SwitchTo(tab);
                await FileViewModel.AutoSaveFile();
                if (!EditorViewModel.Saved && !await FileViewModel.AskToSave())
                    return false;
            }
            return true;
        }

        public bool IsOpenInAnyTab(string path) => FindByPath(path) != null;

        /// <summary>Writes the open documents (untitled ones excluded) and the active one for the next start.</summary>
        public void SaveSession()
        {
            var files = Tabs.Select(t => t == ActiveTab ? FileViewModel.FilePath : t.FilePath).ToList();
            var activeIndex = Tabs.IndexOf(ActiveTab);
            var activePath = activeIndex >= 0 ? files[activeIndex] : null;
            var kept = files.Where(f => !string.IsNullOrEmpty(f)).ToList();
            SessionMemory.Save(kept, Math.Max(0, kept.IndexOf(activePath)));
        }

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
