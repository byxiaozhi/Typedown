using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Typedown.Automation;
using Typedown.Core.Models;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;

namespace Typedown.Core.Services
{
    /// <summary>
    /// The Windows application behind the automation document methods (Typedown.Automation.IAutomationHost). Every
    /// call runs on the dispatcher of the window that holds the document and continues there; nothing here shows a
    /// dialog of its own.
    /// </summary>
    public sealed class WindowsAutomationHost : IAutomationHost, IViewHost
    {
        /// <summary>The classifier version of the editor bundle (services/normalization.ts).</summary>
        public const int EditorClassifierVersion = 3;
        public const int QueryTimeoutMs = 2000;

        private readonly DocumentEditCoordinator coordinator;

        public WindowsAutomationHost(string version, IEditBarriers barriers = null)
        {
            Version = version;
            coordinator = DocumentEdits.Start(barriers);
        }

        public string Version { get; }
        public int ClassifierVersion => EditorClassifierVersion;

        private static WindowRegistry<AppViewModel> Registry => AutomationWindows.Registry;

        private static AutomationException DocumentNotFound(string documentId) =>
            new(AutomationErrorKind.document_not_found, "The document is closed or the id is not valid.", new Dictionary<string, object> { ["documentId"] = documentId });

        public const int StartupTimeoutMs = 30000;

        /// <summary>
        /// Runs work on a window once its startup document is in place: a window that has just opened would otherwise
        /// have its startup flow replace what the work did (seen in E2E R04 - a new document lost its id).
        /// </summary>
        private static async Task<T> OnWindow<T>(string windowId, Func<AppViewModel, Task<T>> work) => await (await Registry.OnWindowAsync(windowId, async app =>
        {
            var ready = app.EditorViewModel.StartupDocumentReady;
            if (!ready.IsCompleted) Log.Debug($"automation: window {windowId} waits for its startup document");
            if (await Task.WhenAny(ready, Task.Delay(StartupTimeoutMs)) != ready)
                throw new AutomationException(AutomationErrorKind.editor_not_ready, "The window has not finished starting.");
            return await work(app);
        }));

        private static async Task<(RegisteredWindow<AppViewModel> window, DocumentTab tab)> Find(string documentId)
        {
            var found = await AutomationWindows.FindDocumentAsync(documentId ?? "");
            return found ?? throw DocumentNotFound(documentId);
        }

        private static bool IsActive(AppViewModel app) => PInvoke.GetForegroundWindow() == app.MainWindow;

        private static string LineEnding(TextFileFormat format) => format?.LineEnding switch { "\r\n" => "crlf", "\r" => "cr", _ => "lf" };

        private static DocumentInfo Info(AppViewModel app, string windowId, DocumentTab tab)
        {
            var active = app.TabsViewModel?.ActiveTab == tab;
            var path = active ? app.FileViewModel.FilePath : tab.FilePath;
            return new DocumentInfo
            {
                DocumentId = tab.DocumentId,
                WindowId = windowId,
                Path = path,
                Title = string.IsNullOrEmpty(path) ? tab.Title : Path.GetFileName(path),
                Revision = tab.Revision,
                Saved = active ? app.EditorViewModel.Saved : tab.Saved,
                Active = active,
                LineEnding = LineEnding(active ? app.FileViewModel.FileFormat : tab.FileFormat),
            };
        }

        public async Task<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken)
        {
            var result = new List<WindowInfo>();
            foreach (var window in Registry.Snapshot())
            {
                try
                {
                    result.Add(await Registry.OnWindowAsync(window.WindowId, app => new WindowInfo
                    {
                        WindowId = window.WindowId,
                        Active = IsActive(app),
                        DocumentCount = app.TabsViewModel?.Tabs.Count ?? 0,
                        ActiveDocumentId = app.TabsViewModel?.ActiveTab?.DocumentId,
                    }));
                }
                catch (AutomationException e) when (e.Kind == AutomationErrorKind.window_not_found) { }
            }
            return result;
        }

        public Task FocusWindowAsync(string windowId, CancellationToken cancellationToken) =>
            Registry.OnWindowAsync(windowId, app => { Focus(app); return true; });

        private static void Focus(AppViewModel app)
        {
            if (PInvoke.IsIconic(app.MainWindow)) PInvoke.ShowWindow(app.MainWindow, PInvoke.ShowWindowCommand.Restore);
            PInvoke.SetForegroundWindow(app.MainWindow);
        }

