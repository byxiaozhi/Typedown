using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Typedown.Automation
{
    /// <summary>
    /// The document MVP methods (docs/automation-api-spec.md, section 3; params and results in
    /// docs/automation-schema/v1.json). Parameters are read and checked here, the work is done by the host.
    /// </summary>
    public static class DocumentMethods
    {
        /// <param name="onWrite">Called after each successful write with the client's display name and the document, so the
        /// application can show briefly who changed what (section 5.1).</param>
        public static MethodTable AddTo(MethodTable table, IAutomationHost host, string instanceId, Action<string, string>? onWrite = null)
        {
            table.Add(new MethodDescriptor("app.getState", Scopes.AppRead, "app.getState/1", async (c, ct) =>
            {
                var windows = await host.ListWindowsAsync(ct).ConfigureAwait(false);
                return new JObject
                {
                    ["instanceId"] = instanceId,
                    ["version"] = host.Version,
                    ["activeWindowId"] = windows.FirstOrDefault(w => w.Active)?.WindowId is string id ? (JToken)id : JValue.CreateNull(),
                    ["windowCount"] = windows.Count,
                };
            }));
            table.Add(new MethodDescriptor("window.list", Scopes.AppRead, "window.list/1", async (c, ct) =>
            {
                var windows = await host.ListWindowsAsync(ct).ConfigureAwait(false);
                return new JObject { ["windows"] = new JArray(windows.Select(Window)) };
            }));
            table.Add(new MethodDescriptor("window.focus", Scopes.WindowFocus, "window.focus/1", async (c, ct) =>
            {
                var windowId = c.Params.RequiredString("windowId", allowEmpty: false);
                await host.FocusWindowAsync(windowId, ct).ConfigureAwait(false);
                return new JObject { ["windowId"] = windowId };
            }));

            table.Add(new MethodDescriptor("document.list", Scopes.DocumentRead, "document.list/1", async (c, ct) =>
            {
                var documents = await host.ListDocumentsAsync(c.Params.OptionalString("windowId", allowEmpty: false), ct).ConfigureAwait(false);
                return new JObject { ["documents"] = new JArray(documents.Select(Summary)) };
            }));
            table.Add(new MethodDescriptor("document.get", Scopes.DocumentRead, "document.get/1", async (c, ct) =>
            {
                var p = c.Params;
                var documentId = p.RequiredString("documentId", allowEmpty: false);
                // No implicit consistency (section 3.2): the caller says which it wants.
                if (!p.Has("consistency")) throw Params.Invalid("consistency", "required");
                var latest = p.OptionalEnum("consistency", "snapshot", "snapshot", "latest") == "latest";
                var include = p.OptionalStringArray("include") ?? Array.Empty<string>();
                foreach (var item in include)
                    if (item != "text" && item != "headings") throw Params.Invalid("include", "unknownValue");
                var snapshot = await host.GetDocumentAsync(documentId, latest, ct).ConfigureAwait(false);
                var result = Summary(snapshot.Info);
                result["contentHash"] = DocumentText.ContentHash(snapshot.Text);
                result["isCurrent"] = latest || snapshot.IsCurrent;
                result["normalization"] = Normalization(snapshot.Normalization);
                if (include.Contains("text")) result["text"] = snapshot.Text;
                if (include.Contains("headings"))
                    result["headings"] = new JArray(Headings.Extract(snapshot.Text).Select(h => new JObject
                    {
                        ["index"] = h.Index, ["level"] = h.Level, ["text"] = h.Text, ["slug"] = h.Slug,
                    }));
                return result;
            }));
            table.Add(new MethodDescriptor("document.open", Scopes.DocumentWrite, "document.open/1", async (c, ct) =>
            {
                var p = c.Params;
                var path = p.RequiredString("path", allowEmpty: false);
                var info = await host.OpenDocumentAsync(path, p.OptionalString("windowId", allowEmpty: false), RevealOf(c, p), ct).ConfigureAwait(false);
                return new JObject { ["windowId"] = info.WindowId, ["documentId"] = info.DocumentId, ["revision"] = info.Revision };
            }));
            table.Add(new MethodDescriptor("document.create", Scopes.DocumentWrite, "document.create/1", async (c, ct) =>
            {
                var p = c.Params;
                var text = p.OptionalString("text");
                if (text != null) DocumentText.Validate("text", text);
                var allowUnknown = PolicyAllowsUnknown(p);
                var reveal = RevealOf(c, p);
                var info = await host.CreateDocumentAsync(p.OptionalString("windowId", allowEmpty: false), reveal, ct).ConfigureAwait(false);
                var result = new JObject { ["windowId"] = info.WindowId, ["documentId"] = info.DocumentId };
                if (string.IsNullOrEmpty(text))
                {
                    var snapshot = await host.GetDocumentAsync(info.DocumentId, false, ct).ConfigureAwait(false);
                    result["revision"] = snapshot.Info.Revision;
                    result["contentHash"] = DocumentText.ContentHash(snapshot.Text);
                    return result;
                }
                try
                {
                    var edit = await host.EditDocumentAsync(info.DocumentId,
                        new EditRequest { BaseRevision = info.Revision, Edit = _ => text!, AllowUnknown = allowUnknown }, reveal, ct).ConfigureAwait(false);
                    result["revision"] = edit.Revision;
                    result["contentHash"] = edit.ContentHash;
                    result["normalization"] = Normalization(edit.Normalization);
                    return result;
                }
                catch (AutomationException)
                {
                    // The new document could not take the text: it is not left behind, empty, for the caller to find.
                    try { await host.DiscardDocumentAsync(info.DocumentId, CancellationToken.None).ConfigureAwait(false); } catch { }
                    throw;
                }
            }));
            table.Add(new MethodDescriptor("document.focus", Scopes.WindowFocus, "document.focus/1", async (c, ct) =>
            {
                var info = await host.FocusDocumentAsync(c.Params.RequiredString("documentId", allowEmpty: false), ct).ConfigureAwait(false);
                return new JObject { ["windowId"] = info.WindowId, ["documentId"] = info.DocumentId };
            }));

            table.Add(new MethodDescriptor("document.replace", Scopes.DocumentWrite, "document.replace/1", (c, ct) =>
            {
                var p = c.Params;
                var text = p.RequiredString("text");
                DocumentText.Validate("text", text);
                return WriteAsync(c, p, _ => text, ct);
            }));
            table.Add(new MethodDescriptor("document.replaceText", Scopes.DocumentWrite, "document.replaceText/1", (c, ct) =>
            {
                var p = c.Params;
                var find = p.RequiredString("find", allowEmpty: false);
                var replacement = p.RequiredString("replacement");
                if (replacement.IndexOf('\r') >= 0) throw Params.Invalid("replacement", "carriageReturn");
                var expectedCount = p.RequiredInteger("expectedCount");
                return WriteAsync(c, p, current => DocumentText.ReplaceText(current, find, replacement, expectedCount), ct);
            }));
            table.Add(new MethodDescriptor("document.undo", Scopes.DocumentWrite, "document.undo/1", (c, ct) => UndoAsync(c, redo: false, ct)));
            table.Add(new MethodDescriptor("document.redo", Scopes.DocumentWrite, "document.redo/1", (c, ct) => UndoAsync(c, redo: true, ct)));
            table.Add(new MethodDescriptor("document.close", Scopes.DocumentWrite, "document.close/1", async (c, ct) =>
            {
                var p = c.Params;
                var info = await host.CloseDocumentAsync(p.RequiredString("documentId", allowEmpty: false), p.OptionalInteger("baseRevision"), ct).ConfigureAwait(false);
                return new JObject { ["documentId"] = info.DocumentId, ["windowId"] = info.WindowId, ["closed"] = true };
            }));
            table.Add(new MethodDescriptor("document.save", Scopes.DocumentSave, "document.save/1", async (c, ct) =>
            {
                var p = c.Params;
                var (info, hash) = await host.SaveDocumentAsync(p.RequiredString("documentId", allowEmpty: false), p.OptionalInteger("baseRevision"), ct).ConfigureAwait(false);
                return new JObject { ["revision"] = info.Revision, ["contentHash"] = hash, ["saved"] = true, ["path"] = info.Path };
            }));
            return table;

            async Task<JToken?> WriteAsync(MethodContext c, Params p, Func<string, string> edit, CancellationToken ct)
            {
                var documentId = p.RequiredString("documentId", allowEmpty: false);
                var request = new EditRequest
                {
                    BaseRevision = p.RequiredInteger("baseRevision"),
                    Edit = edit,
                    AllowUnknown = PolicyAllowsUnknown(p),
                    Save = p.OptionalBoolean("save", false),
                };
                if (request.Save && !c.Session.HasScope(Scopes.DocumentSave))
                    throw new AutomationException(AutomationErrorKind.scope_required, "Saving needs the 'document.save' scope.",
                        new System.Collections.Generic.Dictionary<string, object?> { ["scope"] = Scopes.DocumentSave });
                var (reveal, awaitPresentation) = RevealChecked(c, p);
                request.ScrollToChange = p.OptionalEnum("reveal", "none", RevealValues) == "change";
                p.OptionalString("clientOperationId");
                var result = await host.EditDocumentAsync(documentId, request, reveal, ct).ConfigureAwait(false);
                Wrote(c, documentId);
                return await Presented(documentId, WriteResult(result), awaitPresentation, ct).ConfigureAwait(false);
            }

            async Task<JToken?> UndoAsync(MethodContext c, bool redo, CancellationToken ct)
            {
                var p = c.Params;
                var save = p.OptionalBoolean("save", false);
                if (save && !c.Session.HasScope(Scopes.DocumentSave))
                    throw new AutomationException(AutomationErrorKind.scope_required, "Saving needs the 'document.save' scope.",
                        new System.Collections.Generic.Dictionary<string, object?> { ["scope"] = Scopes.DocumentSave });
                var documentId = p.RequiredString("documentId", allowEmpty: false);
                var (reveal, awaitPresentation) = RevealChecked(c, p);
                var result = await host.UndoAsync(documentId, p.RequiredInteger("baseRevision"), redo, save, reveal, ct).ConfigureAwait(false);
                Wrote(c, documentId);
                return await Presented(documentId, WriteResult(result), awaitPresentation, ct).ConfigureAwait(false);
            }

            // The write is committed whatever happens here: a page that does not draw in time is reported as
            // presentation_timeout carrying the committed revision, so the caller reads instead of repeating the write.
            async Task<JToken?> Presented(string documentId, JObject result, bool awaitPresentation, CancellationToken ct)
            {
                if (!awaitPresentation) return result;
                var presentation = await host.AwaitPresentationAsync(documentId, PresentationTimeoutMs, ct).ConfigureAwait(false);
                var observed = new JObject
                {
                    ["windowVisible"] = presentation.WindowVisible,
                    ["tabActive"] = presentation.TabActive,
                    ["pageFramesPassed"] = presentation.PageFramesPassed,
                    ["hostRenderPassed"] = presentation.HostRenderPassed,
                };
                if (!presentation.Complete)
                    throw new AutomationException(AutomationErrorKind.presentation_timeout, "The change was applied but was not confirmed on screen in time.",
                        new System.Collections.Generic.Dictionary<string, object?>
                        {
                            ["applied"] = true,
                            ["operationId"] = result["operationId"],
                            ["revision"] = result["revision"],
                            ["contentHash"] = result["contentHash"],
                            ["saved"] = result["saved"],
                            ["presentation"] = observed,
                        });
                result["presentation"] = observed;
                return result;
            }

            void Wrote(MethodContext c, string documentId)
            {
                try { onWrite?.Invoke(AutomationServer.DisplayName(c.Session.Client?.Name), documentId); } catch { }
            }
        }

        private static bool PolicyAllowsUnknown(Params p) =>
            p.OptionalEnum("normalizationPolicy", "requireKnownSafe", "requireKnownSafe", "allowUnknown") == "allowUnknown";

        // "change" (added in v1, 2026-10-02) shows the document as "document" does and, for a write, scrolls the first
        // changed block into view; for open, create without text, undo and redo it is the same as "document".
        private static readonly string[] RevealValues = { "none", "document", "change" };

        private static Reveal RevealOf(MethodContext c, Params p)
        {
            var value = p.OptionalEnum("reveal", "none", RevealValues);
            // "document" on open and create has never asked for window.focus, and v1 cannot start asking now; the
            // newer value asks for it, as section 1.3 says every reveal should.
            if (value == "change") RequireFocusScope(c);
            return value == "none" ? Reveal.None : Reveal.Document;
        }

        private static void RequireFocusScope(MethodContext c)
        {
            if (!c.Session.HasScope(Scopes.WindowFocus))
                throw new AutomationException(AutomationErrorKind.scope_required, "reveal needs the 'window.focus' scope.",
                    new System.Collections.Generic.Dictionary<string, object?> { ["scope"] = Scopes.WindowFocus });
        }

        // Showing a document to the person is a focus change: it needs the window.focus scope (section 1.3).
        private static (Reveal reveal, bool awaitPresentation) RevealChecked(MethodContext c, Params p)
        {
            var reveal = RevealOf(c, p);
            if (reveal == Reveal.Document) RequireFocusScope(c);
            var awaitPresentation = p.OptionalBoolean("awaitPresentation", false);
            if (awaitPresentation && reveal != Reveal.Document)
                throw Params.Invalid("awaitPresentation", "requiresReveal", "awaitPresentation needs reveal: \"document\" or \"change\".");
            return (reveal, awaitPresentation);
        }

        /// <summary>How long a write waits for the revealed document to draw before reporting presentation_timeout.</summary>
        public const int PresentationTimeoutMs = 3000;

        private static JObject Window(WindowInfo w) => new()
        {
            ["windowId"] = w.WindowId,
            ["active"] = w.Active,
            ["documentCount"] = w.DocumentCount,
            ["activeDocumentId"] = w.ActiveDocumentId is string id ? (JToken)id : JValue.CreateNull(),
        };

        private static JObject Summary(DocumentInfo d) => new()
        {
            ["documentId"] = d.DocumentId,
            ["windowId"] = d.WindowId,
            ["path"] = d.Path is string path ? (JToken)path : JValue.CreateNull(),
            ["title"] = d.Title,
            ["revision"] = d.Revision,
            ["saved"] = d.Saved,
            ["active"] = d.Active,
            ["lineEnding"] = d.LineEnding,
        };

        public static JObject Normalization(NormalizationInfo n) => new()
        {
            ["pendingNormalization"] = n.Pending,
            ["sourceHash"] = n.SourceHash,
            ["normalizedHash"] = n.NormalizedHash is string h ? (JToken)h : JValue.CreateNull(),
            ["reasons"] = new JArray(n.Reasons),
            ["classifierVersion"] = n.ClassifierVersion,
        };

        private static JObject WriteResult(EditResult r) => new()
        {
            ["operationId"] = r.OperationId,
            ["revision"] = r.Revision,
            ["contentHash"] = r.ContentHash,
            ["saved"] = r.Saved,
            ["origin"] = r.Origin,
            ["normalization"] = Normalization(r.Normalization),
        };
    }
}
