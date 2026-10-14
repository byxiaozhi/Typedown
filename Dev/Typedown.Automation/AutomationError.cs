using System;
using System.Collections.Generic;

namespace Typedown.Automation
{
    /// <summary>The stable error codes of the automation API (docs/automation-api-spec.md, section 4).</summary>
    public enum AutomationErrorKind
    {
        not_initialized = -32001,
        unsupported_version = -32002,
        scope_required = -32003,
        method_not_found = -32601,
        invalid_params = -32602,
        window_not_found = -32010,
        document_not_found = -32011,
        revision_conflict = -32012,
        editor_not_ready = -32013,
        content_sync_timeout = -32014,
        read_only = -32015,
        path_required = -32016,
        save_failed = -32017,
        content_not_roundtrippable = -32018,
        match_count_mismatch = -32019,
        setting_not_exposed = -32020,
        setting_invalid = -32021,
        persistence_failed = -32022,
        presentation_timeout = -32023,
        editor_inconsistent = -32024,
        normalization_unclassified = -32025,
        /// <summary>The document has changes that are not saved: document.close refuses rather than discard them.</summary>
        unsaved_changes = -32026,
        message_too_large = -32030,
        busy = -32031,
        request_cancelled = -32032,
    }

    /// <summary>
    /// A request that failed in a way the client is told about: the JSON-RPC error code is the kind's value and
    /// <c>error.data.kind</c> its name. <see cref="ErrorData"/> may carry revisions, counts or hashes, never document
    /// text, credentials or stack traces.
    /// </summary>
    public sealed class AutomationException : Exception
    {
        public AutomationErrorKind Kind { get; }

        public IReadOnlyDictionary<string, object?> ErrorData { get; }

        public int Code => (int)Kind;

        public AutomationException(AutomationErrorKind kind, string message, IReadOnlyDictionary<string, object?>? data = null)
            : base(message)
        {
            Kind = kind;
            ErrorData = data ?? new Dictionary<string, object?>();
        }
    }
}