        public async Task<IReadOnlyList<DocumentInfo>> ListDocumentsAsync(string windowId, CancellationToken cancellationToken)
        {
            var windows = windowId == null ? Registry.Snapshot().Select(w => w.WindowId).ToList() : new List<string> { windowId };
            var result = new List<DocumentInfo>();
            foreach (var id in windows)
            {
                try
                {
                    result.AddRange(await Registry.OnWindowAsync(id, app => app.TabsViewModel.Tabs.Select(t => Info(app, id, t)).ToList()));
                }
                catch (AutomationException e) when (e.Kind == AutomationErrorKind.window_not_found && windowId == null) { }
            }
            return result;
        }

        public async Task<DocumentSnapshot> GetDocumentAsync(string documentId, bool latest, CancellationToken cancellationToken)
        {
            var (window, tab) = await Find(documentId);
            return await OnWindow(window.WindowId, async app =>
            {
                var active = app.TabsViewModel.ActiveTab == tab;
                if (latest && active && !await app.EditorViewModel.SyncWithPageAsync(AutomationDocument.ReloadTimeoutMs, AutomationDocument.FlushTimeoutMs))
                    throw new AutomationException(AutomationErrorKind.content_sync_timeout, "Could not confirm the editor's latest text.");
                if (!app.TabsViewModel.Tabs.Contains(tab)) throw DocumentNotFound(documentId);
                var text = active ? app.EditorViewModel.Markdown : tab.Markdown ?? "";
                var hash = DocumentText.ContentHash(text);
                var normalization = NormalizationInfo.NotEvaluated(hash, EditorClassifierVersion);
                if (active && app.TabsViewModel.ActiveTab == tab && await app.EditorViewModel.QueryNormalizationAsync(QueryTimeoutMs) is JToken report
                    && report["sourceHash"]?.ToString() == hash && report["normalization"] is JObject n)
                    normalization = new NormalizationInfo(
                        n["pendingNormalization"]?.ToString() ?? PendingNormalization.Unknown, hash,
                        n["normalizedHash"]?.Type == JTokenType.String ? n["normalizedHash"].ToString() : null,
                        (n["reasons"] as JArray)?.Select(r => r.ToString()).ToList() ?? new List<string>(),
                        n["classifierVersion"]?.Value<int?>() ?? EditorClassifierVersion);
                return new DocumentSnapshot { Info = Info(app, window.WindowId, tab), Text = text, IsCurrent = latest || !active, Normalization = normalization };
            });
        }

        private static string TargetWindow(string windowId)
        {
            if (windowId != null) return windowId;
            var windows = Registry.Snapshot();
            if (windows.Count == 0) throw new AutomationException(AutomationErrorKind.window_not_found, Brand.Name + " has no open window.");
            var foreground = PInvoke.GetForegroundWindow();
            // Prefer the window in front; otherwise the most recently opened one.
            return windows.LastOrDefault(w => w.Window.MainWindow == foreground)?.WindowId ?? windows[windows.Count - 1].WindowId;
        }

        public async Task<DocumentInfo> OpenDocumentAsync(string path, string windowId, Reveal reveal, CancellationToken cancellationToken)
        {
            string full;
            try { full = Path.GetFullPath(path); }
            catch (Exception) { throw Params.Invalid("path", "invalid"); }
            // Checked here so the open flow never reaches its error dialog for a missing file.
            if (!File.Exists(full)) throw Params.Invalid("path", "notFound", "No file at that path.");
            foreach (var window in Registry.Snapshot())
            {
                DocumentInfo open = null;
                try
                {
                    open = await Registry.OnWindowAsync(window.WindowId, app =>
                    {
                        var tab = app.TabsViewModel?.FindByPath(full);
                        if (tab == null) return null;
                        if (reveal == Reveal.Document) Focus(app);
                        return Info(app, window.WindowId, tab);
                    });
                }
                catch (AutomationException e) when (e.Kind == AutomationErrorKind.window_not_found) { }
                if (open != null)
                {
                    if (reveal == Reveal.Document) await FocusDocumentAsync(open.DocumentId, cancellationToken);
                    return open;
                }
            }
            var target = TargetWindow(windowId);
            return await OnWindow(target, async app =>
            {
                if (!await app.FileViewModel.OpenFile(full))
                    throw new AutomationException(AutomationErrorKind.editor_not_ready, "The file could not be opened.");
                if (reveal == Reveal.Document) Focus(app);
                return Info(app, target, app.TabsViewModel.ActiveTab);
            });
        }

