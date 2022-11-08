using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Typedown.Universal.Controls
{
    public sealed partial class EditorContainer : UserControl
    {
        private static DependencyProperty IsFindReplaceLoadProperty = DependencyProperty.Register(nameof(IsFindReplaceLoad), typeof(bool), typeof(EditorContainer), new(false));
        private bool IsFindReplaceLoad { get => (bool)GetValue(IsFindReplaceLoadProperty); set => SetValue(IsFindReplaceLoadProperty, value); }

        private static DependencyProperty FindReplaceCenterPointProperty = DependencyProperty.Register(nameof(FindReplaceCenterPoint), typeof(Point), typeof(EditorContainer), new(new Point()));
        private Point FindReplaceCenterPoint { get => (Point)GetValue(FindReplaceCenterPointProperty); set => SetValue(FindReplaceCenterPointProperty, value); }

        public AppViewModel ViewModel => DataContext as AppViewModel;
        public FloatViewModel Float => ViewModel?.FloatViewModel;
        public EditorViewModel Editor => ViewModel?.EditorViewModel;
        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;
        public FormatViewModel Format => ViewModel?.FormatViewModel;

        private readonly CompositeDisposable disposables = new();

        public EditorContainer()
        {
            InitializeComponent();
            HeaderButtons.AddHandler(Button.PointerReleasedEvent, new PointerEventHandler(OnMenuFlyoutItemPointerReleased), true);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            MarkdownEditorPresenter.Content = this.GetService<IMarkdownEditor>();
            disposables.Add(Float.WhenPropertyChanged(nameof(Float.FindReplaceDialogOpen))
                .Cast<FloatViewModel.FindReplaceDialogState>()
                .Subscribe(x => UpdateFindReplaceState(x, true)));
            UpdateFindReplaceState(Float.FindReplaceDialogOpen, false);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            MarkdownEditorPresenter.Content = null;
            disposables.Clear();
        }

        private void UpdateFindReplaceState(FloatViewModel.FindReplaceDialogState findReplaceOpen, bool useTransitions = true)
        {
            FindReplaceCenterPoint = new(ActualWidth / 2, findReplaceOpen switch
            {
                FloatViewModel.FindReplaceDialogState.Search => 32,
                FloatViewModel.FindReplaceDialogState.Replace => 52,
                _ => FindReplaceCenterPoint.Y
            });
            VisualStateManager.GoToState(this, findReplaceOpen != FloatViewModel.FindReplaceDialogState.None ? "FindReplaceVisible" : "FindReplaceCollapsed", useTransitions && Settings.AnimationEnable);
        }

        private void OnMenuFlyoutItemPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            Flyout.Hide();
        }

        private async void OnDragEnter(object sender, DragEventArgs e)
        {
            var deferral = e.GetDeferral();
            try
            {
                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    if (items.Count != 1) return;
                    var item = items.First();
                    switch (FileTypeHelper.GetFileType(item.Path))
                    {
                        case FileTypeHelper.FileType.Markdown:
                            e.AcceptedOperation = DataPackageOperation.Link;
                            e.DragUIOverride.Caption = "打开";
                            break;
                        case FileTypeHelper.FileType.Image:
                            e.AcceptedOperation = DataPackageOperation.Link;
                            e.DragUIOverride.Caption = "插入";
                            break;
                    }
                }

            }
            finally
            {
                deferral.Complete();
            }
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count != 1) return;
                var item = items.First();
                if (FileTypeHelper.IsMarkdownFile(item.Path))
                {
                    ViewModel.FileViewModel.OpenFileCommand.Execute(item.Path);
                }
                if (FileTypeHelper.IsImageFile(item.Path))
                {
                    ViewModel.MarkdownEditor.PostMessage("InsertImage", new { src = item.Path });
                }
            }
        }
    }
}
