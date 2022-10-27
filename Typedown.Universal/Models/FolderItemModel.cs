using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using System.IO;
using Typedown.Universal.ViewModels;

namespace Typedown.Universal.Models
{
    public class FolderItemModel : TreeViewNode, INotifyPropertyChanged, IDisposable
    {
        public enum ItemType
        {
            folder = 0,
            file = 1,
            loading = 2
        }
        public FileSystemWatcher FileSystemWatcher { get; set; } = null;
        public string Name { get; set; }
        public string Path { get; set; }
        public string Glyph { get; set; }
        public ItemType Type { get; set; }
        public bool Opened { get; set; }

        public FileViewModel FileViewModel { get; }

        public void Clear()
        {
            if (FileSystemWatcher != null)
            {
                FileSystemWatcher.Dispose();
                FileSystemWatcher = null;
                foreach (var item in Children)
                    (item as FolderItemModel).Clear();
                Children.Clear();
            }
        }

        static public void UpdateSelectedItem(FileViewModel fileViewModel)
        {
            UpdateSelectedItem(fileViewModel, fileViewModel.WorkFolder.RootItem);
        }

        static public void UpdateSelectedItem(FileViewModel fileViewModel, FolderItemModel item)
        {
            if (item.Type == FolderItemModel.ItemType.file)
            {
                var opened = item.Path == fileViewModel.FilePath;
                if (item.Opened != opened)
                    item.Opened = opened;
            }
            else if (item.Type == FolderItemModel.ItemType.folder)
            {
                foreach (var child in item.Children)
                    UpdateSelectedItem(fileViewModel, child as FolderItemModel);
            }
        }

#pragma warning disable CS0067
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067

        public void Dispose()
        {
            Clear();
        }
    }
}