        public Task<DocumentInfo> CreateDocumentAsync(string windowId, Reveal reveal, CancellationToken cancellationToken)
        {
            var target = TargetWindow(windowId);
            return OnWindow(target, async app =>
            {
                var before = app.TabsViewModel.ActiveTab;
                var beforeId = before?.DocumentId;
                await app.FileViewModel.NewDocumentAsync();
                var tab = app.TabsViewModel.ActiveTab;
                if (tab == null || (tab == before && tab.DocumentId == beforeId))
                    throw new AutomationException(AutomationErrorKind.editor_not_ready, "No new document was created.");
                if (reveal == Reveal.Document) Focus(app);
                return Info(app, target, tab);
            });
        }

        public async Task<DocumentInfo> FocusDocumentAsync(string documentId, CancellationToken cancellationToken)
        {
            var (window, tab) = await Find(documentId);
            return await OnWindow(window.WindowId, async app =>
            {
                if (app.TabsViewModel.ActiveTab != tab) await app.TabsViewModel.SwitchTo(tab);
                Focus(app);
                return Info(app, window.WindowId, tab);
            });
        }

        public async Task<EditResult> EditDocumentAsync(string documentId, EditRequest request, Reveal reveal, CancellationToken cancellationToken)
        {
            if (reveal == Reveal.Document) await FocusDocumentAsync(documentId, cancellationToken);
            var (window, tab) = await Find(documentId);
            return await OnWindow(window.WindowId, app => coordinator.EditAsync(new AutomationDocument(app, tab), request, cancellationToken));
        }

        private static AutomationException NotActive() =>
            new(AutomationErrorKind.editor_not_ready, "Only the document shown in its window can do this in this version; focus it first.",
                new Dictionary<string, object> { ["reason"] = "notActive" });

        public async Task<(DocumentInfo info, string contentHash)> SaveDocumentAsync(string documentId, long? baseRevision, CancellationToken cancellationToken)
        {
            var (window, tab) = await Find(documentId);
            return await OnWindow(window.WindowId, async app =>
            {
                if (app.TabsViewModel.ActiveTab != tab) throw NotActive();
                if (app.FileViewModel.FilePath == null) throw new AutomationException(AutomationErrorKind.path_required, "The document has no file yet; the API does not open a Save As dialog.");
                if (!await app.EditorViewModel.SyncWithPageAsync(AutomationDocument.ReloadTimeoutMs, AutomationDocument.FlushTimeoutMs))
                    throw new AutomationException(AutomationErrorKind.content_sync_timeout, "Could not confirm the editor's latest text.");
                if (baseRevision != null && baseRevision != tab.Revision)
                    throw new AutomationException(AutomationErrorKind.revision_conflict, "The document changed since baseRevision.", new Dictionary<string, object> { ["revision"] = tab.Revision });
                if (!await app.FileViewModel.SaveToExistingPathAsync())
                    throw new AutomationException(AutomationErrorKind.save_failed, "Saving failed; the document stays unsaved.", new Dictionary<string, object> { ["revision"] = tab.Revision });
                return (Info(app, window.WindowId, tab), DocumentText.ContentHash(app.EditorViewModel.Markdown));
            });
        }

        public async Task<Presentation> AwaitPresentationAsync(string documentId, int timeoutMs, CancellationToken cancellationToken)
        {
            var (window, tab) = await Find(documentId);
            return await OnWindow(window.WindowId, async app =>
            {
                var deadline = Task.Delay(timeoutMs);
                var tabActive = app.TabsViewModel.ActiveTab == tab;
                // The page's two frames and one host render run side by side; each only reports whether it came in time.
                var frames = tabActive ? app.EditorViewModel.AwaitPageFramesAsync(timeoutMs) : Task.FromResult(false);
                var rendered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                EventHandler<object> onRendering = (sender, e) => rendered.TrySetResult(true);
                global::Windows.UI.Xaml.Media.CompositionTarget.Rendering += onRendering;
                try
                {
                    await Task.WhenAny(rendered.Task, deadline);
                    var pageFramesPassed = await frames;
                    return new Presentation
                    {
                        WindowVisible = PInvoke.IsWindowVisible(app.MainWindow) && !PInvoke.IsIconic(app.MainWindow),
                        TabActive = app.TabsViewModel.ActiveTab == tab,
                        PageFramesPassed = pageFramesPassed,
                        HostRenderPassed = rendered.Task.IsCompleted,
                    };
                }
                finally
                {
                    global::Windows.UI.Xaml.Media.CompositionTarget.Rendering -= onRendering;
                }
            });
        }

