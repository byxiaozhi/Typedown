using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace Typedown.Core.Controls
{
    public sealed partial class EditorContainer : UserControl
    {
        private readonly static DependencyProperty IsFindReplaceLoadProperty = DependencyProperty.Register(nameof(IsFindReplaceLoad), typeof(bool), typeof(EditorContainer), new(false));
        private bool IsFindReplaceLoad { get => (bool)GetValue(IsFindReplaceLoadProperty); set => SetValue(IsFindReplaceLoadProperty, value); }

        private readonly static DependencyProperty FindReplaceCenterPointProperty = DependencyProperty.Register(nameof(FindReplaceCenterPoint), typeof(Point), typeof(EditorContainer), new(new Point()));
        private Point FindReplaceCenterPoint { get => (Point)GetValue(FindReplaceCenterPointProperty); set => SetValue(FindReplaceCenterPointProperty, value); }

        private readonly static DependencyProperty ScrollStateProperty = DependencyProperty.Register(nameof(ScrollState), typeof(ScrollState), typeof(EditorContainer), new(new ScrollState(), (d, e) => (d as EditorContainer).OnDependencyPropertyChanged(e)));
        private ScrollState ScrollState { get => (ScrollState)GetValue(ScrollStateProperty); set => SetValue(ScrollStateProperty, value); }

        public AppViewModel ViewModel => DataContext as AppViewModel;
        public FloatViewModel Float => ViewModel?.FloatViewModel;
        public EditorViewModel Editor => ViewModel?.EditorViewModel;
        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;
        public FormatViewModel Format => ViewModel?.FormatViewModel;

        private readonly CompositeDisposable disposables = new();

        public EditorContainer()
        {
            InitializeComponent();
            AddHandler(PointerMovedEvent, new PointerEventHandler(OnPointerPointerMoved), true);
            AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnPointerWheelChanged), true);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            MarkdownEditorPresenter.Content = this.GetService<IMarkdownEditor>();
            disposables.Add(Float.WhenPropertyChanged(nameof(Float.FindReplaceDialogOpen))
                .Cast<FloatViewModel.FindReplaceDialogState>()
                .Subscribe(x => UpdateFindReplaceState(x, true)));
            disposables.Add(Editor.EventCenter.GetObservable<EditorEventArgs>("OnScroll")
                .Subscribe(x => ScrollState = x.Args.ToObject<ScrollState>()));
            UpdateFindReplaceState(Float.FindReplaceDialogOpen, false);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            MarkdownEditorPresenter.Content = null;
            disposables.Clear();
            Bindings?.StopTracking();
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

        private void OnScroll(object sender, ScrollEventArgs e)
        {
            ViewModel.MarkdownEditor.PostMessage("OnScroll", new { ScrollX = HorizontalScrollBar.Value, ScrollY = VerticalScrollBar.Value });
        }

        private Visibility IsScrollBarVisibility(double maximum)
        {
            return maximum <= 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private double GetSmallChange(double fontSize, double lineHeight)
        {
            return fontSize * lineHeight;
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
                            e.DragUIOverride.Caption = Locale.GetString("Open");
                            break;
                        case FileTypeHelper.FileType.Image:
                            e.AcceptedOperation = DataPackageOperation.Link;
                            e.DragUIOverride.Caption = Locale.GetString("InsertImage");
                            break;
                    }
                }
            }
            catch
            {
                e.AcceptedOperation = DataPackageOperation.None;
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

        private void OnPointerPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch)
            {
                HorizontalScrollBar.IndicatorMode = ScrollingIndicatorMode.TouchIndicator;
                VerticalScrollBar.IndicatorMode = ScrollingIndicatorMode.TouchIndicator;
            }
            else
            {
                HorizontalScrollBar.IndicatorMode = ScrollingIndicatorMode.MouseIndicator;
                VerticalScrollBar.IndicatorMode = ScrollingIndicatorMode.MouseIndicator;
            }
        }

        private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control))
            {
                var delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
                Settings.FontSize = Math.Max(8, Math.Min(48, Math.Round(Settings.FontSize * (1 + delta / 1200d), 1)));
            }
        }

        private void OnFormatItemClick(object sender, EventArgs e)
        {
            Flyout.Hide();
        }

        private void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == ScrollStateProperty)
            {
                if (e.OldValue is ScrollState oldValue && e.NewValue is ScrollState newValue && MarkdownEditorPresenter.Content is IMarkdownEditor editor)
                {
                    editor.MoveDummyRectangle(new(oldValue.ScrollX - newValue.ScrollX, oldValue.ScrollY - newValue.ScrollY));
                }
            }
        }

        public static bool IsLoadImageMenu(bool isImageFormat, JToken selection)
        {
            return isImageFormat && (selection["selectedImage"]?.HasValues ?? false);
        }
    }
}
