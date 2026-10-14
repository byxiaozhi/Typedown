using PropertyChanged;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.System;

namespace Typedown.Core.Models
{
    public partial class ExplorerItem : INotifyPropertyChanged, IDisposable
    {
        public enum ExplorerItemType { None, Folder, File };

        public string Name { get; internal set; }

        [OnChangedMethod(nameof(OnFullPathChanged))]
        public string FullPath { get; set; }

        public ExplorerItemType Type { get; private set; }

        public ObservableCollection<ExplorerItem> Children { get; } = new();

        [OnChangedMethod(nameof(OnComparerChanged))]
        public Comparer<ExplorerItem> Comparer { get; set; } = new DefaultComparer();

        public Func<FileAttributes, string, bool> Filter { get; set; } = DefaultFilter;

        public Exception Exception { get; private set; }

        [OnChangedMethod(nameof(OnIsExpandedChanged))]
        public bool IsExpanded { get; set; } = false;

        public bool IsSelected { get; set; } = false;

        [OnChangedMethod(nameof(OnIsWatchingChanged))]
        private bool IsWatching { get; set; } = false;

        private FileSystemWatcher fileSystemWatcher;
        private bool disposed;
        private int childrenUpdateVersion;

        private FileViewModel ViewModel { get; }

        private static readonly ConditionalWeakTable<FileViewModel, HashSet<string>> expandedFolder = new();

        // Expanding a folder marks every child for watching, so a folder with hundreds of subfolders would
        // start hundreds of directory listings at once and saturate the disk. Bound how many run together.
        private static readonly SemaphoreSlim enumerationGate = new(Math.Max(2, Environment.ProcessorCount));

        public ExplorerItem(FileViewModel viewModel)
        {
            ViewModel = viewModel;
        }

        /// <summary>True for a row that only carries a message, such as "this folder has too many items".</summary>
        public bool IsNotice => Notice != null;

        private void OnFullPathChanged()
        {
            UpdateName();
            UpdateType();
            UpdateChildren();
            var isExpanded = expandedFolder.GetOrCreateValue(ViewModel).Contains(FullPath);
            if (isExpanded) IsExpanded = true;
        }

        private void OnIsWatchingChanged()
        {
            UpdateChildren();
        }

        private void OnComparerChanged()
        {
            Reorder();
        }

        /// <summary>Set when the entry came from a folder listing, which already said what it is.</summary>
        private ExplorerItemType? knownType;

        private void UpdateType()
        {
            if (knownType is { } known)
            {
                Type = known;
                return;
            }
            if (string.IsNullOrEmpty(FullPath))
                Type = ExplorerItemType.None;
            else if (Directory.Exists(FullPath))
                Type = ExplorerItemType.Folder;
            else if (File.Exists(FullPath))
                Type = ExplorerItemType.File;
            else
                Type = ExplorerItemType.None;
        }

        /// <summary>Set on a row that stands in for content, such as a folder too large to list.</summary>
        public string Notice { get; init; }

        private void UpdateName()
        {
            Name = Notice ?? Path.GetFileName(FullPath);
        }

        private void OnIsExpandedChanged()
        {
            if (IsExpanded)
            {
                IsWatching = true;
                foreach (var item in Children)
                    item.IsWatching = true;
                expandedFolder.GetOrCreateValue(ViewModel).Add(FullPath);
            }
            else
            {
                foreach (var item in Children)
                {
                    item.IsExpanded = false;
                    item.IsWatching = false;
                }
                expandedFolder.GetOrCreateValue(ViewModel).Remove(FullPath);
            }
        }

