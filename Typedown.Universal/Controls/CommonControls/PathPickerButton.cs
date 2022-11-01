using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.Controls
{
    public class PathPickerButton : Button
    {
        public static DependencyProperty PathProperty = DependencyProperty.Register(nameof(Path), typeof(string), typeof(EnumNameBlock), new(""));
        public string Path { get => (string)GetValue(PathProperty); set => SetValue(PathProperty, value.Replace("\\", "/")); }

        public static DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(PathPickMode), typeof(EnumNameBlock), new(PathPickMode.File));
        public PathPickMode Mode { get => (PathPickMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

        public static DependencyProperty FileTypeFilterProperty = DependencyProperty.Register(nameof(FileTypeFilter), typeof(IEnumerable<string>), typeof(EnumNameBlock), new(PathPickMode.File));
        public IEnumerable<string> FileTypeFilter { get => (IEnumerable<string>)GetValue(FileTypeFilterProperty); set => SetValue(FileTypeFilterProperty, value); }

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
            var filePicker = new FileOpenPicker();
            FileTypeFilter.ToList().ForEach(filePicker.FileTypeFilter.Add);
            filePicker.SetOwnerWindow(Window);
            var file = await filePicker.PickSingleFileAsync();
            if (file != null)
                Path = file.Path;
        }

        private async Task PickFolder()
        {
            var folderPicker = new FolderPicker();
            folderPicker.SetOwnerWindow(Window);
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
                Path = folder.Path;
        }

        public enum PathPickMode
        {
            File,
            Folder,
        }
    }
}
