using System;
using System.IO;
using System.Threading.Tasks;
using Typedown.Core.Services;
using Xunit;

namespace Typedown.Automation.Tests
{
    /// <summary>Typedown.Core UploadHistory: a picture goes up once per configuration.</summary>
    public class UploadHistoryTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "td-upload-history-" + Guid.NewGuid().ToString("N"));

        public UploadHistoryTests() => Directory.CreateDirectory(folder);

        public void Dispose() => Directory.Delete(folder, true);

        private string File(string name, byte content)
        {
            var path = Path.Combine(folder, name);
            System.IO.File.WriteAllBytes(path, new byte[] { 0x89, 0x50, 0x4E, 0x47, content });
            return path;
        }

        private string Store => Path.Combine(folder, "history.json");

        [Fact]
        public async Task The_same_content_goes_up_once_per_configuration()
        {
            var history = new UploadHistory(Store);
            var scope = UploadHistory.Scope("OSS", "{\"Bucket\":\"a\"}");
            var calls = 0;
            Task<string> Upload(string url) { calls++; return Task.FromResult(url); }

            var first = await history.UploadAsync(scope, File("a.png", 1), () => Upload("https://x/a.png"));
            Assert.Equal(("https://x/a.png", false), first);
            // The same picture under another name and path: the earlier address.
            var again = await history.UploadAsync(scope, File("copy of a.png", 1), () => Upload("https://x/other.png"));
            Assert.Equal(("https://x/a.png", true), again);
            Assert.Equal(1, calls);

            // Other content, or the configuration changed: uploaded.
            await history.UploadAsync(scope, File("b.png", 2), () => Upload("https://x/b.png"));
            var changed = await history.UploadAsync(UploadHistory.Scope("OSS", "{\"Bucket\":\"b\"}"), File("a.png", 1), () => Upload("https://y/a.png"));
            Assert.Equal(("https://y/a.png", false), changed);
            Assert.Equal(3, calls);

            // Remembered by a new instance (another window, the next start).
            var reopened = new UploadHistory(Store);
            Assert.Equal(3, await reopened.CountAsync());
            Assert.True((await reopened.UploadAsync(scope, File("a.png", 1), () => Upload("https://z"))).reused);

            await reopened.ClearAsync();
            Assert.Equal(0, await new UploadHistory(Store).CountAsync());
            Assert.False((await reopened.UploadAsync(scope, File("a.png", 1), () => Upload("https://x/a2.png"))).reused);
            Assert.Equal(4, calls);
        }

        [Fact]
        public async Task A_failed_upload_is_not_remembered()
        {
            var history = new UploadHistory(Store);
            var scope = UploadHistory.Scope("PowerShell", "{}");
            var file = File("a.png", 1);
            await Assert.ThrowsAsync<InvalidOperationException>(() => history.UploadAsync(scope, file, () => throw new InvalidOperationException("403")));
            await history.UploadAsync(scope, file, () => Task.FromResult(""));
            Assert.Equal(0, await history.CountAsync());
            Assert.False((await history.UploadAsync(scope, file, () => Task.FromResult("https://x/a.png"))).reused);
        }

        [Fact]
        public async Task An_unreadable_history_only_costs_uploads()
        {
            System.IO.File.WriteAllText(Store, "{ not json");
            var history = new UploadHistory(Store);
            Assert.False((await history.UploadAsync("s", File("a.png", 1), () => Task.FromResult("https://x/a.png"))).reused);
            Assert.True((await new UploadHistory(Store).UploadAsync("s", File("a.png", 1), () => Task.FromResult("https://x/b.png"))).reused);
        }
    }
}
