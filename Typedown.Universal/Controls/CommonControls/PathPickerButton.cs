using System;
using System.Collections.Generic;
using System.Linq;
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
        public string Path { get => (string)GetValue(PathProperty); set => SetValue(PathProperty, value.Replace("\\","/")); }

        public static DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(PathPickMode), typeof(EnumNameBlock), new(PathPickMode.File));
        public PathPickMode Mode { get => (PathPickMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

        public static DependencyProperty FileTypeFilterProperty = DependencyProperty.Register(nameof(FileTypeFilter), typeof(IList<string>), typeof(EnumNameBlock), new(PathPickMode.File));
        public IList<string> FileTypeFilter { get => (IList<string>)GetValue(FileTypeFilterProperty); set => SetValue(FileTypeFilterProperty, value); }

        private nint Window => this.GetService<IWindowService>().GetWindow(this);

        public PathPickerButton()
        {
            Style = Application.Current.Resources["DefaultButtonStyle"] as Style;
            FileTypeFilter = new List<string>();
            Click += PathPickerButton_Click;
        }

        private void PathPickerButton_Click(object sender, RoutedEventArgs e)
        {
            switch (Mode)
            {
                case PathPickMode.File:
                    PickFile();
                    break;
                case PathPickMode.Folder:
                    PickFolder();
                    break;
            }
        }

        private async void PickFile()
        {
            var filePicker = new FileOpenPicker();
            FileTypeFilter.ToList().ForEach(filePicker.FileTypeFilter.Add);
            filePicker.SetOwnerWindow(Window);
            var file = await filePicker.PickSingleFileAsync();
            if (file != null)
                Path = file.Path;
        }

        private async void PickFolder()
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
