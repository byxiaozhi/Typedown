using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Typedown.Automation
{
    public sealed class WindowInfo
    {
        public string WindowId { get; set; } = "";
        public bool Active { get; set; }
        public int DocumentCount { get; set; }
        public string? ActiveDocumentId { get; set; }
    }

    public sealed class DocumentInfo
    {
        public string DocumentId { get; set; } = "";
        public string WindowId { get; set; } = "";
        /// <summary>Absolute path; null for an untitled document.</summary>
        public string? Path { get; set; }
        public string Title { get; set; } = "";
        public long Revision { get; set; }
        public bool Saved { get; set; }
        /// <summary>The active tab of its window.</summary>
        public bool Active { get; set; }
        /// <summary><c>lf</c>, <c>crlf</c> or <c>cr</c>: how the file ends its lines on disk.</summary>
        public string LineEnding { get; set; } = "lf";
    }

    public sealed class DocumentSnapshot
    {
        public DocumentInfo Info { get; set; } = new();
        public string Text { get; set; } = "";
        /// <summary>False when a snapshot read could not include the editor's unreported typing.</summary>
        public bool IsCurrent { get; set; }
        public NormalizationInfo Normalization { get; set; } = NormalizationInfo.NotEvaluated("", 0);
    }

    public enum Reveal { None, Document }

    /// <summary>
    /// An approximate observation that a revealed document reached the screen (section 2.3): it cannot prove the window
    /// is not covered or that anyone saw the change.
    /// </summary>
    public sealed class Presentation
    {
        public bool WindowVisible { get; set; }
        public bool TabActive { get; set; }
        /// <summary>The page drew two animation frames after the write.</summary>
        public bool PageFramesPassed { get; set; }
        /// <summary>The window's dispatcher had one chance to render after the write.</summary>
        public bool HostRenderPassed { get; set; }

        public bool Complete => WindowVisible && TabActive && PageFramesPassed && HostRenderPassed;
    }

    /// <summary>
    /// What the document methods need from the application (Windows or Uno). Implementations map closed or unknown
    /// ids to <c>window_not_found</c> / <c>document_not_found</c>, run each call on the owning window's dispatcher,
    /// and never show a dialog.
    /// </summary>
    public interface IAutomationHost
    {
        string Version { get; }
        int ClassifierVersion { get; }

        Task<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken);
        Task FocusWindowAsync(string windowId, CancellationToken cancellationToken);

        Task<IReadOnlyList<DocumentInfo>> ListDocumentsAsync(string? windowId, CancellationToken cancellationToken);

        /// <summary><paramref name="latest"/>: the active document's editor is flushed first (<c>content_sync_timeout</c> when it does not answer).</summary>
        Task<DocumentSnapshot> GetDocumentAsync(string documentId, bool latest, CancellationToken cancellationToken);

        /// <summary>Opens through the normal open flow (an already open file is focused), returning the document.</summary>
        Task<DocumentInfo> OpenDocumentAsync(string path, string? windowId, Reveal reveal, CancellationToken cancellationToken);

        /// <summary>A new untitled, empty document.</summary>
        Task<DocumentInfo> CreateDocumentAsync(string? windowId, Reveal reveal, CancellationToken cancellationToken);

        Task<DocumentInfo> FocusDocumentAsync(string documentId, CancellationToken cancellationToken);

        /// <summary>Runs one edit through <see cref="DocumentEditCoordinator"/> on the document's window.</summary>
        Task<EditResult> EditDocumentAsync(string documentId, EditRequest request, Reveal reveal, CancellationToken cancellationToken);

        /// <summary>Saves to the existing path (<c>path_required</c> for an untitled document), after an optional revision check.</summary>
        Task<(DocumentInfo info, string contentHash)> SaveDocumentAsync(string documentId, long? baseRevision, CancellationToken cancellationToken);

        /// <summary>One step back (or forward) in the document's own history, after the revision check.</summary>
        Task<EditResult> UndoAsync(string documentId, long baseRevision, bool redo, bool save, Reveal reveal, CancellationToken cancellationToken);

        /// <summary>
        /// After a write with <c>reveal: "document"</c>: waits up to <paramref name="timeoutMs"/> for the window and the
        /// page to draw, and reports what was observed. It never fails for a slow page; the caller decides.
        /// </summary>
        Task<Presentation> AwaitPresentationAsync(string documentId, int timeoutMs, CancellationToken cancellationToken);

        /// <summary>Closes a document created by a failed <c>document.create</c>; nothing else uses it.</summary>
        Task DiscardDocumentAsync(string documentId, CancellationToken cancellationToken);

        /// <summary>
        /// document.close: closes a saved document's tab, after the editor's latest text is in. Throws
        /// <c>unsaved_changes</c> (with the revision) for a document with unsaved changes - nothing is discarded and no
        /// dialog is shown - and <c>revision_conflict</c> when <paramref name="baseRevision"/> is given and not current.
        /// The window's only document is replaced by a new empty one: the window stays. Returns the closed document.
        /// </summary>
        Task<DocumentInfo> CloseDocumentAsync(string documentId, long? baseRevision, CancellationToken cancellationToken);
    }
}
