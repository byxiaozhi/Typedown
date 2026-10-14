using System;
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
    /// A Windows tab as the automation edit coordinator sees it (Typedown.Automation.IEditableDocument). Every member
    /// runs on the tab's window dispatcher: the coordinator is started there and stays there. The active tab is edited
    /// through its editor page; a background tab only has the snapshot the tab holds.
    /// </summary>
    public sealed class AutomationDocument : IEditableDocument
    {
        public const int FlushTimeoutMs = 2000;
        public const int ApplyTimeoutMs = 10000;
        public const int ReloadTimeoutMs = 15000;

        private readonly AppViewModel app;
        private readonly DocumentTab tab;

        public AutomationDocument(AppViewModel app, DocumentTab tab)
        {
            this.app = app;
            this.tab = tab;
        }

        private EditorViewModel Editor => app.EditorViewModel;

        public string DocumentId => tab.DocumentId;
        public long Revision => tab.Revision;
        public bool IsActive => app.TabsViewModel?.ActiveTab == tab;
        public string Text => IsActive ? Editor.Markdown : tab.Markdown ?? "";
        public bool Saved => IsActive ? Editor.Saved : tab.Saved;

        public async Task<bool> FlushAsync(CancellationToken cancellationToken) =>
            await Editor.SyncWithPageAsync(ReloadTimeoutMs, FlushTimeoutMs);

        private sealed class State
        {
            public string Text;
            public object Cursor;
            public double ScrollTop;
            public int LoadId;
        }

        public object CaptureState() => new State { Text = Editor.Markdown, Cursor = Editor.CurrentCursor, ScrollTop = Editor.LastScrollY, LoadId = Editor.LoadId };

        // The load the page was on when it applied the edit: another load since means it no longer shows the edit.
        private int appliedLoadId;

        public async Task<ApplyReply> ApplyInEditorAsync(ApplyCommand command, CancellationToken cancellationToken)
        {
            Editor.HoldEditorReports();
            appliedLoadId = Editor.LoadId;
            var reply = await Editor.ApplyDocumentEditAsync(command.OperationId, command.TargetRevision, command.BaseContentHash, command.Text, command.ScrollToChange, ApplyTimeoutMs);
            if (reply == null) throw new TimeoutException("The editor did not answer ApplyDocumentEdit.");
            switch (reply["outcome"]?.ToString())
            {
                case "conflict":
                    // Nothing was applied: whatever the reader typed is theirs, as usual.
                    Editor.ReleaseEditorReports(apply: true);
                    return new ApplyReply(ApplyOutcome.Conflict);
                case "applied":
                    var n = reply["normalization"];
                    if (n == null) return new ApplyReply(ApplyOutcome.Failed);
                    var normalization = new NormalizationInfo(
                        n["pendingNormalization"]?.ToString() ?? PendingNormalization.Unknown,
                        n["sourceHash"]?.ToString() ?? "",
                        n["normalizedHash"]?.Type == JTokenType.String ? n["normalizedHash"].ToString() : null,
                        n["reasons"] is JArray reasons ? reasons.Select(r => r.ToString()).ToList() : new System.Collections.Generic.List<string>(),
                        n["classifierVersion"]?.Value<int?>() ?? 0);
                    return new ApplyReply(ApplyOutcome.Applied, reply["sourceHash"]?.ToString(), normalization);
                default:
                    return new ApplyReply(ApplyOutcome.Failed);
            }
        }

        public bool EditorStillHoldsEdit() => !IsActive || Editor.LoadId == appliedLoadId;

        public async Task<string> RestoreAsync(object captured, CancellationToken cancellationToken)
        {
            var state = (State)captured;
            // The host never took the candidate in, so its own text is still the one before the edit. If another tab
            // has been brought up meanwhile, the page shows that one and this tab's snapshot is already right.
            if (!IsActive) { Editor.ReleaseEditorReports(apply: false); return DocumentText.ContentHash(tab.Markdown ?? ""); }
            Editor.ReleaseEditorReports(apply: false);
            var confirmed = await Editor.ReloadAsync(state.Text, state.Cursor, state.ScrollTop, ReloadTimeoutMs);
            return confirmed == null ? null : DocumentText.ContentHash(confirmed);
        }

        public void Commit(string text, long revision, string operationId)
        {
            if (IsActive)
            {
                Editor.CommitAutomationText(text);
            }
            else
            {
                if (!string.Equals(tab.Markdown, text, StringComparison.Ordinal)) tab.NoteTextChanged();
                tab.Markdown = text;
                tab.CurrentHash = Common.SimpleHash(text);
                tab.Saved = tab.FileHash == tab.CurrentHash;
                tab.IsDirty = !tab.Saved;
                tab.History?.ContentChange(text);
                tab.History?.CommitPending();
            }
            if (tab.Revision != revision)
                Log.Debug($"automation: {operationId} committed revision {tab.Revision}, expected {revision}");
            tab.IsPreview = false; // an edited tab is no longer a throwaway preview
        }

        public void EndEdit()
        {
            // Typing that arrived after the page took the edit becomes the next revision, on top of it - unless the tab
            // was left meanwhile: the editor then shows another document, and this one's typing must not land there.
            Editor.ReleaseEditorReports(apply: IsActive);
        }

        /// <summary>Only the active document is saved in this version; a background tab is focused first.</summary>
        public Task<bool> SaveAsync(CancellationToken cancellationToken) =>
            IsActive ? app.FileViewModel.SaveToExistingPathAsync() : Task.FromResult(false);
    }
}
