using System.IO;
using System.Threading.Tasks;
using Typedown.Core.Utilities;

namespace Typedown.Core.Services
{
    public class AutoBackup
    {
        private readonly string backupPath;

        public AutoBackup(string backupPath = null)
        {
            this.backupPath = backupPath ?? Path.Combine(Config.GetLocalFolderPath(), "Backup");
        }

        private const string UntitledPrefix = "untitled_";

        /// <summary>
        /// A document's backup file. A titled document's is keyed by its path (recovery matches it on reopen). An untitled
        /// one's is keyed by its document id when that is given: all untitled documents used to share the empty path's
        /// file, so with several open only the last one written survived a crash, and background ones were never kept.
        /// </summary>
        public string GetBackupFilePath(string sourcePath, string documentId = null)
        {
            if (!Directory.Exists(backupPath))
                Directory.CreateDirectory(backupPath);
            if (string.IsNullOrEmpty(sourcePath) && !string.IsNullOrEmpty(documentId))
                return Path.Combine(backupPath, UntitledPrefix + documentId + ".md");
            sourcePath ??= "";
            var pathHash = Common.SimpleHash2(sourcePath);
            var pathFilename = Path.GetFileName(sourcePath);
            return Path.Combine(backupPath, $"{pathHash}_{pathFilename}");
        }

        public async Task<bool> Backup(string path, string markdown, string documentId = null)
        {
            try
            {
                // The backup is the last line of defence; a crash while it is being written must not leave half of it.
                await Utilities.SafeFile.WriteAllTextAtomicAsync(GetBackupFilePath(path, documentId), markdown);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetBackup(string path, string documentId = null)
        {
            try
            {
                return await File.ReadAllTextAsync(GetBackupFilePath(path, documentId));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Untitled documents' backups left behind (a crash), as document id and file.</summary>
        public System.Collections.Generic.List<(string documentId, string file)> UntitledBackups()
        {
            var list = new System.Collections.Generic.List<(string, string)>();
            try
            {
                if (!Directory.Exists(backupPath)) return list;
                foreach (var file in Directory.GetFiles(backupPath, UntitledPrefix + "*.md"))
                {
                    var name = Path.GetFileNameWithoutExtension(file).Substring(UntitledPrefix.Length);
                    if (name.Length == 32 && System.Linq.Enumerable.All(name, c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                        list.Add((name, file));
                }
                list.Sort((a, b) => File.GetLastWriteTimeUtc(a.Item2).CompareTo(File.GetLastWriteTimeUtc(b.Item2)));
            }
            catch
            {
                // Ignore: no recovery is better than a failed start.
            }
            return list;
        }

        public void DeleteBackup(string path, string documentId = null)
        {
            try
            {
                File.Delete(GetBackupFilePath(path, documentId));
            }
            catch
            {
                // Ignore
            }
        }
    }
}
