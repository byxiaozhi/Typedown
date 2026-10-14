using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using Typedown.Core.Services;
using Typedown.Core.Utilities;

namespace Typedown.ReliabilityTests
{
    [TestClass]
    public class SafeFileAndBackupTests
    {
        [TestMethod]
        public async Task AtomicWriteReplacesContentWithoutLeavingATemporaryFile()
        {
            using var directory = new TestDirectory();
            var path = directory.File("document.md");
            await File.WriteAllTextAsync(path, "old\n", new UTF8Encoding(false));

            await SafeFile.WriteAllTextAtomicAsync(path, "new\n", new UTF8Encoding(false));

            Assert.AreEqual("new\n", await File.ReadAllTextAsync(path));
            CollectionAssert.AreEqual(new[] { path }, Directory.GetFiles(directory.Path));
        }

        [TestMethod]
        public async Task FailedTemporaryWriteLeavesTheOriginalUntouched()
        {
            using var directory = new TestDirectory();
            var path = directory.File(new string('x', 245) + ".md");
            await File.WriteAllTextAsync(path, "original\n");

            await AssertAtomicWriteFailure(() => SafeFile.WriteAllTextAtomicAsync(path, "replacement\n"));

            Assert.AreEqual("original\n", await File.ReadAllTextAsync(path));
        }

        [TestMethod]
        public async Task LockedWindowsTargetFailsWithoutTruncatingTheOriginal()
        {
            if (!OperatingSystem.IsWindows())
                Assert.Inconclusive("Windows sharing locks are required for this assertion.");

            using var directory = new TestDirectory();
            var path = directory.File("locked.md");
            await File.WriteAllTextAsync(path, "original\n");

            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                await AssertAtomicWriteFailure(() => SafeFile.WriteAllTextAtomicAsync(path, "replacement\n"));

            Assert.AreEqual("original\n", await File.ReadAllTextAsync(path));
            var recovery = Directory.GetFiles(directory.Path, ".locked.md.*.tmp");
            Assert.IsTrue(recovery.Length > 0, "the completed recovery copy was not retained");
            Assert.AreEqual("replacement\n", await File.ReadAllTextAsync(recovery[0]));
        }

        [TestMethod]
        public async Task BackupCanBeReadOverwrittenAndDeleted()
        {
            using var directory = new TestDirectory();
            var backup = new AutoBackup(directory.Path);
            const string document = @"C:\docs\guide.md";

            Assert.IsTrue(await backup.Backup(document, "first"));
            Assert.AreEqual("first", await backup.GetBackup(document));
            Assert.IsTrue(await backup.Backup(document, "second"));
            Assert.AreEqual("second", await backup.GetBackup(document));

            backup.DeleteBackup(document);
            Assert.IsNull(await backup.GetBackup(document));
        }

        private static async Task AssertAtomicWriteFailure(Func<Task> action)
        {
            Exception failure = null;
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            Assert.IsTrue(failure is IOException || failure is UnauthorizedAccessException,
                $"expected an I/O failure, got {failure?.GetType().FullName ?? "no exception"}");
        }
    }
}