        private async void UpdateChildren()
        {
            if (disposed) return;
            if (Type == ExplorerItemType.File)
            {
                // A file has nothing to enumerate; saying so here keeps tens of thousands of entries out of the
                // asynchronous path below.
                ClearChildren();
                return;
            }
            var updateVersion = ++childrenUpdateVersion;
            var path = FullPath;
            var filter = Filter;
            StopWatchFolder();
            Exception = null;
            try
            {
                if (IsWatching && Type == ExplorerItemType.Folder)
                {
                    List<FileSystemInfo> files;
                    await enumerationGate.WaitAsync();
                    try
                    {
                        // The request may have waited in the queue while this node was collapsed or navigated
                        // away from; skip the scan entirely rather than read a directory that is no longer shown.
                        if (disposed || updateVersion != childrenUpdateVersion)
                            return;
                        files = await Task.Run(() => new DirectoryInfo(path).EnumerateFileSystemInfos()
                            .Where(info => filter(info.Attributes, info.Name)).ToList());
                    }
                    finally
                    {
                        // Release before the UI update below so the slot is not held while children are built.
                        enumerationGate.Release();
                    }
                    // A previous folder enumeration can finish after navigation,
                    // collapse or disposal. It must not repopulate this node.
                    if (disposed || updateVersion != childrenUpdateVersion)
                    {
                        Log.Debug($"ExplorerItem.UpdateChildren: stale result for '{path}' (disposed={disposed}, v{updateVersion} != v{childrenUpdateVersion})");
                        return;
                    }
                    Log.Debug($"ExplorerItem.UpdateChildren: '{path}' -> {files.Count} entries");
                    if (files.Count > TooManyEntries)
                    {
                        // Tens of thousands of entries cost more than they are worth: every one of them is a
                        // view model the tree keeps, and on a synced folder the listing alone takes minutes,
                        // with everything else — opening a document included — waiting behind it.
                        SetChildren(new List<ExplorerItem> { CreateNotice(string.Format(Locale.GetString("FolderPane.TooManyEntries") ?? "{0} items", files.Count)) });
                        StopWatchFolder();
                        return;
                    }
                    SetChildren(files.Select(x => CreateChild(x.Name,
                        x.Attributes.HasFlag(FileAttributes.Directory) ? ExplorerItemType.Folder : ExplorerItemType.File)).ToList());
                    try
                    {
                        StartWatchFolder();
                    }
                    catch (Exception ex)
                    {
                        // A watcher failure must not hide successfully read files.
                        StopWatchFolder();
                        Exception = ex;
                    }
                }
                else
                {
                    ClearChildren();
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"ExplorerItem.UpdateChildren: '{path}' failed: {ex.GetType().Name}: {ex.Message}");
                if (disposed || updateVersion != childrenUpdateVersion) return;
                IsWatching = false;
                IsExpanded = false;
                StopWatchFolder();
                ClearChildren();
                Exception = ex;
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
            // Reordering passes existing nodes back in; keep their watchers alive.
            var retained = new HashSet<ExplorerItem>(children);
            foreach (var item in Children)
                if (!retained.Contains(item)) item.Dispose();
            Children.Clear();
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

        /// <summary>Beyond this many entries a folder is described rather than listed.</summary>
        private const int TooManyEntries = 2000;

        /// <summary>A row that only says something; it has no path, so nothing opens it.</summary>
        private ExplorerItem CreateNotice(string text)
        {
            // No path, so nothing sets the name from one: the row carries its own text.
            return new(ViewModel) { knownType = ExplorerItemType.File, Notice = text, Comparer = Comparer, Name = text };
        }

        private ExplorerItem CreateChild(string name, ExplorerItemType? type = null)
        {
            return new(ViewModel) { knownType = type, FullPath = Path.Combine(FullPath, name), Comparer = Comparer, IsWatching = IsExpanded };
        }

        private bool ContainsChildren(string name)
        {
            return Children.Any(x => x.Name == name);
        }

        private static async Task<FileAttributes?> GetFileAttributes(string path, int timeout = 1000)
        {
            var interval = 100;
            var count = timeout / interval;
            for (var i = 0; i < count; i++)
            {
                try
                {
                    return File.GetAttributes(path);
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
            var watcher = fileSystemWatcher;
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            fileSystemWatcher.Created += async (s, e) =>
            {
                if (e?.Name == null) return;
                var attr = await GetFileAttributes(e.FullPath);
                if (attr.HasValue) dispatcherQueue?.TryEnqueue(() =>
                {
                    if (!disposed && fileSystemWatcher == watcher) OnFileCreated(e, attr.Value);
                });
            };
            fileSystemWatcher.Renamed += async (s, e) =>
            {
                if (e?.Name == null) return;
                var attr = await GetFileAttributes(e.FullPath);
                if (attr.HasValue) dispatcherQueue?.TryEnqueue(() =>
                {
                    if (!disposed && fileSystemWatcher == watcher) OnFileRenamed(e, attr.Value);
                });
            };
            fileSystemWatcher.Changed += async (s, e) =>
            {
                if (e?.Name == null) return;
                var attr = await GetFileAttributes(e.FullPath);
                if (attr.HasValue) dispatcherQueue?.TryEnqueue(() =>
                {
                    if (!disposed && fileSystemWatcher == watcher) OnFileChanged(e, attr.Value);
                });
            };
            fileSystemWatcher.Deleted += (s, e) =>
            {
                if (e?.Name == null) return;
                dispatcherQueue?.TryEnqueue(() =>
                {
                    if (!disposed && fileSystemWatcher == watcher) OnFileDeleted(e);
                });
            };
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
            if (Filter(attr, e.Name) && !Children.Any(x => x.Name == e.Name))
                AddChild(e.Name);
        }

        private void OnFileRenamed(RenamedEventArgs e, FileAttributes attr)
        {
            RemoveChildren(e.OldName);
            OnFileCreated(e, attr);
        }

        [SuppressPropertyChangedWarnings]
        private void OnFileChanged(FileSystemEventArgs e, FileAttributes attr)
        {
            var test = Filter(attr, e.Name);
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
            if (disposed) return;
            disposed = true;
            childrenUpdateVersion++;
            StopWatchFolder();
            ClearChildren();
        }

        private static bool DefaultFilter(FileAttributes attr, string name)
        {
            if (attr.HasFlag(FileAttributes.Hidden) || attr.HasFlag(FileAttributes.System))
                return false;
            if (attr.HasFlag(FileAttributes.Directory))
                return true;
            return FileTypeHelper.IsEditableTextFile(name);
        }

        private class DefaultComparer : Comparer<ExplorerItem>
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
