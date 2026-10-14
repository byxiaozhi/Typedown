using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Typedown.Automation.Tests
{
    internal sealed class FakeViewHost : IViewHost
    {
        public ViewState State = new() { Mode = "visual", SidePanePage = "files", StatusBar = true, X = 10, Y = 20, Width = 1200, Height = 800 };
        public readonly List<ViewChange> Changes = new();

        public Task<ViewState> GetViewAsync(string windowId, CancellationToken ct) =>
            windowId == FakeHost.WindowId ? Task.FromResult(State) : Task.FromException<ViewState>(new AutomationException(AutomationErrorKind.window_not_found, "no window"));

        public Task<ViewState> SetViewAsync(string windowId, ViewChange change, CancellationToken ct)
        {
            Changes.Add(change);
            if (change.Mode != null) State.Mode = change.Mode;
            if (change.SidePaneOpen is bool open) State.SidePaneOpen = open;
            if (change.SidePanePage != null) State.SidePanePage = change.SidePanePage;
            if (change.Width is int w) State.Width = w;
            if (change.Height is int h) State.Height = h;
            return GetViewAsync(windowId, ct);
        }
    }

    public class ViewMethodsTests
    {
        private static (Harness h, FakeViewHost host) Start()
        {
            var host = new FakeViewHost();
            return (new Harness(ViewMethods.AddTo(new MethodTable(BuildTypes.Application), host)), host);
        }

        private static void Valid(string def, JToken? instance)
        {
            var r = SchemaTests.EvaluateFor(def, instance);
            Assert.True(r.valid, $"{def}: {r.errors}\n{instance}");
        }

        [Fact]
        public async Task GetView_reads_and_setView_changes_only_what_it_names()
        {
            var (h, host) = Start();
            await using var _ = h;
            await h.InitializeAsync(Scopes.AppRead, Scopes.WindowView);
            var got = await h.CallAsync("window.getView", new JObject { ["windowId"] = FakeHost.WindowId });
            Valid("window.getView.result", got["result"]);
            Assert.Equal("visual", (string)got["result"]!["mode"]!);

            var set = await h.CallAsync("window.setView", new JObject { ["windowId"] = FakeHost.WindowId, ["mode"] = "source", ["sidePane"] = new JObject { ["open"] = true, ["page"] = "outline" }, ["bounds"] = new JObject { ["width"] = 1280, ["height"] = 860 } });
            Valid("window.setView.result", set["result"]);
            Assert.Equal("source", (string)set["result"]!["mode"]!);
            Assert.Equal("outline", (string)set["result"]!["sidePane"]!["page"]!);
            var change = host.Changes[0];
            Assert.Null(change.StatusBar);
            Assert.Null(change.X);
            Assert.Equal(1280, change.Width);
        }

        [Fact]
        public async Task SetView_checks_its_parameters_and_needs_window_view()
        {
            var (h, host) = Start();
            await using var _ = h;
            await h.InitializeAsync(Scopes.AppRead);
            Assert.Equal("window.view", (string)(await h.CallAsync("window.setView", new JObject { ["windowId"] = FakeHost.WindowId, ["mode"] = "source" }))["error"]!["data"]!["scope"]!);

            var (h2, host2) = Start();
            await using var __ = h2;
            await h2.InitializeAsync(Scopes.AppRead, Scopes.WindowView);
            async Task<string?> Reason(JObject p) { p["windowId"] = FakeHost.WindowId; return (string?)(await h2.CallAsync("window.setView", p))["error"]?["data"]?["reason"]; }
            Assert.Equal("nothingToChange", await Reason(new JObject()));
            Assert.NotNull(await Reason(new JObject { ["mode"] = "print" }));
            Assert.NotNull(await Reason(new JObject { ["bounds"] = new JObject { ["width"] = 100 } }));
            Assert.NotNull(await Reason(new JObject { ["sidePane"] = new JObject { ["page"] = "search" } }));
            Assert.Empty(host2.Changes);
            Assert.Equal(-32010, (int)(await h2.CallAsync("window.getView", new JObject { ["windowId"] = "w00000000000000000000000000000000" }))["error"]!["code"]!);
        }

        [Fact]
        public async Task A_mode_switch_and_an_edit_of_the_same_document_run_one_after_the_other()
        {
            var coordinator = new DocumentEditCoordinator(3);
            var doc = new FakeDocument();
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var inSwitch = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var order = new List<string>();
            var modeSwitch = coordinator.ExclusiveAsync(doc.DocumentId, async () => { inSwitch.SetResult(true); await release.Task; order.Add("switch"); return true; }, CancellationToken.None);
            await inSwitch.Task;
            var edit = coordinator.EditAsync(doc, new EditRequest { BaseRevision = 0, Edit = _ => "# edited\n" }, CancellationToken.None);
            await Task.Delay(100);
            Assert.False(edit.IsCompleted, "the edit waits for the mode switch");
            Assert.DoesNotContain("apply", doc.Calls);
            release.SetResult(true);
            await modeSwitch;
            await edit;
            order.Add("edit");
            Assert.Equal(new[] { "switch", "edit" }, order);
        }
    }
}