        public async Task<EditResult> UndoAsync(string documentId, long baseRevision, bool redo, bool save, Reveal reveal, CancellationToken cancellationToken)
        {
            if (reveal == Reveal.Document) await FocusDocumentAsync(documentId, cancellationToken);
            var (window, tab) = await Find(documentId);
            return await OnWindow(window.WindowId, async app =>
            {
                if (app.TabsViewModel.ActiveTab != tab) throw NotActive();
                var editor = app.EditorViewModel;
                if (!await editor.SyncWithPageAsync(AutomationDocument.ReloadTimeoutMs, AutomationDocument.FlushTimeoutMs))
                    throw new AutomationException(AutomationErrorKind.content_sync_timeout, "Could not confirm the editor's latest text.");
                if (tab.Revision != baseRevision)
                    throw new AutomationException(AutomationErrorKind.revision_conflict, "The document changed since baseRevision.", new Dictionary<string, object> { ["revision"] = tab.Revision });
                if (redo) editor.Redo(); else editor.Undo();
                var saved = editor.Saved;
                if (save)
                {
                    if (app.FileViewModel.FilePath == null) throw new AutomationException(AutomationErrorKind.path_required, "The document has no file yet.");
                    if (!await app.FileViewModel.SaveToExistingPathAsync())
                        throw new AutomationException(AutomationErrorKind.save_failed, "Undone, but saving failed.", new Dictionary<string, object> { ["applied"] = true, ["revision"] = tab.Revision });
                    saved = true;
                }
                var hash = DocumentText.ContentHash(editor.Markdown);
                return new EditResult(Guid.NewGuid().ToString("N"), tab.Revision, hash, saved, NormalizationInfo.NotEvaluated(hash, EditorClassifierVersion));
            });
        }

        public async Task<DocumentInfo> CloseDocumentAsync(string documentId, long? baseRevision, CancellationToken cancellationToken)
        {
            var (window, tab) = await Find(documentId);
            Log.Debug($"close {documentId}: found in {window.WindowId}");
            // Between automation edits of this document, never during one.
            return await coordinator.ExclusiveAsync(documentId, () => OnWindow(window.WindowId, async app =>
            {
                if (!app.TabsViewModel.Tabs.Contains(tab)) throw DocumentNotFound(documentId);
                var active = app.TabsViewModel.ActiveTab == tab;
                Log.Debug($"close {documentId}: on the window, active {active}, load {app.EditorViewModel.LoadId} confirmed {app.EditorViewModel.ConfirmedLoadId}");
                // Typing the page has not reported yet counts: its latest text first.
                if (active && !await app.EditorViewModel.SyncWithPageAsync(AutomationDocument.ReloadTimeoutMs, AutomationDocument.FlushTimeoutMs))
                    throw new AutomationException(AutomationErrorKind.content_sync_timeout, "Could not confirm the editor's latest text.");
                if (baseRevision != null && baseRevision != tab.Revision)
                    throw new AutomationException(AutomationErrorKind.revision_conflict, "The document changed since baseRevision.", new Dictionary<string, object> { ["revision"] = tab.Revision });
                var saved = active ? app.EditorViewModel.Saved : tab.Saved;
                if (!saved)
                    throw new AutomationException(AutomationErrorKind.unsaved_changes, "The document has unsaved changes; save it first. Nothing was closed.",
                        new Dictionary<string, object> { ["revision"] = tab.Revision });
                var info = Info(app, window.WindowId, tab);
                // The window's only document: a new empty one comes first, so the window stays.
                if (app.TabsViewModel.Tabs.Count == 1) await app.FileViewModel.NewDocumentAsync();
                if (app.TabsViewModel.Tabs.Count == 1 || !await app.TabsViewModel.CloseTab(tab))
                    throw new AutomationException(AutomationErrorKind.editor_not_ready, "The document could not be closed.");
                coordinator.Forget(documentId);
                return info;
            }), cancellationToken);
        }

