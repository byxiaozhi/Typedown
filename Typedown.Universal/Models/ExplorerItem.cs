using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Typedown.Universal.Utilities;
using Windows.System;

namespace Typedown.Universal.Models
{
    public partial class ExplorerItem : INotifyPropertyChanged, IDisposable
    {
        public enum ExplorerItemType { None, Folder, File };

        public string Name { get; private set; }

        public string FullPath { get; set; }

        public ExplorerItemType Type { get; private set; }

        public ObservableCollection<ExplorerItem> Children { get; } = new();

        public Comparer<ExplorerItem> Comparer { get; set; } = new DefaultComparer();

        public Exception Exception { get; private set; }

        public bool IsExpanded { get; set; } = false;

        private bool IsWatching { get; set; } = false;

        private FileSystemWatcher fileSystemWatcher;

        public ExplorerItem()
        {
            PropertyChanged += OnExplorerItemPropertyChanged;
        }

        private void OnExplorerItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(FullPath):
                    UpdateName();
                    UpdateType();
                    UpdateChildren();
                    break;
                case nameof(IsExpanded):
                    OnIsExpandedChanged();
                    break;
                case nameof(IsWatching):
                    UpdateChildren();
                    break;
                case nameof(Comparer):
                    Reorder();
                    break;
            }
        }

        private void UpdateType()
        {
            if (string.IsNullOrEmpty(FullPath))
                Type = ExplorerItemType.None;
            else if (Directory.Exists(FullPath))
                Type = ExplorerItemType.Folder;
            else if (File.Exists(FullPath))
                Type = ExplorerItemType.File;
            else
                Type = ExplorerItemType.None;
        }

        private void UpdateName()
        {
            Name = Path.GetFileName(FullPath);
        }

        private void OnIsExpandedChanged()
        {
            if (IsExpanded)
            {
                IsWatching = true;
                foreach (var item in Children)
                    item.IsWatching = true;
            }
            else
            {
                foreach (var item in Children)
                {
                    item.IsExpanded = false;
                    item.IsWatching = false;
                }
            }
        }

        private async void UpdateChildren()
        {
            StopWatchFolder();
            try
            {
                if (IsWatching && Type == ExplorerItemType.Folder)
                {
                    var files = await Task.Run(() => EnumerateFilteredFileSystemInfos().ToList());
                    SetChildren(files.Select(x => CreateChild(x.Name)).ToList());
                    StartWatchFolder();
                }
                else
                {
                    ClearChildren();
                }
            }
            catch (Exception ex)
            {
                Exception = ex;
                IsWatching = false;
                IsExpanded = false;
                StopWatchFolder();
                ClearChildren();
            }
        }

        private void Reorder()
        {
            SetChildren(Children.ToList());
            foreach (var item in Children)
                item.Comparer = Comparer;
        }

        private void ClearChildren()
        {
            foreach (var item in Children)
                item.Dispose();
            Children.Clear();
        }

        private void SetChildren(List<ExplorerItem> children)
        {
            ClearChildren();
            foreach (var item in children.OrderBy(x => x, Comparer))
                Children.Add(item);
        }

        private void RemoveChildren(string name)
        {
            foreach (var item in Children.Where(x => x.Name == name).ToList())
            {
                item.Dispose();
                Children.Remove(item);
            }
        }

        private void AddChild(string name)
        {
            Children.InsertByOrder(CreateChild(name), Comparer.Compare);
        }

        private ExplorerItem CreateChild(string name)
        {
            return new() { FullPath = Path.Combine(FullPath, name), Comparer = Comparer, IsWatching = IsExpanded };
        }

        private bool ContainsChildren(string name)
        {
            return Children.Any(x => x.Name == name);
        }

        private static bool FileFilter(FileAttributes attr, string name)
        {
            if (attr.HasFlag(FileAttributes.Hidden) || attr.HasFlag(FileAttributes.System))
                return false;
            if (attr.HasFlag(FileAttributes.Directory))
                return true;
            return FileExtension.Markdown.Contains(Path.GetExtension(name));
        }

        private static async Task<FileAttributes?> GetFileAttributes(string path, int timeout = 1000)
        {
            var interval = 100;
            var count = timeout / interval;
            for (var i = 0; i < count; i++)
            {
                try
                {
                    return await Task.Run(() => File.GetAttributes(path));
                }
                catch (FileNotFoundException)
                {
                    await Task.Delay(interval);
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        private void StartWatchFolder()
        {
            if (Type != ExplorerItemType.Folder) return;
            fileSystemWatcher?.Dispose();
            fileSystemWatcher = new() { NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Attributes };
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            fileSystemWatcher.Created += (s, e) => dispatcherQueue.TryEnqueue(async () =>
            {
                var attr = await GetFileAttributes(Path.Combine(FullPath, e.Name));
                if (attr.HasValue) OnFileCreated(e, attr.Value);
            });
            fileSystemWatcher.Renamed += (s, e) => dispatcherQueue.TryEnqueue(async () =>
            {
                var attr = await GetFileAttributes(Path.Combine(FullPath, e.Name));
                if (attr.HasValue) OnFileRenamed(e, attr.Value);
            });
            fileSystemWatcher.Changed += (s, e) => dispatcherQueue.TryEnqueue(async () =>
            {
                var attr = await GetFileAttributes(Path.Combine(FullPath, e.Name));
                if (attr.HasValue) OnFileChanged(e, attr.Value);
            });
            fileSystemWatcher.Deleted += (s, e) => dispatcherQueue.TryEnqueue(() =>
            {
                OnFileDeleted(e);
            });
            fileSystemWatcher.Path = FullPath;
            fileSystemWatcher.EnableRaisingEvents = true;
        }

        private void StopWatchFolder()
        {
            fileSystemWatcher?.Dispose();
            fileSystemWatcher = null;
        }

        private void OnFileCreated(FileSystemEventArgs e, FileAttributes attr)
        {
            if (FileFilter(attr, e.Name))
                AddChild(e.Name);
        }

        private void OnFileRenamed(RenamedEventArgs e, FileAttributes attr)
        {
            RemoveChildren(e.OldName);
            OnFileCreated(e, attr);
        }

        private void OnFileChanged(FileSystemEventArgs e, FileAttributes attr)
        {
            var test = FileFilter(attr, e.Name);
            var contains = ContainsChildren(e.Name);
            if (test && !contains)
                AddChild(e.Name);
            else if (!test && contains)
                RemoveChildren(e.Name);
        }

        private void OnFileDeleted(FileSystemEventArgs e)
        {
            RemoveChildren(e.Name);
        }

        public void Dispose()
        {
            StopWatchFolder();
            ClearChildren();
        }

        public IEnumerable<FileSystemInfo> EnumerateFilteredFileSystemInfos()
        {
            if (Type == ExplorerItemType.Folder)
                return new DirectoryInfo(FullPath).EnumerateFileSystemInfos().Where(info => FileFilter(info.Attributes, info.Name));
            return null;
        }

        public class DefaultComparer : Comparer<ExplorerItem>
        {
            public override int Compare(ExplorerItem x, ExplorerItem y)
            {
                if (x.Type != y.Type)
                {
                    if (x.Type == ExplorerItemType.Folder)
                        return -1;
                    if (y.Type == ExplorerItemType.Folder)
                        return 1;
                }
                return x.Name.CompareTo(y.Name);
            }
        }
    }
}
