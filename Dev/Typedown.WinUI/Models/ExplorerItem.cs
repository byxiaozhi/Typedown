using Microsoft.UI.Dispatching;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI.Models
{
    // Ported from Typedown.Core\Models\RuntimeModels\ExplorerItem.cs: a lazily-expanding folder tree
    // node with live FileSystemWatcher updates. Replaces this port's earlier flat pre-scanned list
    // (FolderFileEntry + ScanFolder in MainWindow.xaml.cs, both now gone) with real nested
    // expand/collapse and externally-made changes (a file created/renamed/deleted outside the app)
    // showing up without reopening the folder.
    //
    // Simplified from the original:
    //   - No ConditionalWeakTable-keyed-by-FileViewModel remembered-expanded-folders — this app is
    //     single-window, so the caller just passes one shared HashSet down through the whole tree.
    //   - Watcher events trigger a full re-scan of the changed folder instead of the original's
    //     per-event GetFileAttributes-with-retry + single-entry add/remove — simpler, and cheap enough
    //     for a markdown-project-sized directory; a folder with hundreds of files changing constantly
    //     isn't really this app's use case.
    //   - No separate IFileOperation/IClipboard service classes — clipboard cut/copy/paste and
    //     item-to-item drag-drop are wired directly in MainWindow.xaml.cs instead (see its Folder
    //     tree clipboard & drag-drop region), using Windows.ApplicationModel.DataTransfer directly
    //     rather than reproducing the original's own service-interface layer.
    public class ExplorerItem : INotifyPropertyChanged, IDisposable
    {
        public enum ExplorerItemType { None, Folder, File }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name { get; private set; } = "";

        [OnChangedMethod(nameof(OnFullPathChanged))]
        public string FullPath { get; set; }

        public ExplorerItemType Type { get; private set; }

        public ObservableCollection<ExplorerItem> Children { get; } = new();

        [OnChangedMethod(nameof(OnIsExpandedChanged))]
        public bool IsExpanded { get; set; }

        public bool IsSelected { get; set; }

        [OnChangedMethod(nameof(OnIsWatchingChanged))]
        private bool IsWatching { get; set; }

        private readonly HashSet<string> expandedPaths;
        private readonly DispatcherQueue dispatcherQueue;
        private FileSystemWatcher watcher;

        public ExplorerItem(HashSet<string> expandedPaths, DispatcherQueue dispatcherQueue)
        {
            this.expandedPaths = expandedPaths;
            this.dispatcherQueue = dispatcherQueue;
        }

        private ExplorerItem CreateChild(string name) =>
            new(expandedPaths, dispatcherQueue) { FullPath = Path.Combine(FullPath, name) };

        private void OnFullPathChanged()
        {
            var dirName = string.IsNullOrEmpty(FullPath) ? "" : new DirectoryInfo(FullPath).Name;
            Name = string.IsNullOrEmpty(dirName) ? FullPath ?? "" : dirName; // root-of-drive paths ("C:\") have no DirectoryInfo.Name
            Type = string.IsNullOrEmpty(FullPath) ? ExplorerItemType.None
                 : Directory.Exists(FullPath) ? ExplorerItemType.Folder
                 : File.Exists(FullPath) ? ExplorerItemType.File
                 : ExplorerItemType.None;
            if (Type == ExplorerItemType.Folder && expandedPaths.Contains(FullPath))
                IsExpanded = true;
            else if (IsWatching)
                _ = Reload(); // FullPath reassigned while already watching (e.g. root folder switched)
        }

        private void OnIsExpandedChanged()
        {
            if (IsExpanded)
            {
                expandedPaths.Add(FullPath);
                IsWatching = true;
            }
            else
            {
                expandedPaths.Remove(FullPath);
                foreach (var child in Children) child.IsExpanded = false;
                IsWatching = false;
            }
        }

        private void OnIsWatchingChanged() => _ = Reload();

        private async Task Reload()
        {
            StopWatch();
            if (IsWatching && Type == ExplorerItemType.Folder)
            {
                List<string> names;
                try
                {
                    names = await Task.Run(() => new DirectoryInfo(FullPath)
                        .EnumerateFileSystemInfos()
                        .Where(info => PassesFilter(info.Attributes, info.Name))
                        .Select(info => info.Name)
                        .ToList());
                }
                catch
                {
                    names = new List<string>();
                }
                SetChildren(names);
                StartWatch();
            }
            else
            {
                ClearChildren();
            }
        }

        private void SetChildren(List<string> names)
        {
            var wanted = new HashSet<string>(names);
            foreach (var stale in Children.Where(c => !wanted.Contains(c.Name)).ToList())
            {
                stale.Dispose();
                Children.Remove(stale);
            }
            var existing = new HashSet<string>(Children.Select(c => c.Name));
            foreach (var name in names.Where(n => !existing.Contains(n)))
                InsertSorted(CreateChild(name));
        }

        private void InsertSorted(ExplorerItem item)
        {
            var index = 0;
            while (index < Children.Count && Compare(Children[index], item) < 0) index++;
            Children.Insert(index, item);
        }

        // Folders before files, then alphabetical — same as the original's DefaultComparer.
        private static int Compare(ExplorerItem a, ExplorerItem b)
        {
            if (a.Type != b.Type)
            {
                if (a.Type == ExplorerItemType.Folder) return -1;
                if (b.Type == ExplorerItemType.Folder) return 1;
            }
            return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        }

        private void ClearChildren()
        {
            foreach (var item in Children) item.Dispose();
            Children.Clear();
        }

        private void StartWatch()
        {
            if (Type != ExplorerItemType.Folder) return;
            try
            {
                watcher = new FileSystemWatcher(FullPath) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName };
                watcher.Created += (s, e) => dispatcherQueue.TryEnqueue(() => _ = Reload());
                watcher.Renamed += (s, e) => dispatcherQueue.TryEnqueue(() => _ = Reload());
                watcher.Deleted += (s, e) => dispatcherQueue.TryEnqueue(() => _ = Reload());
                watcher.EnableRaisingEvents = true;
            }
            catch
            {
                // A folder that disappears/becomes inaccessible between the enumeration above and here
                // just doesn't get watched — not fatal, matches the original's best-effort approach.
            }
        }

        private void StopWatch()
        {
            watcher?.Dispose();
            watcher = null;
        }

        private static bool PassesFilter(FileAttributes attr, string name)
        {
            if (attr.HasFlag(FileAttributes.Hidden) || attr.HasFlag(FileAttributes.System)) return false;
            // Not in the original's DefaultFilter, kept from this port's earlier flat-scan version:
            // dotfolders (.git etc.) and node_modules are the only two conventionally-huge,
            // never-relevant directories worth hardcoding an exclusion for.
            if (name.StartsWith(".") || name == "node_modules") return false;
            if (attr.HasFlag(FileAttributes.Directory)) return true;
            return FileTypeHelper.IsMarkdownFile(name);
        }

        public void Dispose()
        {
            StopWatch();
            ClearChildren();
        }
    }
}
