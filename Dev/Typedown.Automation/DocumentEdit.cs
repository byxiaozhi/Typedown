using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Typedown.Automation
{
    public static class PendingNormalization
    {
        public const string None = "none";
        public const string KnownFormatting = "knownFormatting";
        public const string Unknown = "unknown";
        public const string Unsafe = "unsafe";
    }

    /// <summary>What the first visual edit would do to the text (the <c>normalization</c> object of the spec).</summary>
    public sealed class NormalizationInfo
    {
        public NormalizationInfo(string pending, string sourceHash, string? normalizedHash, IReadOnlyList<string> reasons, int classifierVersion)
        {
            Pending = pending;
            SourceHash = sourceHash;
            NormalizedHash = normalizedHash;
            Reasons = reasons;
            ClassifierVersion = classifierVersion;
        }

        public string Pending { get; }
        public string SourceHash { get; }
        public string? NormalizedHash { get; }
        public IReadOnlyList<string> Reasons { get; }
        public int ClassifierVersion { get; }

        /// <summary>A document no editor has loaded yet: nothing is known about its normalization.</summary>
        public static NormalizationInfo NotEvaluated(string sourceHash, int classifierVersion) =>
            new(PendingNormalization.Unknown, sourceHash, null, new[] { "notEvaluated" }, classifierVersion);
    }

    public enum ApplyOutcome
    {
        /// <summary>The page applied the candidate and reports what it holds.</summary>
        Applied,
        /// <summary>The page's live text no longer matches baseContentHash (the reader typed): nothing was applied.</summary>
        Conflict,
        /// <summary>The page failed while applying; its state is unknown.</summary>
        Failed,
    }

    public sealed class ApplyReply
    {
        public ApplyReply(ApplyOutcome outcome, string? sourceHash = null, NormalizationInfo? normalization = null)
        {
            Outcome = outcome;
            SourceHash = sourceHash;
            Normalization = normalization;
        }

        public ApplyOutcome Outcome { get; }
        /// <summary>Hash of the exact source text the page now holds.</summary>
        public string? SourceHash { get; }
        public NormalizationInfo? Normalization { get; }
    }

    /// <summary>What the host hands the page for one edit (the page-side ApplyDocumentEdit message).</summary>
    public sealed class ApplyCommand
    {
        public ApplyCommand(string operationId, long targetRevision, string baseContentHash, string text, bool scrollToChange = false)
        {
            OperationId = operationId;
            TargetRevision = targetRevision;
            BaseContentHash = baseContentHash;
            Text = text;
            ScrollToChange = scrollToChange;
        }

        public string OperationId { get; }
        public long TargetRevision { get; }
        public string BaseContentHash { get; }
        public string Text { get; }
        /// <summary>See <see cref="EditRequest.ScrollToChange"/>.</summary>
        public bool ScrollToChange { get; }
    }

    /// <summary>
    /// One document as the edit coordinator needs it. The host implements it for a tab and runs the whole edit on the
    /// window's dispatcher (spec 2.2: writes run in the window's UI queue), so members may touch UI state directly.
    /// Text is always the authoritative host text ("\n" line endings).
    /// </summary>
    public interface IEditableDocument
    {
        string DocumentId { get; }
        long Revision { get; }
        string Text { get; }
        bool Saved { get; }
        /// <summary>Shown in its window's editor, as opposed to a background tab that only has a snapshot.</summary>
        bool IsActive { get; }

        /// <summary>Makes the host hold the page's latest text (flushing throttled reports); false on timeout.</summary>
        Task<bool> FlushAsync(CancellationToken cancellationToken);

        /// <summary>Text, caret and scroll before the edit: what a failed edit restores. Opaque to the coordinator.</summary>
        object CaptureState();

        /// <summary>Hands the candidate to the page. Throws <see cref="TimeoutException"/> when the page does not answer in time.</summary>
        Task<ApplyReply> ApplyInEditorAsync(ApplyCommand command, CancellationToken cancellationToken);

        /// <summary>
        /// After the page applied the edit: false when the window has since shown the document through another load
        /// (the web view reloaded, the tab was switched away and back), so the page no longer holds the candidate and
        /// committing would leave host and page apart. A tab switched away and not back still takes the commit into
        /// its snapshot.
        /// </summary>
        bool EditorStillHoldsEdit();

        /// <summary>
        /// Reloads the captured state through the normal LoadFile flow under a new load id and returns the hash of
        /// the text the page confirmed, or null when it did not confirm. Never changes the revision or the history.
        /// </summary>
        Task<string?> RestoreAsync(object state, CancellationToken cancellationToken);

        /// <summary>
        /// Commits in one step: the text, one undo step, the unsaved state, the revision, then derived state (title,
        /// outline, word count). For a background tab it updates the tab's snapshot.
        /// </summary>
        void Commit(string text, long revision, string operationId);

        /// <summary>Saves through the normal atomic save path; false when it failed.</summary>
        Task<bool> SaveAsync(CancellationToken cancellationToken);

        /// <summary>
        /// The committed edit (and its save, if asked for) is complete. Typing the reader did on top of the edit while
        /// it was in flight becomes the next revision only now, so a save made for the edit writes exactly the edit's
        /// revision and not text the reader has not saved.
        /// </summary>
        void EndEdit();
    }

    /// <summary>
    /// Points in the write path where a test build can hold an edit (docs/automation-api-analysis-plan.md, the test
    /// host). The application passes none; the automation test host passes its barriers.
    /// </summary>
    public interface IEditBarriers
    {
        /// <summary>Returns when the edit may go on past <paramref name="point"/>.</summary>
        Task PassAsync(string point, string documentId, string? operationId);
    }

    public static class EditBarrierPoints
    {
        /// <summary>The host has the page's flushed text and has not checked the revision yet.</summary>
        public const string BeforeFlushReply = "beforeFlushReply";
        /// <summary>The page has applied the candidate; the host has not committed it.</summary>
        public const string AfterEditorMutationBeforeReport = "afterEditorMutationBeforeReport";
        /// <summary>The edit is committed; the save has not started.</summary>
        public const string BeforeSaveCommit = "beforeSaveCommit";
    }

    public sealed class EditRequest
    {
        public long BaseRevision { get; set; }
        /// <summary>Computes the candidate from the latest text; may throw (e.g. <c>match_count_mismatch</c>).</summary>
        public Func<string, string> Edit { get; set; } = t => t;
        public bool AllowUnknown { get; set; }
        public bool Save { get; set; }
        /// <summary>
        /// <c>reveal: "change"</c>: once applied, the page scrolls the first changed block into view when it is off
        /// screen. The reader's cursor stays where it was.
        /// </summary>
        public bool ScrollToChange { get; set; }
    }

    public sealed class EditResult
    {
        public EditResult(string operationId, long revision, string contentHash, bool saved, NormalizationInfo normalization)
        {
            OperationId = operationId;
            Revision = revision;
            ContentHash = contentHash;
            Saved = saved;
            Normalization = normalization;
        }

        public string OperationId { get; }
        public long Revision { get; }
        public string ContentHash { get; }
        public bool Saved { get; }
        public NormalizationInfo Normalization { get; }
        public string Origin => "automation";
    }

    /// <summary>
    /// The write path of docs/automation-api-spec.md, section 2.2. Edits of one document run one at a time. Nothing
    /// is committed until the page confirms the candidate; any failure after the page was touched is undone by one
    /// path only - reloading the state captured before the edit - and a document whose reload also fails is
    /// quarantined (<c>editor_inconsistent</c>) and refuses further writes.
    /// </summary>
    public sealed class DocumentEditCoordinator
    {
        // No ConfigureAwait(false) in this class, on purpose: started on a window's UI thread, every step after an
        // await must resume there, because the document's members touch that window's state.
        private readonly Dictionary<string, SemaphoreSlim> locks = new(StringComparer.Ordinal);

        /// <summary>Where the coordinator says what it is waiting for (the platform's log); nothing when unset.</summary>
        public Action<string>? Diagnostics { get; set; }
        private readonly HashSet<string> quarantined = new(StringComparer.Ordinal);
        private readonly int classifierVersion;
        private readonly IEditBarriers? barriers;

        public DocumentEditCoordinator(int classifierVersion, IEditBarriers? barriers = null)
        {
            this.classifierVersion = classifierVersion;
            this.barriers = barriers;
        }

        private Task Barrier(string point, IEditableDocument document, string? operationId) =>
            barriers == null ? Task.CompletedTask : barriers.PassAsync(point, document.DocumentId, operationId);

        public bool IsQuarantined(string documentId)
        {
            lock (locks) return quarantined.Contains(documentId);
        }

        /// <summary>Forgets a closed document (its lock and quarantine).</summary>
        public void Forget(string documentId)
        {
            lock (locks)
            {
                locks.Remove(documentId);
                quarantined.Remove(documentId);
            }
        }

        private SemaphoreSlim LockFor(string documentId)
        {
            lock (locks)
            {
                if (!locks.TryGetValue(documentId, out var gate)) locks[documentId] = gate = new SemaphoreSlim(1, 1);
                return gate;
            }
        }

        private static AutomationException Error(AutomationErrorKind kind, string message, Dictionary<string, object?>? data = null) => new(kind, message, data);

        /// <summary>
        /// Runs work while no edit of the document is in flight, and keeps the next one waiting until it is done: a mode
        /// switch must not unload the editor while an edit is applied and not yet committed (spec 2.2).
        /// </summary>
        public async Task<T> ExclusiveAsync<T>(string documentId, Func<Task<T>> work, CancellationToken cancellationToken)
        {
            var gate = LockFor(documentId);
            // An operation that waits here long is behind another one on the same document: worth a line in the log.
            if (!await gate.WaitAsync(5000, cancellationToken))
            {
                Diagnostics?.Invoke($"document {documentId}: waiting for another operation on it");
                await gate.WaitAsync(cancellationToken);
            }
            try { return await work(); }
            finally { gate.Release(); }
        }

        public async Task<EditResult> EditAsync(IEditableDocument document, EditRequest request, CancellationToken cancellationToken)
        {
            var gate = LockFor(document.DocumentId);
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await EditLockedAsync(document, request, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<EditResult> EditLockedAsync(IEditableDocument document, EditRequest request, CancellationToken cancellationToken)
        {
            // 1. The document must still be writable.
            if (IsQuarantined(document.DocumentId))
                throw Error(AutomationErrorKind.editor_inconsistent, "The document's editor could not be restored after a failed edit; it refuses writes until reopened.");

            // 2. The active document's latest text first; a stale snapshot must never be overwritten.
            if (document.IsActive && !await document.FlushAsync(cancellationToken))
                throw Error(AutomationErrorKind.content_sync_timeout, "Could not confirm the editor's latest text.");
            await Barrier(EditBarrierPoints.BeforeFlushReply, document, null);

            // 3. Revision check against the flushed state.
            if (document.Revision != request.BaseRevision)
                throw Error(AutomationErrorKind.revision_conflict, "The document changed since baseRevision.",
                    new Dictionary<string, object?> { ["revision"] = document.Revision });

            // Past this point cancellation is not honoured: an edit either commits or is rolled back, never left half way.
            var baseText = document.Text;
            var candidate = request.Edit(baseText);
            DocumentText.Validate("text", candidate);
            var baseHash = DocumentText.ContentHash(baseText);
            var candidateHash = DocumentText.ContentHash(candidate);
            var operationId = Guid.NewGuid().ToString("N");

            if (candidate == baseText)
            {
                // Nothing to change: the revision stays, and no undo step is made.
                return new EditResult(operationId, document.Revision, baseHash, document.Saved,
                    new NormalizationInfo(PendingNormalization.None, baseHash, baseHash, Array.Empty<string>(), classifierVersion));
            }

            // 4. Candidate revision; the pre-edit state is kept for the one rollback path.
            var targetRevision = document.Revision + 1;
            NormalizationInfo normalization;
            if (!document.IsActive)
            {
                // A background tab has no editor to classify the candidate.
                normalization = NormalizationInfo.NotEvaluated(candidateHash, classifierVersion);
                if (!request.AllowUnknown) throw Unclassified(normalization);
            }
            else
            {
                var state = document.CaptureState();
                ApplyReply reply;
                AutomationException? failure = null;
                try
                {
                    // 5-7. The page compares its live text with baseContentHash, applies, and reports.
                    reply = await document.ApplyInEditorAsync(new ApplyCommand(operationId, targetRevision, baseHash, candidate, request.ScrollToChange), CancellationToken.None);
                    if (reply.Outcome == ApplyOutcome.Applied) await Barrier(EditBarrierPoints.AfterEditorMutationBeforeReport, document, operationId);
                }
                catch (Exception e) when (e is TimeoutException || e is OperationCanceledException)
                {
                    reply = new ApplyReply(ApplyOutcome.Failed);
                    failure = Error(AutomationErrorKind.editor_not_ready, "The editor did not confirm the edit in time; the document was restored.");
                }

                if (reply.Outcome == ApplyOutcome.Conflict)
                    // Detected before anything was applied: nothing to restore.
                    throw Error(AutomationErrorKind.revision_conflict, "The document was edited in the window meanwhile.",
                        new Dictionary<string, object?> { ["revision"] = document.Revision });

                if (failure == null)
                {
                    if (reply.Outcome != ApplyOutcome.Applied || reply.Normalization == null)
                        failure = Error(AutomationErrorKind.editor_not_ready, "The editor failed to apply the edit; the document was restored.");
                    else if (reply.SourceHash != candidateHash)
                        failure = Error(AutomationErrorKind.editor_not_ready, "The editor holds other text than was sent; the document was restored.");
                    else if (reply.Normalization.Pending == PendingNormalization.Unsafe)
                        failure = Error(AutomationErrorKind.content_not_roundtrippable, "The first visual edit would lose content from this text; the document was restored.",
                            NormalizationData(reply.Normalization));
                    else if (reply.Normalization.Pending == PendingNormalization.Unknown && !request.AllowUnknown)
                        failure = Unclassified(reply.Normalization);
                    else if (!document.EditorStillHoldsEdit())
                        failure = Error(AutomationErrorKind.editor_not_ready, "The editor was reloaded before the edit was committed; the document was restored.",
                            new Dictionary<string, object?> { ["reason"] = "editorReloaded" });
                }

                if (failure != null)
                {
                    // 8-9. One recovery path: reload what was there before, and check it came back exactly.
                    string? restored = null;
                    try { restored = await document.RestoreAsync(state, CancellationToken.None); }
                    catch (Exception) { }
                    if (restored != baseHash)
                    {
                        lock (locks) quarantined.Add(document.DocumentId);
                        throw Error(AutomationErrorKind.editor_inconsistent, "The edit failed and the editor could not be restored; the document refuses writes until reopened.",
                            new Dictionary<string, object?> { ["cause"] = failure.Kind.ToString() });
                    }
                    throw failure;
                }
                normalization = reply.Normalization!;
            }

            // 10. Commit once, then derived state.
            document.Commit(candidate, targetRevision, operationId);

            // 11. Save when asked; the edit stays committed if the save fails.
            try
            {
                var saved = document.Saved;
                if (request.Save)
                {
                    await Barrier(EditBarrierPoints.BeforeSaveCommit, document, operationId);
                    if (!await document.SaveAsync(CancellationToken.None))
                        throw Error(AutomationErrorKind.save_failed, "The edit was applied but saving failed; the document stays unsaved.",
                            new Dictionary<string, object?> { ["applied"] = true, ["revision"] = targetRevision });
                    saved = true;
                }
                return new EditResult(operationId, targetRevision, candidateHash, saved, normalization);
            }
            finally
            {
                document.EndEdit();
            }
        }

        private static Dictionary<string, object?> NormalizationData(NormalizationInfo n) => new()
        {
            ["pendingNormalization"] = n.Pending,
            ["sourceHash"] = n.SourceHash,
            ["normalizedHash"] = n.NormalizedHash,
            ["reasons"] = n.Reasons,
            ["classifierVersion"] = n.ClassifierVersion,
        };

        private static AutomationException Unclassified(NormalizationInfo n) =>
            new(AutomationErrorKind.normalization_unclassified,
                "The text differs from what the editor would write back and could not be shown safe; nothing was changed. Retry with normalizationPolicy allowUnknown to accept it.",
                NormalizationData(n));
    }
}
