using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Typedown.Core.Services;
using Xunit;

namespace Typedown.Automation.Tests
{
    /// <summary>The Windows settings store shared by every window (docs/automation-api-analysis-plan.md, stage 0b).</summary>
    public class SettingsStoreTests : IDisposable
    {
        private readonly string dir = Path.Combine(Path.GetTempPath(), "td-settings-" + Guid.NewGuid().ToString("N"));
        private string File(string name = "Settings.json") { Directory.CreateDirectory(dir); return Path.Combine(dir, name); }
        public void Dispose() { try { Directory.Delete(dir, true); } catch { } }

        [Fact]
        public void The_api_revision_counts_only_the_names_it_is_given_and_resets()
        {
            var store = new JsonSettingsStore(File(), (_, _) => Task.CompletedTask);
            var exposed = new[] { "FontSize", "AppTheme" };
            var start = store.RevisionOf(exposed);
            // A window moved and its pane resized: values the application keeps for itself.
            store.Set("StartupPlacement", "somewhere");
            store.Set("SidePaneWidth", 320d);
            Assert.Equal(start, store.RevisionOf(exposed));
            store.Set("FontSize", 20d);
            Assert.Equal(start + 1, store.RevisionOf(exposed));
            store.Set("FontSize", 20d); // the same value: nothing changed
            Assert.Equal(start + 1, store.RevisionOf(exposed));
            store.Reset();
            Assert.Equal(start + 2, store.RevisionOf(exposed));
            Assert.True(store.Revision >= 4, "the store's own revision still counts every change");
        }

        [Fact]
        public void Every_window_gets_the_same_store()
        {
            var path = File();
            Assert.Same(JsonSettingsStore.Shared(path), JsonSettingsStore.Shared(Path.Combine(dir, ".", "Settings.json")));
            Assert.NotSame(JsonSettingsStore.Shared(path), JsonSettingsStore.Shared(File("Other.json")));
        }

        [Fact]
        public async Task An_older_window_no_longer_puts_back_values_another_window_changed()
        {
            // Two windows opened on the same file, then each changes a different setting.
            var path = File();
            System.IO.File.WriteAllText(path, "{\"FontSize\":16.0,\"AppTheme\":0}");
            var first = JsonSettingsStore.Shared(path);
            var second = JsonSettingsStore.Shared(path);
            first.Set("FontSize", 20d, "window A");
            second.Set("AppTheme", 2, "window B");
            await first.FlushAsync();
            var saved = JObject.Parse(System.IO.File.ReadAllText(path));
            Assert.Equal(20d, (double)saved["FontSize"]!);
            Assert.Equal(2, (int)saved["AppTheme"]!);
            Assert.Equal(20d, second.Get("FontSize", 0d));
        }

        [Fact]
        public void Windows_hear_of_each_others_changes_with_the_origin()
        {
            var store = JsonSettingsStore.Shared(File());
            var heard = new List<(string, object)>();
            store.Changed += (name, origin) => heard.Add((name, origin));
            var before = store.Revision;
            store.Set("FontSize", 18d, "A");
            store.Set("FontSize", 18d, "B"); // unchanged: no event, no revision
            store.Set("TextDirection", "rtl", "B");
            store.Reset("C");
            Assert.Equal(new[] { ("FontSize", (object)"A"), ("TextDirection", "B"), ((string)null!, "C") }, heard);
            Assert.Equal(before + 3, store.Revision);
        }

        [Fact]
        public void A_listener_that_throws_does_not_silence_the_others()
        {
            var errors = new List<Exception>();
            var store = JsonSettingsStore.Shared(File(), errors.Add);
            var heard = 0;
            store.Changed += (_, _) => throw new InvalidOperationException("closed window");
            store.Changed += (_, _) => heard++;
            store.Set("FontSize", 30d);
            Assert.Equal(1, heard);
            Assert.Single(errors);
        }

        [Fact]
        public async Task A_burst_of_changes_from_many_threads_ends_with_the_last_values_on_disk()
        {
            var path = File();
            var store = JsonSettingsStore.Shared(path);
            await Task.WhenAll(Enumerable.Range(0, 8).Select(t => Task.Run(() =>
            {
                for (var i = 0; i < 200; i++) store.Set($"key{t}", i);
            })));
            await store.FlushAsync();
            var saved = JObject.Parse(System.IO.File.ReadAllText(path));
            for (var t = 0; t < 8; t++) Assert.Equal(199, (int)saved[$"key{t}"]!);
        }

        [Fact]
        public async Task A_failed_write_is_reported_and_the_next_change_writes_everything()
        {
            var path = File();
            var fail = true;
            var errors = new ConcurrentBag<Exception>();
            var store = new JsonSettingsStore(path, async (target, json) =>
            {
                await Task.Yield();
                if (fail) throw new IOException("disk full");
                System.IO.File.WriteAllText(target, json);
            }, errors.Add);
            store.Set("FontSize", 22d);
            await store.FlushAsync();
            Assert.NotNull(store.LastWriteError);
            Assert.Single(errors);
            Assert.Equal(22d, store.Get("FontSize", 0d)); // still applied in memory
            fail = false;
            store.Set("LineHeight", 2d);
            await store.FlushAsync();
            Assert.Null(store.LastWriteError);
            var saved = JObject.Parse(System.IO.File.ReadAllText(path));
            Assert.Equal(22d, (double)saved["FontSize"]!);
            Assert.Equal(2d, (double)saved["LineHeight"]!);
        }

        [Fact]
        public void Window_layout_and_modes_stay_with_their_window()
        {
            foreach (var name in new[] { "SourceCode", "ReadOnly", "SidePaneOpen", "StartupPlacement", "Topmost" })
                Assert.True(SettingsScope.IsWindowLocal(name), name);
            foreach (var name in new[] { "FontSize", "AppTheme", "CustomTheme", "LineHeight", "TextDirection", "Language" })
                Assert.False(SettingsScope.IsWindowLocal(name), name);
            Assert.False(SettingsScope.IsWindowLocal(null!));
        }
    }
}
