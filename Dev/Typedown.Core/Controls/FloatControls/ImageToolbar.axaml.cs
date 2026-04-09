using Avalonia.Interactivity;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Typedown.Core.Interfaces;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Avalonia.Utilities;
using Avalonia.Input;
using Avalonia;
using Avalonia.Controls;

namespace Typedown.Core.Controls.FloatControls
{
    public sealed partial class ImageToolbar : Flyout
    {
        public AppViewModel ViewModel { get; }

        public IMarkdownEditor MarkdownEditor { get; }

        public IKeyboardAccelerator KeyboardAccelerator { get; }

        private readonly CompositeDisposable disposables = new();

        private JToken attrs;

        public ImageToolbar(AppViewModel viewModel, IMarkdownEditor markdownEditor, IKeyboardAccelerator keyboardAccelerator)
        {
            ViewModel = viewModel;
            MarkdownEditor = markdownEditor;
            KeyboardAccelerator = keyboardAccelerator;
            Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);

            var scope = Avalonia.Controls.NameScope.GetNameScope((Avalonia.StyledElement)Content);
            scope.Find<Button>("EditButton").Click += EditClick;
            scope.Find<Button>("InlineButton").Click += InlineClick;
            scope.Find<Button>("LeftButton").Click += LeftClick;
            scope.Find<Button>("CenterButton").Click += CenterClick;
            scope.Find<Button>("RightButton").Click += RightClick;
            scope.Find<Button>("DeleteButton").Click += DeleteClick;

            var zoomFlyout = (MenuFlyout)scope.Find<Button>("ZoomButton").Flyout;
            foreach (var item in zoomFlyout.Items.OfType<MenuItem>())
            {
                item.Click += ZoomClick;
            }

            Opened += OnOpened;
            Closed += OnClosed;
        }

        public void Open(Rect rect, JToken attrs)
        {
            this.attrs = attrs;
            ShowAt(MarkdownEditor.GetDummyRectangle(rect));
        }

        private void EditClick(object sender, RoutedEventArgs e)
        {
            PostEditImageMessage(new { type = "edit" });
        }

        private void InlineClick(object sender, RoutedEventArgs e)
        {
            PostEditImageMessage(new { type = "inline" });
        }

        private void LeftClick(object sender, RoutedEventArgs e)
        {
            PostEditImageMessage(new { type = "left" });
        }

        private void CenterClick(object sender, RoutedEventArgs e)
        {
            PostEditImageMessage(new { type = "center" });
        }

        private void RightClick(object sender, RoutedEventArgs e)
        {
            PostEditImageMessage(new { type = "right" });
        }

        private void DeleteClick(object sender, RoutedEventArgs e)
        {
            PostEditImageMessage(new { type = "delete" });
        }

        private void ZoomClick(object sender, RoutedEventArgs e)
        {
            var zoom = (sender as MenuItem).Tag as string;
            var style = (attrs["style"]?.ToString() ?? "").Split(';').Where(x => !x.StartsWith("zoom:") && !string.IsNullOrWhiteSpace(x)).ToList();
            style.Add($"zoom:{zoom}");
            PostEditImageMessage(new { type = "updateImage", attrName = "style", attrValue = $"{string.Join(';', style)};" });
        }

        private void PostEditImageMessage(object args)
        {
            MarkdownEditor.PostMessage("ImageEditToolbarClick", args);
            Hide();
        }

        private void OnOpened(object sender, object e)
        {
            disposables.Add(KeyboardAccelerator.GetObservable().Where(e => e.Key == Typedown.Core.Enums.VirtualKey.Back || e.Key == Typedown.Core.Enums.VirtualKey.Delete).Subscribe(e =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => DeleteClick(null, null));
                e.Handled = true;
            }));
        }

        private void OnClosed(object sender, object e)
        {
            disposables.Dispose();
        }
    }
}

