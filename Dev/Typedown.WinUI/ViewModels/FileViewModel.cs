using System;
using System.IO;
using System.Threading.Tasks;
using Typedown.WinUI.Interfaces;
using Typedown.WinUI.Models;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI.ViewModels
{
    // Drastically reduced port of Typedown.Core\ViewModels\FileViewModel.cs (560 lines in the
    // original). The original is deeply coupled to infrastructure this scaffold doesn't have yet:
    // AccessHistory (EF Core), AppContentDialog, multi-window focus stealing, native "recent
    // files"/export menus, and the Command<T> bindings the original XAML used. This slice covers what
    // milestone #3's menu bar actually needs and can exercise for real: New, Open, Save, Save As, the
    // startup command-line file load, dirty-state tracking for the unsaved-changes prompt, and (later,
    // see the AutoBackup region below) crash-recovery backups. FileStartupAction.OpenLast/AccessHistory
    // are still TODOs.
    public sealed class FileViewModel
    {
        private readonly SettingsViewModel settings;
        private readonly IMarkdownEditor markdownEditor;

        // The original tracked this via EditorViewModel.FileHash/CurrentHash (hash comparison); we just
        // compare against the last-loaded-or-saved text directly, which is simpler and cheap enough for
        // realistic document sizes.
        private string savedSnapshot = "";

        // The editor normalizes loaded text (an empty document becomes "\n"), so savedSnapshot has to
        // be re-based on what the editor actually holds after a load. That used to key off the first
        // MarkdownChange after a load, but the editor only sometimes sends one — when it didn't, the
        // user's first real edit was taken for the load echo and recorded as already saved: no dirty
        // mark, no AutoSave, no prompt on close, so a single paste/cut/delete right after opening a
        // file could be silently lost. FileLoaded, which the editor always sends ~100ms after every
        // load (Typedown.Editor/src/components/Editor/index.tsx), is used instead. The value says
        // whether the load represents what's on disk (clean) or recovered/imported text that isn't
        // saved anywhere yet (stays dirty); null means no load is waiting to be confirmed.
        private bool? pendingLoadIsClean;

        public string FilePath { get; private set; }

        public string Markdown { get; private set; } = "";

        // Set when the open file wasn't UTF-8 and was read in Windows' legacy code page instead
        // (Utilities/TextFileEncoding.cs), e.g. "Windows-1250". Saving writes UTF-8, which clears it.
        public string LegacyEncodingName { get; private set; }

        // Line endings and the final newline don't count: the editor writes "\n" and may add a newline
        // at the end, and when its first render is slower than the FileLoaded confirmation (a startup
        // file with Windows line endings), a document nobody touched would otherwise read as unsaved.
        public bool IsDirty => Normalized(Markdown) != Normalized(savedSnapshot);

        private static string Normalized(string text) => text?.Replace("\r\n", "\n").TrimEnd('\n');

        public string ImageBasePath => string.IsNullOrEmpty(FilePath) ? settings.DefaultImageBasePath : Path.GetDirectoryName(FilePath);

        public string DisplayName => string.IsNullOrEmpty(FilePath) ? Locale.GetString("Untitled") : Path.GetFileName(FilePath);

        public event Action FileStateChanged;

        // With tabs, a window holds several documents but one editor: only the document on screen
        // takes the editor's MarkdownChange/FileLoaded events. The others keep their text as it was.
        public bool IsActive { get; set; } = true;

        // Crash-recovery slot. A saved file's is its path, as before; an untitled document gets one of
        // its own, so two untitled tabs don't overwrite each other's backup.
        private readonly string untitledKey;

        public string BackupKey => string.IsNullOrEmpty(FilePath) ? untitledKey : FilePath;

        // Only for "Move to new window": the untitled slot travels with the document.
        public string UntitledKey => untitledKey;

        // Switching tabs re-renders a document the editor may echo back slightly reformatted (the
        // same normalization pendingLoadIsClean handles on a load). Until the editor's next
        // StateChange, a clean document takes that echo as its saved state instead of turning dirty.
        private bool? pendingEchoIsClean;

        // The document that was on screen before: a late report of its text is never this document's.
        private string echoForeignText;

        public void ExpectEcho(string previousText)
        {
            pendingEchoIsClean = !IsDirty;
            echoForeignText = previousText;
        }

        public void EndEcho()
        {
            pendingEchoIsClean = null;
            echoForeignText = null;
        }

        public FileViewModel(SettingsViewModel settings, EventCenter eventCenter, IMarkdownEditor markdownEditor, string untitledKey = null)
        {
            this.settings = settings;
            this.markdownEditor = markdownEditor;
            this.untitledKey = untitledKey ?? AutoBackup.NewUntitledKey();
            // Mirrors EditorViewModel's constructor in the original: the editor pushes its live text
            // back on every change (see Transport's "diffmsg" handling), so Save always has the
            // current content without a separate "give me the text" round trip.
            eventCenter.GetObservable<EditorEventArgs>("MarkdownChange").Subscribe(x =>
            {
                if (!IsActive) return;
                var text = x.Args["text"]?.ToString();
                if (pendingEchoIsClean != null && echoForeignText != null && text == echoForeignText) return;
                Markdown = text ?? Markdown;
                if (pendingEchoIsClean == true) savedSnapshot = Markdown;
                FileStateChanged?.Invoke();
            });
            eventCenter.GetObservable<EditorEventArgs>("FileLoaded").Subscribe(x =>
            {
                if (!IsActive || pendingLoadIsClean == null) return;
                if (pendingLoadIsClean == true)
                    savedSnapshot = x.Args["text"]?.ToString() ?? savedSnapshot;
                pendingLoadIsClean = null;
                FileStateChanged?.Invoke();
            });
        }

        public async Task LoadStartUpMarkdown()
        {
            var path = Program.StartupFilePath ?? CommandLine.GetOpenFilePath(Environment.GetCommandLineArgs());
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    var read = await TextFileEncoding.ReadAsync(path);
                    Markdown = read.Text;
                    LegacyEncodingName = read.LegacyName;
                    FilePath = path;
                }
                catch
                {
                    Markdown = "";
                }
            }
            savedSnapshot = Markdown;
            // The startup document isn't pushed via LoadFile — it goes out in the GetSettings response
            // instead — but the editor still sends FileLoaded once mounted, same as any other load.
            pendingLoadIsClean = true;
        }

        public void NewFile()
        {
            FilePath = null;
            LegacyEncodingName = null;
            Markdown = "";
            savedSnapshot = Markdown;
            CompleteDiscard();
            pendingLoadIsClean = true;
            PushToEditor();
            FileStateChanged?.Invoke();
        }

        public async Task OpenFile(string path)
        {
            if (!File.Exists(path)) return;
            var read = await TextFileEncoding.ReadAsync(path);
            Markdown = read.Text;
            LegacyEncodingName = read.LegacyName;
            savedSnapshot = Markdown;
            FilePath = path;
            CompleteDiscard();
            pendingLoadIsClean = true;
            PushToEditor();
            FileStateChanged?.Invoke();
        }

        // Ported from the original's FileViewModel.RenameFile(): updates the tracked path after the
        // caller (MainWindow's folder-tree Rename) has already moved the file on disk, without
        // touching Markdown/savedSnapshot — so unsaved edits to the currently-open file survive a
        // rename instead of being silently discarded by a reload.
        public void RenamePathOnly(string newPath)
        {
            FilePath = newPath;
            FileStateChanged?.Invoke();
        }

        // Returns false when there's no FilePath yet — caller (MainWindow) should fall back to SaveAs.
        public async Task<bool> Save()
        {
            if (string.IsNullOrEmpty(FilePath)) return false;
            await File.WriteAllTextAsync(FilePath, Markdown);
            LegacyEncodingName = null; // written as UTF-8 now
            savedSnapshot = Markdown;
            // The document is now safely on disk for real — any recovery backup for it is obsolete.
            AutoBackup.DeleteBackup(BackupKey);
            FileStateChanged?.Invoke();
            return true;
        }

        public async Task SaveAs(string path)
        {
            await File.WriteAllTextAsync(path, Markdown);
            // Clears the backup slot this document was using before it had a real save location
            // (its untitled slot, or the old path) — matches the original's ordering of deleting
            // under the *old* key before reassigning FilePath.
            AutoBackup.DeleteBackup(BackupKey);
            FilePath = path;
            LegacyEncodingName = null;
            savedSnapshot = Markdown;
            FileStateChanged?.Invoke();
        }

        // --- AutoBackup ---
        // Ported from the original's FileViewModel.AutoBackupFile()/CheckBackup(). Split across two
        // halves like the rest of this port's dialog-free model / MainWindow-owns-dialogs split:
        // BackupTick is the silent safety-net write MainWindow's timer calls every few seconds;
        // PeekBackup/DiscardBackup/ApplyRecoveredBackup let MainWindow drive the Recover/Discard
        // prompt (which needs a XamlRoot this class doesn't have) after any load.
        public bool ShouldBackup => IsDirty && !string.IsNullOrWhiteSpace(Markdown);

        // The editor going blank while the file on disk still has content is the signature of editor
        // state corruption (the pre-fix paste bug did exactly this, and auto-save then wrote the blank
        // buffer over a real document), not something to persist unattended. Auto-save skips it and
        // the file stays dirty; an explicit Ctrl+S still saves, since that's the user choosing it.
        public bool WouldBlankSavedFile => string.IsNullOrWhiteSpace(Markdown) && !string.IsNullOrWhiteSpace(savedSnapshot);

        // Returns true when a backup write actually happened (for the caller's log line). A backup is
        // only deleted once the buffer matches disk again — not when it merely went blank while still
        // dirty, which would throw away the last good recovery copy exactly when it's needed.
        public async Task<bool> BackupTick()
        {
            if (ShouldBackup) return await AutoBackup.Backup(BackupKey, Markdown);
            if (!IsDirty) AutoBackup.DeleteBackup(BackupKey);
            return false;
        }

        public Task<string> PeekBackup(string path) => AutoBackup.GetBackup(path);

        public void DiscardBackup(string path) => AutoBackup.DeleteBackup(path);

        // "Don't Save": the recovery backup of the text being thrown away is deleted once the document
        // is really replaced (NewFile/OpenFile, after the new content is in place, so the backup timer
        // can't write the old text again in between) or its window closes, not when the user answers:
        // a file picker may still follow, and if it's cancelled the text stays open and protected.
        private bool discardPending;
        private string discardPath;

        public void DiscardOnSwitch()
        {
            discardPending = true;
            discardPath = BackupKey;
        }

        public void CompleteDiscard()
        {
            if (!discardPending) return;
            discardPending = false;
            AutoBackup.DeleteBackup(discardPath);
        }

        // Swaps in recovered backup text after NewFile/OpenFile/LoadStartUpMarkdown already ran.
        // savedSnapshot is deliberately left mismatched (not set to the recovered text) so IsDirty
        // reads true — the recovered content exists only in the backup and the live buffer, not on
        // disk yet, same as the original's Saved = false for this case.
        public void ApplyRecoveredBackup(string text)
        {
            Markdown = text;
            savedSnapshot = null;
            pendingLoadIsClean = false;
            PushToEditor();
            FileStateChanged?.Invoke();
        }

        // Undo/Redo: the host pushes a history snapshot to the editor itself (SetMarkdown, with the
        // cursor), so only the tracked text is updated here — no LoadFile push. Setting it directly
        // rather than waiting for the editor's MarkdownChange echo means Save/AutoSave can never
        // write the pre-undo text while the screen already shows the undone one.
        public void ReplaceBuffer(string text)
        {
            Markdown = text;
            FileStateChanged?.Invoke();
        }

        private void PushToEditor() => markdownEditor?.PostMessage("LoadFile", new { text = Markdown, basePath = ImageBasePath });
    }
}
