using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Typedown.Automation.Tests
{
    /// <summary>One window of fake documents behind the real edit coordinator.</summary>
    internal sealed class FakeHost : IAutomationHost
    {
        public const string WindowId = "w0123456789abcdef0123456789abcdef";
        public readonly DocumentEditCoordinator Coordinator = new(3);
        public readonly List<(FakeDocument doc, string? path)> Docs = new();
        public readonly List<string> Discarded = new();
        public FakeDocument? Active;
        public Func<ApplyCommand, ApplyReply>? NewDocumentPage;

        public string Version => "1.2.30";
        public int ClassifierVersion => 3;

        public FakeDocument Add(string text, string? path = null, bool active = true)
        {
            var doc = new FakeDocument { Text = text, PageText = text, IsActive = active };
            Docs.Add((doc, path));
            if (active) { if (Active != null) Active.IsActive = false; Active = doc; }
            return doc;
        }

        private (FakeDocument doc, string? path) Find(string id) =>
            Docs.FirstOrDefault(d => d.doc.DocumentId == id) is var found && found.doc != null ? found
                : throw new AutomationException(AutomationErrorKind.document_not_found, "no such document", new Dictionary<string, object?> { ["documentId"] = id });

        private DocumentInfo Info((FakeDocument doc, string? path) d) => new()
        {
            DocumentId = d.doc.DocumentId, WindowId = WindowId, Path = d.path, Title = d.path == null ? "Untitled" : System.IO.Path.GetFileName(d.path),
            Revision = d.doc.Revision, Saved = d.doc.Saved, Active = d.doc.IsActive, LineEnding = "crlf",
        };

        public Task<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<WindowInfo>>(new[] { new WindowInfo { WindowId = WindowId, Active = true, DocumentCount = Docs.Count, ActiveDocumentId = Active?.DocumentId } });

        public Task FocusWindowAsync(string windowId, CancellationToken ct) =>
            windowId == WindowId ? Task.CompletedTask : Task.FromException(new AutomationException(AutomationErrorKind.window_not_found, "no window"));

        public Task<IReadOnlyList<DocumentInfo>> ListDocumentsAsync(string? windowId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DocumentInfo>>(Docs.Select(Info).ToList());

        public async Task<DocumentSnapshot> GetDocumentAsync(string documentId, bool latest, CancellationToken ct)
        {
            var d = Find(documentId);
            if (latest && d.doc.IsActive && !await d.doc.FlushAsync(ct)) throw new AutomationException(AutomationErrorKind.content_sync_timeout, "no answer");
            var hash = DocumentText.ContentHash(d.doc.Text);
            return new DocumentSnapshot { Info = Info(d), Text = d.doc.Text, IsCurrent = !d.doc.IsActive || d.doc.PageText == d.doc.Text, Normalization = NormalizationInfo.NotEvaluated(hash, 3) };
        }

        /// <summary>The reveal each open, create and edit was given.</summary>
        public readonly List<Reveal> Reveals = new();

        public Task<DocumentInfo> OpenDocumentAsync(string path, string? windowId, Reveal reveal, CancellationToken ct)
        {
            Reveals.Add(reveal);
            if (!path.EndsWith(".md")) throw new AutomationException(AutomationErrorKind.invalid_params, "not found", new Dictionary<string, object?> { ["field"] = "path", ["reason"] = "notFound" });
            var existing = Docs.FirstOrDefault(d => d.path == path);
            return Task.FromResult(Info(existing.doc != null ? existing : (Add("# opened\n", path), path)));
        }

        public Task<DocumentInfo> CreateDocumentAsync(string? windowId, Reveal reveal, CancellationToken ct)
        {
            var doc = Add("");
            doc.Page = NewDocumentPage;
            return Task.FromResult(Info((doc, null)));
        }

        public Task<DocumentInfo> FocusDocumentAsync(string documentId, CancellationToken ct) => Task.FromResult(Info(Find(documentId)));

        public Task<EditResult> EditDocumentAsync(string documentId, EditRequest request, Reveal reveal, CancellationToken ct)
        {
            Reveals.Add(reveal);
            return Coordinator.EditAsync(Find(documentId).doc, request, ct);
        }

        public async Task<(DocumentInfo info, string contentHash)> SaveDocumentAsync(string documentId, long? baseRevision, CancellationToken ct)
        {
            var d = Find(documentId);
            if (d.path == null) throw new AutomationException(AutomationErrorKind.path_required, "untitled");
            if (baseRevision != null && baseRevision != d.doc.Revision) throw new AutomationException(AutomationErrorKind.revision_conflict, "stale", new Dictionary<string, object?> { ["revision"] = d.doc.Revision });
            await d.doc.SaveAsync(ct);
            return (Info(d), DocumentText.ContentHash(d.doc.Text));
        }

        public Task<EditResult> UndoAsync(string documentId, long baseRevision, bool redo, bool save, Reveal reveal, CancellationToken ct) =>
            throw new AutomationException(AutomationErrorKind.editor_not_ready, "not in the fake");

        public Presentation NextPresentation = new() { WindowVisible = true, TabActive = true, PageFramesPassed = true, HostRenderPassed = true };
        public int PresentationWaits;

        public Task<Presentation> AwaitPresentationAsync(string documentId, int timeoutMs, CancellationToken ct)
        {
            Find(documentId);
            PresentationWaits++;
            return Task.FromResult(NextPresentation);
        }

        public Task DiscardDocumentAsync(string documentId, CancellationToken ct)
        {
            Discarded.Add(documentId);
            Docs.RemoveAll(d => d.doc.DocumentId == documentId);
            return Task.CompletedTask;
        }

        public async Task<DocumentInfo> CloseDocumentAsync(string documentId, long? baseRevision, CancellationToken ct)
        {
            var d = Find(documentId);
            if (d.doc.IsActive && !await d.doc.FlushAsync(ct)) throw new AutomationException(AutomationErrorKind.content_sync_timeout, "no answer");
            if (baseRevision != null && baseRevision != d.doc.Revision)
                throw new AutomationException(AutomationErrorKind.revision_conflict, "moved on", new Dictionary<string, object?> { ["revision"] = d.doc.Revision });
            if (!d.doc.Saved)
                throw new AutomationException(AutomationErrorKind.unsaved_changes, "unsaved", new Dictionary<string, object?> { ["revision"] = d.doc.Revision });
            var info = Info(d);
            Docs.Remove(d);
            if (Docs.Count == 0) Add("", null);   // the window keeps a document
            else if (d.doc == Active) { Active = Docs[0].doc; Active.IsActive = true; }
            return info;
        }
    }

    public class DocumentMethodsTests
    {
        private static (Harness h, FakeHost host) Start()
        {
            var host = new FakeHost();
            var table = DocumentMethods.AddTo(new MethodTable(BuildTypes.Application), host, "0123456789abcdef0123456789abcdef");
            return (new Harness(table), host);
        }

        private static void Valid(string def, JToken? instance)
        {
            var r = SchemaTests.EvaluateFor(def, instance);
            Assert.True(r.valid, $"{def}: {r.errors}\n{instance}");
        }

        private static string Kind(JObject reply) => (string)reply["error"]!["data"]!["kind"]!;

        private static readonly string[] AllScopes = { Scopes.AppRead, Scopes.DocumentRead, Scopes.DocumentWrite, Scopes.DocumentSave, Scopes.WindowFocus };

        [Fact]
        public async Task Lists_and_reads_match_the_schema()
        {
            var (h, host) = Start();
            await using var _ = h;
            var doc = host.Add("# One\n\ntext\n\n## Two\n", "/tmp/a.md");
            await h.InitializeAsync(AllScopes);

            var state = await h.CallAsync("app.getState");
            Valid("app.getState.result", state["result"]);
            Valid("window.list.result", (await h.CallAsync("window.list"))["result"]);
            var list = await h.CallAsync("document.list");
            Valid("document.list.result", list["result"]);
            Assert.Equal(doc.DocumentId, (string)list["result"]!["documents"]![0]!["documentId"]!);

            var get = await h.CallAsync("document.get", new JObject { ["documentId"] = doc.DocumentId, ["consistency"] = "latest", ["include"] = new JArray("text", "headings") });
            Valid("document.get.result", get["result"]);
            Assert.Equal("# One\n\ntext\n\n## Two\n", (string)get["result"]!["text"]!);
            Assert.Equal(new[] { "one", "two" }, get["result"]!["headings"]!.Select(x => (string)x["slug"]!));
            Assert.Equal(DocumentText.ContentHash("# One\n\ntext\n\n## Two\n"), (string)get["result"]!["contentHash"]!);

            var noConsistency = await h.CallAsync("document.get", new JObject { ["documentId"] = doc.DocumentId });
            Assert.Equal("consistency", (string)noConsistency["error"]!["data"]!["field"]!);
            Assert.Equal("document_not_found", Kind(await h.CallAsync("document.get", new JObject { ["documentId"] = "0000", ["consistency"] = "snapshot" })));
        }

        [Fact]
        public async Task Close_takes_a_saved_document_away_and_refuses_an_unsaved_one()
        {
            var (h, host) = Start();
            await using var _ = h;
            var a = host.Add("# A\n", "/tmp/a.md", active: false);
            var b = host.Add("# B\n", "/tmp/b.md");
            await h.InitializeAsync(Scopes.DocumentRead);
            Assert.Equal("document.write", (string)(await h.CallAsync("document.close", new JObject { ["documentId"] = a.DocumentId }))["error"]!["data"]!["scope"]!);
            await h.DisposeAsync();

            (h, host) = Start();
            await using var _2 = h;
            a = host.Add("# A\n", "/tmp/a.md", active: false);
            b = host.Add("# B\n", "/tmp/b.md");
            await h.InitializeAsync(AllScopes);

            // Unsaved: refused with the revision, nothing closed.
            b.Saved = false; b.Revision = 4;
            var refused = await h.CallAsync("document.close", new JObject { ["documentId"] = b.DocumentId });
            Assert.Equal("unsaved_changes", Kind(refused));
            Assert.Equal(-32026, (int)refused["error"]!["code"]!);
            Assert.Equal(4, (int)refused["error"]!["data"]!["revision"]!);
            Assert.Equal(2, host.Docs.Count);

            // Typing the page has not reported yet counts: the page is asked first (flush) - here it has none.
            b.Saved = true;
            Assert.Equal("revision_conflict", Kind(await h.CallAsync("document.close", new JObject { ["documentId"] = b.DocumentId, ["baseRevision"] = 3 })));
            var closed = await h.CallAsync("document.close", new JObject { ["documentId"] = b.DocumentId, ["baseRevision"] = 4 });
            Valid("document.close.result", closed["result"]);
            Assert.True((bool)closed["result"]!["closed"]!);
            Assert.Equal(new[] { a.DocumentId }, host.Docs.Select(d => d.doc.DocumentId));
            Assert.Equal("document_not_found", Kind(await h.CallAsync("document.close", new JObject { ["documentId"] = b.DocumentId })));

            // The window's last document: it goes, and an empty one takes its place.
            await h.CallAsync("document.close", new JObject { ["documentId"] = a.DocumentId });
            Assert.Single(host.Docs);
            Assert.Null(host.Docs[0].path);
            Assert.Equal("invalid_params", Kind(await h.CallAsync("document.close", new JObject())));
        }

        [Fact]
        public async Task A_snapshot_read_says_when_the_editor_holds_unreported_typing()
        {
            var (h, host) = Start();
            await using var _ = h;
            var doc = host.Add("a\n");
            doc.PageText = "a typed\n";
            await h.InitializeAsync(AllScopes);
            var snapshot = await h.CallAsync("document.get", new JObject { ["documentId"] = doc.DocumentId, ["consistency"] = "snapshot", ["include"] = new JArray("text") });
            Assert.False((bool)snapshot["result"]!["isCurrent"]!);
            Assert.Equal("a\n", (string)snapshot["result"]!["text"]!);
            var latest = await h.CallAsync("document.get", new JObject { ["documentId"] = doc.DocumentId, ["consistency"] = "latest", ["include"] = new JArray("text") });
            Assert.True((bool)latest["result"]!["isCurrent"]!);
            Assert.Equal("a typed\n", (string)latest["result"]!["text"]!);
            Assert.Equal(1, (int)latest["result"]!["revision"]!);
        }

        [Fact]
        public async Task Replace_needs_the_current_revision_and_advances_it()
        {
            var (h, host) = Start();
            await using var _ = h;
            var doc = host.Add("old\n", "/tmp/a.md");
            await h.InitializeAsync(AllScopes);
            var ok = await h.CallAsync("document.replace", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 0, ["text"] = "new\n", ["clientOperationId"] = "step-1" });
            Valid("document.replace.result", ok["result"]);
            Assert.Equal(1, (int)ok["result"]!["revision"]!);
            Assert.Equal("automation", (string)ok["result"]!["origin"]!);

            var stale = await h.CallAsync("document.replace", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 0, ["text"] = "again\n" });
            Assert.Equal(-32012, (int)stale["error"]!["code"]!);
            Assert.Equal(1, (int)stale["error"]!["data"]!["revision"]!);
            Assert.Equal("carriageReturn", (string)(await h.CallAsync("document.replace", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 1, ["text"] = "a\r\n" }))["error"]!["data"]!["reason"]!);
            Assert.Equal("baseRevision", (string)(await h.CallAsync("document.replace", new JObject { ["documentId"] = doc.DocumentId, ["text"] = "x" }))["error"]!["data"]!["field"]!);
            Assert.Equal("new\n", doc.Text);
        }

        [Fact]
        public async Task Unknown_normalization_needs_allowUnknown_and_unsafe_is_refused()
        {
            var (h, host) = Start();
            await using var _ = h;
            var doc = host.Add("a\n");
            doc.Page = c => FakeDocument.Classified(c, c.Text.Contains("{") ? PendingNormalization.Unsafe : PendingNormalization.Unknown, "formatting-changed");
            await h.InitializeAsync(AllScopes);
            var refused = await h.CallAsync("document.replace", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 0, ["text"] = "* b\n" });
            Assert.Equal(-32025, (int)refused["error"]!["code"]!);
            Valid("response", refused);
            var accepted = await h.CallAsync("document.replace", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 0, ["text"] = "* b\n", ["normalizationPolicy"] = "allowUnknown" });
            Assert.Equal("unknown", (string)accepted["result"]!["normalization"]!["pendingNormalization"]!);
            var unsafeWrite = await h.CallAsync("document.replace", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 1, ["text"] = "```ts {x}\n```\n", ["normalizationPolicy"] = "allowUnknown" });
            Assert.Equal(-32018, (int)unsafeWrite["error"]!["code"]!);
            Assert.Equal("unknownValue", (string)(await h.CallAsync("document.replace", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 1, ["text"] = "x\n", ["normalizationPolicy"] = "trustMe" }))["error"]!["data"]!["reason"]!);
        }

        [Fact]
        public async Task ReplaceText_counts_before_it_changes_anything()
        {
            var (h, host) = Start();
            await using var _ = h;
            var doc = host.Add("a b a\n");
            await h.InitializeAsync(AllScopes);
            var mismatch = await h.CallAsync("document.replaceText", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 0, ["find"] = "a", ["replacement"] = "c", ["expectedCount"] = 1 });
            Assert.Equal(-32019, (int)mismatch["error"]!["code"]!);
            Assert.Equal(2, (int)mismatch["error"]!["data"]!["actualCount"]!);
            var ok = await h.CallAsync("document.replaceText", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 0, ["find"] = "a", ["replacement"] = "c", ["expectedCount"] = 2 });
            Valid("document.replaceText.result", ok["result"]);
            Assert.Equal("c b c\n", doc.Text);
        }

        [Fact]
        public async Task Saving_and_showing_need_their_own_scopes()
        {
            var (h, host) = Start();
            await using var _ = h;
            var doc = host.Add("a\n", "/tmp/a.md");
            var untitled = host.Add("u\n", null, active: false);
            await h.InitializeAsync(Scopes.DocumentRead, Scopes.DocumentWrite);
            Assert.Equal("document.save", (string)(await h.CallAsync("document.replace", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 0, ["text"] = "b\n", ["save"] = true }))["error"]!["data"]!["scope"]!);
            Assert.Equal("window.focus", (string)(await h.CallAsync("document.replace", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 0, ["text"] = "b\n", ["reveal"] = "document" }))["error"]!["data"]!["scope"]!);
            Assert.Equal("scope_required", Kind(await h.CallAsync("document.save", new JObject { ["documentId"] = doc.DocumentId })));
            Assert.Equal("a\n", doc.Text);

            var (h2, host2) = Start();
            await using var __ = h2;
            var named = host2.Add("a\n", "/tmp/b.md");
            var blank = host2.Add("u\n", null, active: false);
            await h2.InitializeAsync(AllScopes);
            var saved = await h2.CallAsync("document.replace", new JObject { ["documentId"] = named.DocumentId, ["baseRevision"] = 0, ["text"] = "b\n", ["save"] = true });
            Assert.True((bool)saved["result"]!["saved"]!);
            Valid("document.save.result", (await h2.CallAsync("document.save", new JObject { ["documentId"] = named.DocumentId, ["baseRevision"] = 1 }))["result"]);
            Assert.Equal(-32016, (int)(await h2.CallAsync("document.save", new JObject { ["documentId"] = blank.DocumentId }))["error"]!["code"]!);
        }

        [Fact]
        public async Task Reveal_change_shows_the_document_and_asks_the_page_to_scroll_to_the_change()
        {
            var (h, host) = Start();
            await using var _ = h;
            var doc = host.Add("a\nb\n", "/tmp/c.md");
            await h.InitializeAsync(Scopes.DocumentRead, Scopes.DocumentWrite);
            // The newer value asks for window.focus everywhere; "document" on open keeps not asking (v1 cannot start now).
            Assert.Equal("window.focus", (string)(await h.CallAsync("document.replaceText", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 0, ["find"] = "b", ["replacement"] = "B", ["expectedCount"] = 1, ["reveal"] = "change" }))["error"]!["data"]!["scope"]!);
            Assert.Equal("window.focus", (string)(await h.CallAsync("document.open", new JObject { ["path"] = "/tmp/c.md", ["reveal"] = "change" }))["error"]!["data"]!["scope"]!);
            Assert.NotNull((await h.CallAsync("document.open", new JObject { ["path"] = "/tmp/c.md", ["reveal"] = "document" }))["result"]);
            Assert.Equal(0, doc.Revision);

            var (h2, host2) = Start();
            await using var __ = h2;
            var shown = host2.Add("a\nb\n", "/tmp/d.md");
            await h2.InitializeAsync(AllScopes);
            async Task<JObject> Replace(string find, string replacement, object? reveal, bool awaitShown = false)
            {
                var p = new JObject { ["documentId"] = shown.DocumentId, ["baseRevision"] = shown.Revision, ["find"] = find, ["replacement"] = replacement, ["expectedCount"] = 1 };
                if (reveal != null) p["reveal"] = JToken.FromObject(reveal);
                if (awaitShown) p["awaitPresentation"] = true;
                return await h2.CallAsync("document.replaceText", p);
            }
            var changed = await Replace("b", "B", "change", awaitShown: true);
            Valid("document.replaceText.result", changed["result"]);
            Assert.True((bool)changed["result"]!["presentation"]!["pageFramesPassed"]!);
            Assert.Equal(Reveal.Document, host2.Reveals[^1]);
            Assert.True(shown.LastApply!.ScrollToChange);

            Assert.NotNull((await Replace("B", "b", "document"))["result"]);
            Assert.Equal(Reveal.Document, host2.Reveals[^1]);
            Assert.False(shown.LastApply!.ScrollToChange);
            Assert.NotNull((await Replace("b", "B", null))["result"]);
            Assert.Equal(Reveal.None, host2.Reveals[^1]);
            Assert.False(shown.LastApply!.ScrollToChange);

            var unknown = await Replace("B", "b", "elsewhere");
            Assert.Equal("invalid_params", Kind(unknown));
            Assert.Equal("a\nB\n", shown.Text);
        }

        [Fact]
        public async Task AwaitPresentation_reports_what_was_drawn_and_needs_reveal()
        {
            var (h, host) = Start();
            await using var _ = h;
            var doc = host.Add("a\n", "/tmp/p.md");
            await h.InitializeAsync(AllScopes);

            var alone = await h.CallAsync("document.replace", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 0, ["text"] = "b\n", ["awaitPresentation"] = true });
            Assert.Equal("requiresReveal", (string)alone["error"]!["data"]!["reason"]!);
            Assert.Equal(0, doc.Revision);

            var shown = await h.CallAsync("document.replace", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 0, ["text"] = "b\n", ["reveal"] = "document", ["awaitPresentation"] = true });
            Valid("document.replace.result", shown["result"]);
            Assert.True((bool)shown["result"]!["presentation"]!["pageFramesPassed"]!);

            var plain = await h.CallAsync("document.replace", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 1, ["text"] = "c\n", ["reveal"] = "document" });
            Assert.Null(plain["result"]!["presentation"]);
            Assert.Equal(1, host.PresentationWaits);

            // A page that does not draw in time: the write stays committed and the error says so.
            host.NextPresentation = new Presentation { WindowVisible = true, TabActive = true, PageFramesPassed = false, HostRenderPassed = true };
            var late = await h.CallAsync("document.replace", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 2, ["text"] = "b\n", ["reveal"] = "document", ["awaitPresentation"] = true });
            Assert.Equal(-32023, (int)late["error"]!["code"]!);
            var data = late["error"]!["data"]!;
            Assert.True((bool)data["applied"]!);
            Assert.Equal(3, (long)data["revision"]!);
            Assert.Equal(DocumentText.ContentHash("b\n"), (string)data["contentHash"]!);
            Assert.False((bool)data["presentation"]!["pageFramesPassed"]!);
            Assert.Equal(3, doc.Revision);
        }

        [Fact]
        public async Task Create_with_text_that_cannot_be_written_leaves_nothing_behind()
        {
            var (h, host) = Start();
            await using var _ = h;
            await h.InitializeAsync(AllScopes);
            var empty = await h.CallAsync("document.create");
            Valid("document.create.result", empty["result"]);
            var created = await h.CallAsync("document.create", new JObject { ["text"] = "# hi\n" });
            Valid("document.create.result", created["result"]);
            Assert.Equal(1, (int)created["result"]!["revision"]!);

            host.NewDocumentPage = c => FakeDocument.Classified(c, PendingNormalization.Unsafe, "fence-lost");
            var before = host.Docs.Count;
            var refused = await h.CallAsync("document.create", new JObject { ["text"] = "```ts {x}\n```\n" });
            Assert.Equal("content_not_roundtrippable", Kind(refused));
            Assert.Equal(before, host.Docs.Count);
            Assert.Single(host.Discarded);
        }

        [Fact]
        public async Task Open_and_focus_return_the_document()
        {
            var (h, host) = Start();
            await using var _ = h;
            await h.InitializeAsync(AllScopes);
            var opened = await h.CallAsync("document.open", new JObject { ["path"] = "/tmp/n.md" });
            Valid("document.open.result", opened["result"]);
            var again = await h.CallAsync("document.open", new JObject { ["path"] = "/tmp/n.md" });
            Assert.Equal((string)opened["result"]!["documentId"]!, (string)again["result"]!["documentId"]!);
            Valid("document.focus.result", (await h.CallAsync("document.focus", new JObject { ["documentId"] = (string)opened["result"]!["documentId"]! }))["result"]);
            Assert.Equal("window_not_found", Kind(await h.CallAsync("window.focus", new JObject { ["windowId"] = "wnope" })));
        }

        [Fact]
        public void Headings_skip_code_and_front_matter_and_number_repeats()
        {
            var headings = Headings.Extract("---\n# not: heading\n---\n# Intro *one*\n\n```\n# code\n```\nSetext Title\n===\n## Intro one\n### [Link](http://x) & Co.!\n#### 中文 标题\n");
            Assert.Equal(new[] { "Intro one", "Setext Title", "Intro one", "Link & Co.!", "中文 标题" }, headings.Select(x => x.Text));
            Assert.Equal(new[] { "intro-one", "setext-title", "intro-one-1", "link--co", "中文-标题" }, headings.Select(x => x.Slug));
            Assert.Equal(new[] { 1, 1, 2, 3, 4 }, headings.Select(x => x.Level));
        }
    }
}
