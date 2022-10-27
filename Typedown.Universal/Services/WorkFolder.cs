using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Models;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.System;

namespace Typedown.Universal.Services
{
    public class WorkFolder : IDisposable
    {
        private SettingsViewModel Settings { get; }

        private AppViewModel ViewModel { get; }

        public ObservableCollection<FolderItemModel> TreeViewItems { get; }
        public FolderItemModel RootItem { get; }

        private readonly string folderGlyph = ((char)short.Parse("f12b", NumberStyles.AllowHexSpecifier)).ToString();

        private readonly string fileGlyph = ((char)short.Parse("e8a5", NumberStyles.AllowHexSpecifier)).ToString();

        public WorkFolder(SettingsViewModel settings, AppViewModel viewModel)
        {
            Settings = settings;
            ViewModel = viewModel;
            TreeViewItems = new ObservableCollection<FolderItemModel>();
            Settings.PropertyChanged += Settings_PropertyChanged;
            RootItem = new FolderItemModel();
            InitWorkFolder();
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.WorkFolder) && Settings.WorkFolder != RootItem.Path)
            {
                InitWorkFolder();
            }
        }

        private void AddChild(FolderItemModel folder, FolderItemModel child)
        {
            var count = folder.Children.Count;
            for (var i = 0; i < count; i++)
            {
                var oldItem = (folder.Children[i] as FolderItemModel);
                if ((child.Type == oldItem.Type && string.Compare(oldItem.Name, child.Name) > 0) || (child.Type == FolderItemModel.ItemType.folder && oldItem.Type == FolderItemModel.ItemType.file))
                {
                    folder.Children.Insert(i, child);
                    if (folder == RootItem) TreeViewItems.Insert(i, child);
                    return;
                }
            }
            folder.Children.Add(child);
            if (folder == RootItem) TreeViewItems.Add(child);
        }

        private void OnFileCreated(FolderItemModel folder, FileSystemEventArgs e, FileAttributes attr)
        {
            if (FileFilter(attr, e.Name))
            {
                if (folder.IsExpanded || folder == RootItem)
                {
                    if (!folder.Children.Where(x => (x as FolderItemModel).Path == e.FullPath).Any())
                    {
                        AddChild(folder, CreateFolderItem(e.FullPath));
                    }
                }
                else
                {
                    ScanWorkFolder(folder, false);
                }
            }
        }

        private void OnFileDeleted(FolderItemModel folder, FileSystemEventArgs e)
        {
            if (folder.IsExpanded || folder == RootItem)
            {
                foreach (var item in folder.Children.Where(x => (x as FolderItemModel).Path == e.FullPath).ToList())
                {
                    (item as FolderItemModel).Dispose();
                    folder.Children.Remove(item);
                    if (folder == RootItem)
                    {
                        TreeViewItems.Remove(item as FolderItemModel);
                    }
                }
            }
            else
            {
                ScanWorkFolder(folder, false);
            }
        }

        private void OnFileRenamed(FolderItemModel folder, RenamedEventArgs e, FileAttributes attr)
        {
            if (folder.IsExpanded || folder == RootItem)
            {
                var items = folder.Children.Where(x => (x as FolderItemModel).Path == e.OldFullPath).Select(x => x as FolderItemModel).ToList();
                if (items.Count > 0)
                {
                    for (var i = 1; i < items.Count; i++)
                    {
                        items[i].Dispose();
                        folder.Children.Remove(items[i]);
                        if (folder == RootItem)
                        {
                            TreeViewItems.Remove(items[i]);
                        }
                    }
                    var item = items[0];
                    folder.Children.Remove(item);
                    if (folder == RootItem)
                    {
                        TreeViewItems.Remove(item);
                    }
                    if (FileFilter(attr, e.Name))
                    {
                        item.Path = e.FullPath;
                        item.Name = e.Name;
                        AddChild(folder, item);
                        if (folder.Children.Count == 1 || (folder.Children[0] as FolderItemModel).Type != FolderItemModel.ItemType.loading)
                        {
                            folder.IsExpanded = true;
                        }
                    }
                    else
                    {
                        item.Dispose();
                    }
                }
                else
                {
                    OnFileCreated(folder, e, attr);
                }
            }
            else
            {
                ScanWorkFolder(folder, false);
            }
        }

        private void OnFileChanged(FolderItemModel folder, FileSystemEventArgs e, FileAttributes attr)
        {
            if (folder.IsExpanded || folder == RootItem)
            {
                var filter = FileFilter(attr, e.Name);
                var contains = folder.Children.Where(x => (x as FolderItemModel).Path == e.FullPath).Any();
                if (filter && !contains)
                {
                    OnFileCreated(folder, e, attr);
                }
                else if (!filter && contains)
                {
                    OnFileDeleted(folder, e);
                }
            }
            else
            {
                ScanWorkFolder(folder, false);
            }
        }

        public static async Task<FileAttributes?> WaitFileAttributes(string path, int timeout = 3000)
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

        private void StartWatchFolder(FolderItemModel folder)
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            if (folder.FileSystemWatcher != null) folder.FileSystemWatcher.Dispose();
            folder.FileSystemWatcher = new();
            folder.FileSystemWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Attributes;
            folder.FileSystemWatcher.Created += (s, e) => dispatcherQueue.TryEnqueue(async () =>
            {
                var attr = await WaitFileAttributes(Path.Combine(folder.Path, e.Name));
                if (attr.HasValue) OnFileCreated(folder, e, attr.Value);
            });
            folder.FileSystemWatcher.Renamed += (s, e) => dispatcherQueue.TryEnqueue(async () =>
            {
                var attr = await WaitFileAttributes(Path.Combine(folder.Path, e.Name));
                if (attr.HasValue) OnFileRenamed(folder, e, attr.Value);
            });
            folder.FileSystemWatcher.Changed += (s, e) => dispatcherQueue.TryEnqueue(async () =>
            {
                var attr = await WaitFileAttributes(Path.Combine(folder.Path, e.Name));
                if (attr.HasValue) OnFileCreated(folder, e, attr.Value);
            });
            folder.FileSystemWatcher.Deleted += (s, e) => dispatcherQueue.TryEnqueue(() =>
            {
                OnFileDeleted(folder, e);
            });
            folder.FileSystemWatcher.Path = folder.Path;
            folder.FileSystemWatcher.EnableRaisingEvents = true;
        }

        private bool IsDirectory(String path)
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.Directory);
        }

        private FolderItemModel CreateFolderItem(String path)
        {
            return IsDirectory(path) ? CreateFolderItem(new DirectoryInfo(path)) : CreateFolderItem(new FileInfo(path));
        }

        private FolderItemModel CreateFolderItem(FileSystemInfo item)
        {
            var isFolder = item.Attributes.HasFlag(FileAttributes.Directory);
            FolderItemModel fileItem = new();
            fileItem.Name = item.Name;
            fileItem.Path = item.FullName;
            fileItem.Glyph = isFolder ? folderGlyph : fileGlyph;
            fileItem.Type = isFolder ? FolderItemModel.ItemType.folder : FolderItemModel.ItemType.file;
            fileItem.Opened = item.FullName == ViewModel.FileViewModel.FilePath;
            ScanWorkFolder(fileItem, false);
            return fileItem;
        }

        private bool FileFilter(FileSystemInfo file)
        {
            return FileFilter(file.Attributes, file.Name);
        }

        private bool FileFilter(FileAttributes attr, string name)
        {
            var attrTest = !attr.HasFlag(FileAttributes.Hidden) && !attr.HasFlag(FileAttributes.System);
            var dirTest = attr.HasFlag(FileAttributes.Directory);
            var typeTest = FileExtension.Markdown.Where(x => name.ToLower().EndsWith(x)).Any();
            return attrTest && (typeTest || dirTest);
        }

        public void ScanWorkFolder(FolderItemModel parent, bool full = true)
        {
            try
            {
                parent.Clear();
                DirectoryInfo directoryInfo = new(parent.Path);
                if (!directoryInfo.Exists) return;
                var directories = directoryInfo.EnumerateDirectories().Where(FileFilter);
                var files = directoryInfo.EnumerateFiles().Where(FileFilter);
                if (full)
                {
                    directories.OrderBy(x => x.Name).Select(CreateFolderItem).ToList().ForEach(parent.Children.Add);
                    files.OrderBy(x => x.Name).Select(CreateFolderItem).ToList().ForEach(parent.Children.Add);
                }
                else if (directories.Any() || files.Any())
                {
                    parent.Children.Add(new FolderItemModel()
                    {
                        Type = FolderItemModel.ItemType.loading
                    });
                }
                StartWatchFolder(parent);
            }
            catch { }
        }

        private void InitWorkFolder()
        {
            RootItem.Path = Settings.WorkFolder;
            ScanWorkFolder(RootItem);
            TreeViewItems.Clear();
            RootItem.Children.ToList().ForEach(x => TreeViewItems.Add(x as FolderItemModel));
        }

        public void Dispose()
        {
            Settings.PropertyChanged -= Settings_PropertyChanged;
            RootItem.Clear();
        }
    }
}
