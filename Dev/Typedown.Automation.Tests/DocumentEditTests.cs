using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Typedown.Automation.Tests
{
    /// <summary>A document whose page behaves as each test says, recording what the coordinator asked of it.</summary>
    internal sealed class FakeDocument : IEditableDocument
    {
        public string DocumentId { get; } = Guid.NewGuid().ToString("N");
        public long Revision { get; set; }
        public string Text { get; set; } = "# base\n";
        public bool Saved { get; set; } = true;
        public bool IsActive { get; set; } = true;

        /// <summary>The text shown in the page; the host text lags it until a flush.</summary>
        public string PageText;
        public bool FlushWorks = true;
        public Func<ApplyCommand, ApplyReply>? Page;
        public bool RestoreWorks = true;
        public bool SaveWorks = true;
        public readonly List<string> Calls = new();
        public int UndoSteps;

        public FakeDocument() => PageText = Text;

        public static ApplyReply Classified(ApplyCommand c, string pending, params string[] reasons) =>
            new(ApplyOutcome.Applied, DocumentText.ContentHash(c.Text), new NormalizationInfo(pending, DocumentText.ContentHash(c.Text), DocumentText.ContentHash(c.Text + "~"), reasons, 2));

        public Task<bool> FlushAsync(CancellationToken ct)
        {
            Calls.Add("flush");
            if (!FlushWorks) return Task.FromResult(false);
            if (PageText != Text) { Text = PageText; Revision++; Saved = false; }
            return Task.FromResult(true);
        }

        public Action? AfterCapture;

        public object CaptureState() { Calls.Add("capture"); var state = (Text, "caret", 12.5); AfterCapture?.Invoke(); return state; }

        /// <summary>What the page was last handed.</summary>
        public ApplyCommand? LastApply;

        public Task<ApplyReply> ApplyInEditorAsync(ApplyCommand command, CancellationToken ct)
        {
            Calls.Add("apply");
            LastApply = command;
            if (DocumentText.ContentHash(PageText) != command.BaseContentHash) return Task.FromResult(new ApplyReply(ApplyOutcome.Conflict));
            var reply = (Page ?? (c => Classified(c, PendingNormalization.None)))(command);
            if (reply.Outcome != ApplyOutcome.Conflict) PageText = command.Text; // whatever happened, the page changed
            return Task.FromResult(reply);
        }

        /// <summary>Set by a test: the page loaded something else after applying (a web view reload).</summary>
        public bool ReloadedAfterApply;

        public bool EditorStillHoldsEdit()
        {
            Calls.Add("check");
            if (!ReloadedAfterApply) return true;
            PageText = Text; // a reload shows what the host holds, which is still the text before the edit
            return false;
        }

        public Task<string?> RestoreAsync(object state, CancellationToken ct)
        {
            Calls.Add("restore");
            if (!RestoreWorks) return Task.FromResult<string?>(null);
            PageText = (((string, string, double))state).Item1;
            return Task.FromResult<string?>(DocumentText.ContentHash(PageText));
        }

        public void Commit(string text, long revision, string operationId)
        {
            Calls.Add("commit");
            Text = text;
            Revision = revision;
            Saved = false;
            UndoSteps++;
        }

        public void EndEdit() => Calls.Add("end");

        public Task<bool> SaveAsync(CancellationToken ct)
        {
            Calls.Add("save");
            if (SaveWorks) Saved = true;
            return Task.FromResult(SaveWorks);
        }
    }

    public class DocumentEditTests
    {
        private readonly DocumentEditCoordinator coordinator = new(2);

        private static EditRequest Replace(long baseRevision, string text, bool allowUnknown = false, bool save = false) =>
            new() { BaseRevision = baseRevision, Edit = _ => text, AllowUnknown = allowUnknown, Save = save };

        private async Task<AutomationErrorKind> Fails(FakeDocument doc, EditRequest request) =>
            (await Assert.ThrowsAsync<AutomationException>(() => coordinator.EditAsync(doc, request, CancellationToken.None))).Kind;

        [Fact]
        public async Task A_safe_edit_commits_once_with_the_next_revision()
        {
            var doc = new FakeDocument();
            var result = await coordinator.EditAsync(doc, Replace(0, "# new\n"), CancellationToken.None);
            Assert.Equal(1, result.Revision);
            Assert.Equal(DocumentText.ContentHash("# new\n"), result.ContentHash);
            Assert.Equal("automation", result.Origin);
            Assert.False(result.Saved);
            Assert.Equal(32, result.OperationId.Length);
            Assert.Equal(new[] { "flush", "capture", "apply", "check", "commit", "end" }, doc.Calls);
            Assert.Equal(("# new\n", 1L, 1), (doc.Text, doc.Revision, doc.UndoSteps));
        }

        [Fact]
        public async Task A_stale_base_revision_changes_nothing()
        {
            var doc = new FakeDocument { Revision = 5 };
            var e = await Assert.ThrowsAsync<AutomationException>(() => coordinator.EditAsync(doc, Replace(4, "x\n"), CancellationToken.None));
            Assert.Equal(AutomationErrorKind.revision_conflict, e.Kind);
            Assert.Equal(5L, e.ErrorData["revision"]);
            Assert.DoesNotContain("apply", doc.Calls);
        }

        [Fact]
        public async Task Typing_not_yet_reported_is_flushed_first_and_makes_the_base_stale()
        {
            var doc = new FakeDocument();
            doc.PageText = "# base typed\n";
            Assert.Equal(AutomationErrorKind.revision_conflict, await Fails(doc, Replace(0, "x\n")));
            Assert.Equal("# base typed\n", doc.Text);
            Assert.Equal(1, doc.Revision);
        }

        [Fact]
        public async Task Typing_between_the_flush_and_the_apply_is_a_conflict_detected_by_the_page()
        {
            var doc = new FakeDocument { Page = _ => throw new InvalidOperationException("the page must refuse before applying") };
            doc.AfterCapture = () => doc.PageText += "typed";
            Assert.Equal(AutomationErrorKind.revision_conflict, await Fails(doc, Replace(0, "x\n")));
            Assert.DoesNotContain("restore", doc.Calls);
            Assert.DoesNotContain("commit", doc.Calls);
            Assert.Equal("# base\ntyped", doc.PageText);
        }

        [Fact]
        public async Task An_unknown_normalization_is_refused_by_default_and_the_page_is_restored()
        {
            var doc = new FakeDocument { Page = c => FakeDocument.Classified(c, PendingNormalization.Unknown, "formatting-changed") };
            var e = await Assert.ThrowsAsync<AutomationException>(() => coordinator.EditAsync(doc, Replace(0, "* a\n"), CancellationToken.None));
            Assert.Equal(AutomationErrorKind.normalization_unclassified, e.Kind);
            Assert.Equal(2, e.ErrorData["classifierVersion"]);
            Assert.Equal(new[] { "formatting-changed" }, (IReadOnlyList<string>)e.ErrorData["reasons"]!);
            Assert.Equal(new[] { "flush", "capture", "apply", "restore" }, doc.Calls);
            Assert.Equal(("# base\n", "# base\n", 0L, 0), (doc.Text, doc.PageText, doc.Revision, doc.UndoSteps));
        }

        [Fact]
        public async Task AllowUnknown_commits_and_keeps_reporting_the_risk()
        {
            var doc = new FakeDocument { Page = c => FakeDocument.Classified(c, PendingNormalization.Unknown, "formatting-changed") };
            var result = await coordinator.EditAsync(doc, Replace(0, "* a\n", allowUnknown: true), CancellationToken.None);
            Assert.Equal(PendingNormalization.Unknown, result.Normalization.Pending);
            Assert.Equal(1, result.Revision);
        }

        [Fact]
        public async Task Unsafe_is_refused_under_every_policy()
        {
            var doc = new FakeDocument { Page = c => FakeDocument.Classified(c, PendingNormalization.Unsafe, "fence-lost") };
            Assert.Equal(AutomationErrorKind.content_not_roundtrippable, await Fails(doc, Replace(0, "```ts {x}\n```\n", allowUnknown: true)));
            Assert.Equal(("# base\n", 0L), (doc.PageText, doc.Revision));
            Assert.Contains("restore", doc.Calls);
        }

        [Theory]
        [InlineData("timeout")]
        [InlineData("failed")]
        [InlineData("wrongText")]
        public async Task A_page_that_fails_is_restored_and_nothing_is_committed(string how)
        {
            var doc = new FakeDocument
            {
                Page = c => how switch
                {
                    "timeout" => throw new TimeoutException(),
                    "failed" => new ApplyReply(ApplyOutcome.Failed),
                    _ => FakeDocument.Classified(new ApplyCommand(c.OperationId, c.TargetRevision, c.BaseContentHash, c.Text + "extra"), PendingNormalization.None),
                },
            };
            Assert.Equal(AutomationErrorKind.editor_not_ready, await Fails(doc, Replace(0, "x\n")));
            Assert.Equal(("# base\n", "# base\n", 0L), (doc.Text, doc.PageText, doc.Revision));
            Assert.DoesNotContain("commit", doc.Calls);
        }

        [Fact]
        public async Task A_reload_after_the_page_applied_is_not_committed_and_restores()
        {
            var doc = new FakeDocument { ReloadedAfterApply = true };
            var e = await Assert.ThrowsAsync<AutomationException>(() => coordinator.EditAsync(doc, Replace(0, "# written\n"), CancellationToken.None));
            Assert.Equal(AutomationErrorKind.editor_not_ready, e.Kind);
            Assert.Equal("editorReloaded", e.ErrorData["reason"]);
            Assert.DoesNotContain("commit", doc.Calls);
            Assert.Contains("restore", doc.Calls);
            Assert.Equal(0, doc.Revision);
            Assert.Equal("# base\n", doc.Text);
            Assert.Equal("# base\n", doc.PageText);
        }

        [Fact]
        public async Task A_restore_that_fails_quarantines_the_document()
        {
            var doc = new FakeDocument { Page = _ => new ApplyReply(ApplyOutcome.Failed), RestoreWorks = false };
            var e = await Assert.ThrowsAsync<AutomationException>(() => coordinator.EditAsync(doc, Replace(0, "x\n"), CancellationToken.None));
            Assert.Equal(AutomationErrorKind.editor_inconsistent, e.Kind);
            Assert.Equal("editor_not_ready", e.ErrorData["cause"]);
            Assert.True(coordinator.IsQuarantined(doc.DocumentId));
            doc.Page = null;
            doc.RestoreWorks = true;
            Assert.Equal(AutomationErrorKind.editor_inconsistent, await Fails(doc, Replace(0, "y\n")));
            coordinator.Forget(doc.DocumentId);
            Assert.False(coordinator.IsQuarantined(doc.DocumentId));
        }

        [Fact]
        public async Task A_flush_that_times_out_refuses_to_write()
        {
            var doc = new FakeDocument { FlushWorks = false };
            Assert.Equal(AutomationErrorKind.content_sync_timeout, await Fails(doc, Replace(0, "x\n")));
            Assert.Equal(new[] { "flush" }, doc.Calls);
        }

        [Fact]
        public async Task A_background_document_needs_allowUnknown_and_never_touches_an_editor()
        {
            var doc = new FakeDocument { IsActive = false };
            var e = await Assert.ThrowsAsync<AutomationException>(() => coordinator.EditAsync(doc, Replace(0, "x\n"), CancellationToken.None));
            Assert.Equal(AutomationErrorKind.normalization_unclassified, e.Kind);
            Assert.Equal(new[] { "notEvaluated" }, (IReadOnlyList<string>)e.ErrorData["reasons"]!);
            Assert.Null(e.ErrorData["normalizedHash"]);
            var result = await coordinator.EditAsync(doc, Replace(0, "x\n", allowUnknown: true), CancellationToken.None);
            Assert.Equal(1, result.Revision);
            Assert.Equal(new[] { "commit", "end" }, doc.Calls);
        }

        [Fact]
        public async Task The_same_text_is_no_change()
        {
            var doc = new FakeDocument();
            var result = await coordinator.EditAsync(doc, Replace(0, "# base\n"), CancellationToken.None);
            Assert.Equal(0, result.Revision);
            Assert.DoesNotContain("commit", doc.Calls);
            Assert.Equal(PendingNormalization.None, result.Normalization.Pending);
        }

        [Fact]
        public async Task Text_with_cr_and_replaceText_mismatches_change_nothing()
        {
            var doc = new FakeDocument();
            Assert.Equal(AutomationErrorKind.invalid_params, await Fails(doc, Replace(0, "a\r\n")));
            Assert.Equal(AutomationErrorKind.match_count_mismatch,
                await Fails(doc, new EditRequest { BaseRevision = 0, Edit = t => DocumentText.ReplaceText(t, "base", "b", 2) }));
            Assert.DoesNotContain("apply", doc.Calls);
            var ok = await coordinator.EditAsync(doc, new EditRequest { BaseRevision = 0, Edit = t => DocumentText.ReplaceText(t, "base", "top", 1) }, CancellationToken.None);
            Assert.Equal("# top\n", doc.Text);
            Assert.Equal(1, ok.Revision);
        }

        [Fact]
        public async Task Save_failure_keeps_the_committed_edit_and_says_so()
        {
            var doc = new FakeDocument { SaveWorks = false };
            var e = await Assert.ThrowsAsync<AutomationException>(() => coordinator.EditAsync(doc, Replace(0, "x\n", save: true), CancellationToken.None));
            Assert.Equal(AutomationErrorKind.save_failed, e.Kind);
            Assert.Equal(true, e.ErrorData["applied"]);
            Assert.Equal(("x\n", 1L, false), (doc.Text, doc.Revision, doc.Saved));
            doc.SaveWorks = true;
            var saved = await coordinator.EditAsync(doc, Replace(1, "y\n", save: true), CancellationToken.None);
            Assert.True(saved.Saved);
            // Held typing is released only after the save: the save writes the edit's revision.
            Assert.Equal(new[] { "commit", "save", "end" }, doc.Calls.Where(c => c is "commit" or "save" or "end").TakeLast(3));
        }

        [Fact]
        public async Task Two_writers_on_the_same_base_run_one_at_a_time_and_the_second_conflicts()
        {
            var doc = new FakeDocument();
            var entered = 0;
            var release = new TaskCompletionSource<bool>();
            doc.Page = c =>
            {
                if (Interlocked.Increment(ref entered) > 1) throw new InvalidOperationException("two edits in the page at once");
                release.Task.Wait();
                Interlocked.Decrement(ref entered);
                return FakeDocument.Classified(c, PendingNormalization.None);
            };
            var first = Task.Run(() => coordinator.EditAsync(doc, Replace(0, "one\n"), CancellationToken.None));
            var second = Task.Run(() => coordinator.EditAsync(doc, Replace(0, "two\n"), CancellationToken.None));
            await Task.Delay(100);
            release.SetResult(true);
            var outcomes = await Task.WhenAll(first.ContinueWith(t => t), second.ContinueWith(t => t));
            Assert.Single(outcomes, t => t.Status == TaskStatus.RanToCompletion);
            var failed = outcomes.Single(t => t.IsFaulted).Exception!.InnerException as AutomationException;
            Assert.Equal(AutomationErrorKind.revision_conflict, failed!.Kind);
            Assert.Equal(1, doc.Revision);
        }
    }
}

