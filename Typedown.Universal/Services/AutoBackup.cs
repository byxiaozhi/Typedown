using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Services
{
    public class AutoBackup
    {
        public class Content
        {
            public string Text { get; set; }
        }

        public class Metadata
        {
            public string File { get; set; }
        }

        Dictionary<string, Metadata> Metadatas { get; set; }

        private readonly string backupPath;

        private readonly string metadataFilePath;

        private readonly string metadataFilePath2;

        public AutoBackup()
        {
            try
            {
                backupPath = Path.Combine(Windows.Storage.ApplicationData.Current.LocalCacheFolder.Path, "Backup");
            }
            catch (Exception)
            {
                backupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Typedown", "Backup");
            }
            metadataFilePath = Path.Combine(backupPath, "metadata");
            metadataFilePath2 = Path.Combine(backupPath, "metadata2");
        }

        public async void WriteMetadata()
        {
            if (Metadatas == null) return;
            try
            {
                await File.WriteAllTextAsync(metadataFilePath2, JsonConvert.SerializeObject(Metadatas));
                File.Delete(metadataFilePath);
                File.Move(metadataFilePath2, metadataFilePath);
            }
            catch { }
        }

        public async Task LoadMetadata()
        {
            Metadatas = null;
            try
            {
                if (File.Exists(metadataFilePath2))
                    Metadatas = JsonConvert.DeserializeObject<Dictionary<string, Metadata>>(await File.ReadAllTextAsync(metadataFilePath2));
            }
            catch { }
            if (Metadatas == null)
            {
                try
                {
                    if (File.Exists(metadataFilePath))
                        Metadatas = JsonConvert.DeserializeObject<Dictionary<string, Metadata>>(await File.ReadAllTextAsync(metadataFilePath));
                }
                catch { }
            }
            if (Metadatas == null)
            {
                if (!Directory.Exists(backupPath))
                {
                    Directory.CreateDirectory(backupPath);
                }
                Metadatas = new Dictionary<string, Metadata>();
            }
        }

        private async Task EnsureMetadataLoaded()
        {
            if (Metadatas == null)
            {
                await LoadMetadata();
            }
        }

        public async Task<bool> Backup(string path, string Markdown)
        {
            await EnsureMetadataLoaded();
            if (path == null) path = "";
            try
            {
                string filename;
                if (Metadatas.ContainsKey(path))
                {
                    filename = Metadatas[path].File;
                }
                else
                {
                    filename = Guid.NewGuid().ToString();
                    var metadata = new Metadata
                    {
                        File = filename
                    };
                    Metadatas.Add(path, metadata);
                    WriteMetadata();
                }
                var content = new Content()
                {
                    Text = Markdown
                };
                await File.WriteAllTextAsync(Path.Combine(backupPath, filename), JsonConvert.SerializeObject(content));
                return true;
            }
            catch { }
            return false;
        }

        public async Task<string> GetBackup(string path)
        {
            await EnsureMetadataLoaded();
            if (path == null) path = "";
            if (Metadatas.ContainsKey(path))
            {
                var filename = Metadatas[path].File;
                try
                {
                    var content = JsonConvert.DeserializeObject<Content>(await File.ReadAllTextAsync(Path.Combine(backupPath, filename)));
                    return content.Text;
                }
                catch
                {
                    DeleteBackup(path);
                }
            }
            return null;
        }

        public async void DeleteBackup(string path)
        {
            await EnsureMetadataLoaded();
            if (path == null) path = "";
            if (Metadatas.ContainsKey(path))
            {
                try
                {
                    var filename = Metadatas[path].File;
                    Metadatas.Remove(path);
                    File.Delete(Path.Combine(backupPath, Path.Combine(backupPath, filename)));
                }
                catch { }
                WriteMetadata();
            }
        }
    }
}
