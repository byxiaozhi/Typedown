using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Typedown.Core.Interfaces;
using Typedown.Core.ViewModels;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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
            InitializeComponent();
        }

        public void Open(Rect rect, JToken attrs)
        {
            this.attrs = attrs;
            AreOpenCloseAnimationsEnabled = ViewModel.SettingsViewModel.AnimationEnable;
            OverlayInputPassThroughElement = ViewModel.XamlRoot.Content;
            ShowAt(MarkdownEditor.GetDummyRectangle(rect));
        }

        private void EditClick(object sender, RoutedEventArgs e)
        {
            PostEditTableMessage(new { type = "edit" });
        }

        private void InlineClick(object sender, RoutedEventArgs e)
        {
            PostEditTableMessage(new { type = "inline" });
        }

        private void LeftClick(object sender, RoutedEventArgs e)
        {
            PostEditTableMessage(new { type = "left" });
        }

        private void CenterClick(object sender, RoutedEventArgs e)
        {
            PostEditTableMessage(new { type = "center" });
        }

        private void RightClick(object sender, RoutedEventArgs e)
        {
            PostEditTableMessage(new { type = "right" });
        }

        private void DeleteClick(object sender, RoutedEventArgs e)
        {
            PostEditTableMessage(new { type = "delete" });
        }

        private void ZoomClick(object sender, RoutedEventArgs e)
        {
            var zoom = (sender as MenuFlyoutItem).Tag as string;
            var style = (attrs["style"]?.ToString() ?? "").Split(';').Where(x => !x.StartsWith("zoom:") && !string.IsNullOrWhiteSpace(x)).ToList();
            style.Add($"zoom:{zoom}");
            PostEditTableMessage(new { type = "updateImage", attrName = "style", attrValue = $"{string.Join(';', style)};" });
        }

        private void PostEditTableMessage(object args)
        {
            MarkdownEditor.PostMessage("ImageEditToolbarClick", args);
            Hide();
        }

        private void OnOpened(object sender, object e)
        {
            disposables.Add(KeyboardAccelerator.GetObservable().Where(e => e.Key == VirtualKey.Back || e.Key == VirtualKey.Delete).Subscribe(e =>
            {
                _ = Dispatcher.RunIdleAsync(_ => DeleteClick(null, null));
                e.Handled = true;
            }));
        }

        private void OnClosed(object sender, object e)
        {
            disposables.Dispose();
        }
    }
}
