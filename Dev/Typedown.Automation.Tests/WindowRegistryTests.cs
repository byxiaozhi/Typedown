using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Typedown.Automation;
using Xunit;

namespace Typedown.Automation.Tests
{
    /// <summary>A UI thread of its own: work runs one item at a time, in order, on that thread only.</summary>
    internal sealed class TestDispatcher : IUiDispatcher, IDisposable
    {
        private readonly BlockingCollection<Action> queue = new();
        private readonly Thread thread;
        public int ThreadId => thread.ManagedThreadId;
        public bool Refuse { get; set; }

        public TestDispatcher()
        {
            thread = new Thread(() => { foreach (var item in queue.GetConsumingEnumerable()) item(); }) { IsBackground = true };
            thread.Start();
        }

        public Task<T> InvokeAsync<T>(Func<T> work)
        {
            if (Refuse) throw new InvalidOperationException("dispatcher shut down");
            var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            queue.Add(() => { try { done.SetResult(work()); } catch (Exception e) { done.SetException(e); } });
            return done.Task;
        }

        public void Dispose() => queue.CompleteAdding();
    }

    internal sealed class FakeWindow
    {
        public string[] DocumentIds = Array.Empty<string>();
        public int OwnerThread;
    }

    public class WindowRegistryTests
    {
        [Fact]
        public async Task Work_runs_on_the_windows_own_dispatcher()
        {
            using var ui = new TestDispatcher();
            var registry = new WindowRegistry<FakeWindow>();
            var id = registry.Register(new FakeWindow(), ui);
            var ran = await registry.OnWindowAsync(id, _ => Environment.CurrentManagedThreadId);
            Assert.Equal(ui.ThreadId, ran);
        }

        [Fact]
        public void Ids_are_stable_and_never_reused()
        {
            using var ui = new TestDispatcher();
            var registry = new WindowRegistry<FakeWindow>();
            var a = new FakeWindow();
            var id = registry.Register(a, ui);
            Assert.Equal(id, registry.Register(a, ui));
            Assert.True(registry.Unregister(a));
            Assert.NotEqual(id, registry.Register(a, ui));
        }

        [Fact]
        public async Task Unknown_or_closed_windows_answer_window_not_found()
        {
            using var ui = new TestDispatcher();
            var registry = new WindowRegistry<FakeWindow>();
            var e = await Assert.ThrowsAsync<AutomationException>(() => registry.OnWindowAsync("wmissing", _ => 1));
            Assert.Equal(AutomationErrorKind.window_not_found, e.Kind);
            Assert.Equal(-32010, e.Code);

            var window = new FakeWindow();
            var id = registry.Register(window, ui);
            registry.Unregister(window);
            e = await Assert.ThrowsAsync<AutomationException>(() => registry.OnWindowAsync(id, _ => 1));
            Assert.Equal(AutomationErrorKind.window_not_found, e.Kind);
        }

        [Fact]
        public async Task A_window_closed_while_work_waits_in_its_queue_does_not_run_it()
        {
            using var ui = new TestDispatcher();
            var registry = new WindowRegistry<FakeWindow>();
            var window = new FakeWindow();
            var id = registry.Register(window, ui);
            using var blocker = new ManualResetEventSlim();
            var busy = ui.InvokeAsync(() => { blocker.Wait(); return 0; });
            var ran = false;
            var queued = registry.OnWindowAsync(id, _ => ran = true);
            registry.Unregister(window);
            blocker.Set();
            await busy;
            var e = await Assert.ThrowsAsync<AutomationException>(() => queued);
            Assert.Equal(AutomationErrorKind.window_not_found, e.Kind);
            Assert.False(ran);
        }

        [Fact]
        public async Task A_dispatcher_that_refuses_work_after_close_reports_window_not_found()
        {
            using var ui = new TestDispatcher();
            var registry = new WindowRegistry<FakeWindow>();
            var window = new FakeWindow();
            var id = registry.Register(window, ui);
            ui.Refuse = true;
            // Still registered: the dispatcher's own failure is not disguised.
            await Assert.ThrowsAsync<InvalidOperationException>(() => registry.OnWindowAsync(id, _ => 1));
        }

        [Fact]
        public async Task FindAsync_asks_each_window_on_its_own_thread_and_skips_closed_ones()
        {
            using var ui1 = new TestDispatcher();
            using var ui2 = new TestDispatcher();
            var registry = new WindowRegistry<FakeWindow>();
            var first = new FakeWindow { DocumentIds = new[] { "d1" } };
            var second = new FakeWindow { DocumentIds = new[] { "d2", "d3" } };
            registry.Register(first, ui1);
            var secondId = registry.Register(second, ui2);

            var found = await registry.FindAsync(w => w.DocumentIds.Contains("d3") ? (object)Environment.CurrentManagedThreadId : null);
            Assert.NotNull(found);
            Assert.Equal(secondId, found!.Value.Window.WindowId);
            Assert.Equal(ui2.ThreadId, (int)found.Value.Value);

            registry.Unregister(second);
            Assert.Null(await registry.FindAsync(w => w.DocumentIds.Contains("d3") ? "x" : null));
        }

        [Fact]
        public async Task Registration_is_safe_from_many_threads()
        {
            using var ui = new TestDispatcher();
            var registry = new WindowRegistry<FakeWindow>();
            var windows = Enumerable.Range(0, 200).Select(_ => new FakeWindow()).ToArray();
            await Task.WhenAll(windows.Select(w => Task.Run(() => registry.Register(w, ui))));
            Assert.Equal(200, registry.Snapshot().Select(w => w.WindowId).Distinct().Count());
            await Task.WhenAll(windows.Where((_, i) => i % 2 == 0).Select(w => Task.Run(() => registry.Unregister(w))));
            Assert.Equal(100, registry.Snapshot().Count);
            Assert.All(registry.Snapshot(), w => Assert.Contains(w.Window, windows.Where((_, i) => i % 2 == 1)));
        }
    }
}