namespace Typedown.Automation.Tests
{
    public class DocumentEditThreadTests
    {
        /// <summary>Started on a UI thread with a synchronization context, every document call stays on that thread.</summary>
        [Fact]
        public async Task An_edit_started_on_the_ui_thread_stays_there()
        {
            var threads = new System.Collections.Concurrent.ConcurrentBag<int>();
            var uiThread = 0;
            var done = new TaskCompletionSource<EditResult>();
            var thread = new Thread(() =>
            {
                uiThread = Environment.CurrentManagedThreadId;
                var context = new SingleThreadContext();
                SynchronizationContext.SetSynchronizationContext(context);
                var doc = new ThreadRecordingDocument(threads);
                var coordinator = new DocumentEditCoordinator(2);
                coordinator.EditAsync(doc, new EditRequest { BaseRevision = 0, Edit = _ => "x\n" }, CancellationToken.None)
                    .ContinueWith(t => { if (t.IsFaulted) done.SetException(t.Exception!.InnerException!); else done.SetResult(t.Result); context.Complete(); }, TaskScheduler.Default);
                context.Run();
            });
            thread.Start();
            var result = await done.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(1, result.Revision);
            Assert.NotEmpty(threads);
            Assert.All(threads, t => Assert.Equal(uiThread, t));
        }