        public async Task DiscardDocumentAsync(string documentId, CancellationToken cancellationToken)
        {
            var (window, tab) = await Find(documentId);
            await OnWindow(window.WindowId, async app =>
            {
                // The only tab closes its window: an empty untitled document is left instead.
                if (app.TabsViewModel.Tabs.Count > 1) await app.TabsViewModel.CloseTab(tab);
                coordinator.Forget(documentId);
                return true;
            });
        }

        private static ViewState ReadView(AppViewModel app)
        {
            var settings = app.SettingsViewModel;
            PInvoke.GetWindowRect(app.MainWindow, out var r);
            return new ViewState
            {
                Mode = settings.SourceCode ? "source" : settings.ReadOnly ? "reading" : "visual",
                SidePaneOpen = settings.SidePaneOpen,
                SidePanePage = settings.SidePaneIndex == 1 ? "outline" : "files",
                StatusBar = settings.StatusBarOpen,
                FocusMode = settings.FocusMode,
                Typewriter = settings.Typewriter,
                X = r.left,
                Y = r.top,
                Width = r.right - r.left,
                Height = r.bottom - r.top,
                Maximized = PInvoke.IsZoomed(app.MainWindow),
            };
        }

        public Task<ViewState> GetViewAsync(string windowId, CancellationToken cancellationToken) =>
            Registry.OnWindowAsync(windowId, ReadView);

        /// <summary>
        /// Mode, side pane, status bar, focus and typewriter are the app's own settings (the menu changes the same ones,
        /// and they are remembered); bounds belong to this window. A mode switch waits until the editor shows the
        /// document again, and runs between - never during - automation edits of the window's active document.
        /// </summary>
        public async Task<ViewState> SetViewAsync(string windowId, ViewChange change, CancellationToken cancellationToken)
        {
            var documentId = await Registry.OnWindowAsync(windowId, app => app.TabsViewModel?.ActiveTab?.DocumentId ?? "");
            return await coordinator.ExclusiveAsync(documentId, () => OnWindow(windowId, async app =>
            {
                var settings = app.SettingsViewModel;
                var editor = app.EditorViewModel;
                var modeChanged = change.Mode != null && change.Mode != ReadView(app).Mode;
                if (modeChanged)
                {
                    // Setting one on turns the other off (SettingsViewModel); visual turns both off.
                    if (change.Mode == "source") settings.SourceCode = true;
                    else if (change.Mode == "reading") settings.ReadOnly = true;
                    else { settings.SourceCode = false; settings.ReadOnly = false; }
                }
                if (change.SidePanePage != null) settings.SidePaneIndex = change.SidePanePage == "outline" ? 1 : 0;
                if (change.SidePaneOpen is bool open) settings.SidePaneOpen = open;
                if (change.StatusBar is bool statusBar) settings.StatusBarOpen = statusBar;
                if (change.FocusMode is bool focus) settings.FocusMode = focus;
                if (change.Typewriter is bool typewriter) settings.Typewriter = typewriter;
                if (change.X != null || change.Y != null || change.Width != null || change.Height != null)
                {
                    var hwnd = app.MainWindow;
                    if (PInvoke.IsZoomed(hwnd) || PInvoke.IsIconic(hwnd)) PInvoke.ShowWindow(hwnd, PInvoke.ShowWindowCommand.Restore);
                    PInvoke.GetWindowRect(hwnd, out var r);
                    PInvoke.SetWindowPos(hwnd, IntPtr.Zero, change.X ?? r.left, change.Y ?? r.top, change.Width ?? r.right - r.left, change.Height ?? r.bottom - r.top,
                        PInvoke.SetWindowPosFlags.SWP_NOZORDER | PInvoke.SetWindowPosFlags.SWP_NOACTIVATE);
                }
                if (modeChanged && !await editor.WaitForLoadAsync(AutomationDocument.ReloadTimeoutMs))
                    throw new AutomationException(AutomationErrorKind.content_sync_timeout, "The editor did not show the document again after the mode switch.");
                // Two page frames after the change: what a screenshot taken now shows is the new view.
                await editor.AwaitPageFramesAsync(QueryTimeoutMs);
                return ReadView(app);
            }), cancellationToken);
        }
    }
}
