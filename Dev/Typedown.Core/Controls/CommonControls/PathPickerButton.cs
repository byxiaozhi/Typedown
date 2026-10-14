using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;
using Typedown.Core.Utilities;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls
{
    public class PathPickerButton : Button
    {
        public static DependencyProperty PathProperty = DependencyProperty.Register(nameof(Path), typeof(string), typeof(PathPickerButton), new(""));
        public string Path { get => (string)GetValue(PathProperty); set => SetValue(PathProperty, value.Replace("\\", "/")); }

        public static DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(PathPickMode), typeof(PathPickerButton), new(PathPickMode.File));
        public PathPickMode Mode { get => (PathPickMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

        public static DependencyProperty FileTypeFilterProperty = DependencyProperty.Register(nameof(FileTypeFilter), typeof(IEnumerable<string>), typeof(PathPickerButton), new(PathPickMode.File));
        public IEnumerable<string> FileTypeFilter { get => (IEnumerable<string>)GetValue(FileTypeFilterProperty); set => SetValue(FileTypeFilterProperty, value); }

        /// <summary>File mode: several files may be picked; Path is the first, PickedEventArgs.Paths all of them.</summary>
        public bool AllowMultiple { get; set; }

        public event EventHandler<PickedEventArgs> Picked;

        private nint Window => this.GetService<IWindowService>().GetWindow(this);

        public bool IsPicking { get; private set; }

        public PathPickerButton()
        {
            Style = Application.Current.Resources["DefaultButtonStyle"] as Style;
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
                var filePicker = new FileOpenPicker();
                FileTypeFilter.ToList().ForEach(filePicker.FileTypeFilter.Add);
                filePicker.SetOwnerWindow(Window);
                var paths = AllowMultiple
                    ? ((await FilePickersExtensions.ShowAsync(() => filePicker.PickMultipleFilesAsync()))?.Select(f => f.Path) ?? Enumerable.Empty<string>()).ToList()
                    : new[] { (await FilePickersExtensions.ShowAsync(() => filePicker.PickSingleFileAsync()))?.Path }.Where(p => p != null).ToList();
                var isCancel = paths.Count == 0;
                if (!isCancel) Path = paths[0];
                Picked?.Invoke(this, new(isCancel, paths.FirstOrDefault(), paths));
            }
            catch (Exception ex)
            {
                await AppContentDialog.Create(Locale.GetString("Error"), ex.Message, Locale.GetDialogString("Ok")).ShowAsync(XamlRoot);
            }
        }

        private async Task PickFolder()
        {
            try
            {
                var folderPicker = new FolderPicker();
                folderPicker.SetOwnerWindow(Window);
                folderPicker.FileTypeFilter.Add("*");
                var folder = await FilePickersExtensions.ShowAsync(() => folderPicker.PickSingleFolderAsync());
                var isCancel = folder is null;
                if (!isCancel) Path = folder.Path;
                Picked?.Invoke(this, new(isCancel, folder?.Path));
            }
            catch (Exception ex)
            {
                await AppContentDialog.Create(Locale.GetString("Error"), ex.Message, Locale.GetDialogString("Ok")).ShowAsync(XamlRoot);
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

        /// <summary>Every picked file (one unless AllowMultiple).</summary>
        public IReadOnlyList<string> Paths { get; }

        public PickedEventArgs(bool isCancel, string path, IReadOnlyList<string> paths = null)
        {
            IsCancel = isCancel;
            Path = path;
            Paths = paths ?? (path == null ? new string[0] : new[] { path });
        }
    }
}
