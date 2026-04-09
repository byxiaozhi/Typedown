using Avalonia.Interactivity;
using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;
using Typedown.Core.Utilities;
using Avalonia.Platform.Storage;
using Avalonia;
using Avalonia.Controls;

namespace Typedown.Core.Controls
{
    public class PathPickerButton : Button
    {
        public static AvaloniaProperty PathProperty = AvaloniaProperty.Register<PathPickerButton, string>(nameof(Path), "");
        public string Path { get => (string)GetValue(PathProperty); set => SetValue(PathProperty, value.Replace("\\", "/")); }

        public static AvaloniaProperty ModeProperty = AvaloniaProperty.Register<PathPickerButton, PathPickMode>(nameof(Mode), PathPickMode.File);
        public PathPickMode Mode { get => (PathPickMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

        public static AvaloniaProperty FileTypeFilterProperty = AvaloniaProperty.Register<PathPickerButton, IEnumerable<string>>(nameof(FileTypeFilter), null);
        public IEnumerable<string> FileTypeFilter { get => (IEnumerable<string>)GetValue(FileTypeFilterProperty); set => SetValue(FileTypeFilterProperty, value); }

        public event EventHandler<PickedEventArgs> Picked;



        public bool IsPicking { get; private set; }

        public PathPickerButton()
        {
            Classes.Add("DefaultButtonStyle");
            FileTypeFilter = new List<string>();
            Click += OnPathPickerButtonClick;
        }

        private async void OnPathPickerButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                IsPicking = true;
                switch (Mode)
                {
                    case PathPickMode.File:
                        await PickFile();
                        break;
                    case PathPickMode.Folder:
                        await PickFolder();
                        break;
                }
            }
            finally
            {
                IsPicking = false;
            }
        }

        private async Task PickFile()
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    AllowMultiple = false
                });
                var file = files.FirstOrDefault();
                var isCancel = file is null;
                if (!isCancel) Path = file.TryGetLocalPath() ?? file.Path.ToString();
                Picked?.Invoke(this, new(isCancel, isCancel ? null : Path));
            }
            catch (Exception ex)
            {
                var dialog = AppContentDialog.Create(Locale.GetString("Error"), ex.Message, Locale.GetDialogString("Ok"));
                dialog.XamlRoot = this;
                await dialog.ShowAsync();
            }
        }

        private async Task PickFolder()
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    AllowMultiple = false
                });
                var folder = folders.FirstOrDefault();
                var isCancel = folder is null;
                if (!isCancel) Path = folder.TryGetLocalPath() ?? folder.Path.ToString();
                Picked?.Invoke(this, new(isCancel, isCancel ? null : Path));
            }
            catch (Exception ex)
            {
                var dialog = AppContentDialog.Create(Locale.GetString("Error"), ex.Message, Locale.GetDialogString("Ok"));
                dialog.XamlRoot = this;
                await dialog.ShowAsync();
            }
        }

        public enum PathPickMode
        {
            File,
            Folder,
        }
    }

    public class PickedEventArgs
    {
        public bool IsCancel { get; }

        public string Path { get; }

        public PickedEventArgs(bool isCancel, string path)
        {
            IsCancel = isCancel;
            Path = path;
        }
    }
}

