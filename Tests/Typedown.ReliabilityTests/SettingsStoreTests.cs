using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Typedown.Core.Services;

namespace Typedown.ReliabilityTests
{
    [TestClass]
    public class SettingsStoreTests
    {
        [TestMethod]
        public async Task LegacyValuesAndUnknownFieldsSurviveAWrite()
        {
            using var directory = new TestDirectory();
            var path = directory.File("Settings.json");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "legacy-settings.json"), path);

            var store = new JsonSettingsStore(path);
            Assert.AreEqual(18.5, store.Get("FontSize", 0d));
            Assert.IsTrue(store.Get("AutoSave", false));
            Assert.AreEqual("zh-Hans", store.Get("Language", "default"));

            store.Set("FontSize", 20d);
            await store.FlushAsync();

            var saved = JObject.Parse(await File.ReadAllTextAsync(path));
            Assert.AreEqual(20d, saved.Value<double>("FontSize"));
            Assert.AreEqual("keep-me", saved["FutureFeature"]?["token"]?.Value<string>());
        }

        [TestMethod]
        public async Task DamagedFileFallsBackAndNextWriteRepairsIt()
        {
            using var directory = new TestDirectory();
            var path = directory.File("Settings.json");
            await File.WriteAllTextAsync(path, "{ this is not JSON");

            var store = new JsonSettingsStore(path);
            Assert.AreEqual(16d, store.Get("FontSize", 16d));

            store.Set("Language", "en");
            await store.FlushAsync();

            var saved = JObject.Parse(await File.ReadAllTextAsync(path));
            Assert.AreEqual("en", saved.Value<string>("Language"));
            Assert.IsNull(store.LastWriteError);
        }

        [TestMethod]
        public void IncompatibleLegacyValueFallsBackWithoutDiscardingOtherValues()
        {
            using var directory = new TestDirectory();
            var path = directory.File("Settings.json");
            File.WriteAllText(path, "{\"FontSize\":{\"old\":true},\"Language\":\"fr\"}");

            var store = new JsonSettingsStore(path);
            Assert.AreEqual(16d, store.Get("FontSize", 16d));
            Assert.AreEqual("fr", store.Get("Language", "default"));
        }

        [TestMethod]
        public async Task RapidChangesAreMergedAndNeverWrittenConcurrently()
        {
            using var directory = new TestDirectory();
            var path = directory.File("Settings.json");
            var active = 0;
            var maxActive = 0;
            var writes = 0;

            async Task SlowWriter(string target, string json)
            {
                var now = Interlocked.Increment(ref active);
                int observed;
                while (now > (observed = maxActive))
                    Interlocked.CompareExchange(ref maxActive, now, observed);
                Interlocked.Increment(ref writes);
                await Task.Delay(10);
                await File.WriteAllTextAsync(target, json);
                Interlocked.Decrement(ref active);
            }

            var store = new JsonSettingsStore(path, SlowWriter);
            for (var i = 0; i < 200; i++)
            {
                store.Set("FontSize", i);
                store.Set("Language", i % 2 == 0 ? "en" : "zh-Hans");
            }
            await store.FlushAsync();

            var saved = JObject.Parse(await File.ReadAllTextAsync(path));
            Assert.AreEqual(199, saved.Value<int>("FontSize"));
            Assert.AreEqual("zh-Hans", saved.Value<string>("Language"));
            Assert.AreEqual(1, maxActive, "settings writes overlapped");
            Assert.IsTrue(writes < 400, "rapid changes were not coalesced");
        }

        [TestMethod]
        public async Task ResetWritesAnEmptyObject()
        {
            using var directory = new TestDirectory();
            var path = directory.File("Settings.json");
            File.WriteAllText(path, "{\"Language\":\"de\",\"FutureFeature\":42}");
            var store = new JsonSettingsStore(path);

            store.Reset();
            await store.FlushAsync();

            Assert.AreEqual(0, JObject.Parse(await File.ReadAllTextAsync(path)).Count);
        }

        [TestMethod]
        public async Task AFailedWriteIsReportedAndALaterChangeRetries()
        {
            using var directory = new TestDirectory();
            var path = directory.File("Settings.json");
            var attempts = 0;

            async Task FailOnce(string target, string json)
            {
                if (Interlocked.Increment(ref attempts) == 1)
                    throw new IOException("injected failure");
                await File.WriteAllTextAsync(target, json);
            }

            var store = new JsonSettingsStore(path, FailOnce);
            store.Set("FontSize", 18);
            await store.FlushAsync();
            Assert.IsNotNull(store.LastWriteError);

            store.Set("FontSize", 19);
            await store.FlushAsync();

            Assert.IsNull(store.LastWriteError);
            Assert.AreEqual(19, JObject.Parse(await File.ReadAllTextAsync(path)).Value<int>("FontSize"));
            Assert.AreEqual(2, attempts);
        }
    }
}