        private sealed class SingleThreadContext : SynchronizationContext
        {
            private readonly System.Collections.Concurrent.BlockingCollection<(SendOrPostCallback, object?)> queue = new();
            public override void Post(SendOrPostCallback d, object? state) => queue.Add((d, state));
            public void Run() { foreach (var (d, s) in queue.GetConsumingEnumerable()) d(s); }
            public void Complete() => queue.CompleteAdding();
        }

        /// <summary>Awaits real asynchrony (a timer) inside each call, so continuations have to find their way back.</summary>
        private sealed class ThreadRecordingDocument : IEditableDocument
        {
            private readonly System.Collections.Concurrent.ConcurrentBag<int> threads;
            public ThreadRecordingDocument(System.Collections.Concurrent.ConcurrentBag<int> threads) => this.threads = threads;
            private void Note() => threads.Add(Environment.CurrentManagedThreadId);
            public string DocumentId { get; } = "d";
            public long Revision { get { Note(); return rev; } }
            private long rev;
            public string Text { get { Note(); return text; } }
            private string text = "a\n";
            public bool Saved { get { Note(); return true; } }
            public bool IsActive { get { Note(); return true; } }
            public async Task<bool> FlushAsync(CancellationToken ct) { Note(); await Task.Delay(20); Note(); return true; }
            public object CaptureState() { Note(); return text; }
            public async Task<ApplyReply> ApplyInEditorAsync(ApplyCommand c, CancellationToken ct)
            {
                Note(); await Task.Delay(20); Note();
                return FakeDocument.Classified(c, PendingNormalization.None);
            }
            public bool EditorStillHoldsEdit() { Note(); return true; }
            public Task<string?> RestoreAsync(object state, CancellationToken ct) { Note(); return Task.FromResult<string?>(null); }
            public void Commit(string t, long revision, string operationId) { Note(); text = t; rev = revision; }
            public Task<bool> SaveAsync(CancellationToken ct) { Note(); return Task.FromResult(true); }
            public void EndEdit() => Note();
        }
    }
}
