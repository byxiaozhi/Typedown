using System.IO;
using System.Threading.Tasks;
using Typedown.Core.Utilities;

namespace Typedown.Core.Services
{
    public class AutoBackup
    {
        private readonly string backupPath = Path.Combine(Config.GetLocalFolderPath(), "Backup");

        public string GetBackupFilePath(string sourcePath)
        {
            var dir = Path.GetDirectoryName(sourcePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            sourcePath ??= "";
            var pathHash = Common.SimpleHash2(sourcePath);
            var pathFilename = Path.GetFileName(sourcePath);
            return Path.Combine(backupPath, $"{pathHash}_{pathFilename}");
        }

        public async Task<bool> Backup(string path, string markdown)
        {
            try
            {
                await File.WriteAllTextAsync(GetBackupFilePath(path), markdown);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetBackup(string path)
        {
            try
            {
                return await File.ReadAllTextAsync(GetBackupFilePath(path));
            }
            catch
            {
                return null;
            }
        }

        public void DeleteBackup(string path)
        {
            try
            {
                File.Delete(GetBackupFilePath(path));
            }
            catch
            {
                // Ignore
            }
        }
    }
}
