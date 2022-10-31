using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json.Linq;
using System;
using System.Web;
using Typedown.Universal.Interfaces;
using Typedown.Universal.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Typedown.Universal.Controls.FloatControls
{
    public sealed partial class ImageSelector : UserControl
    {
        public AppViewModel ViewModel { get; }

        private readonly IMarkdownEditor markdownEditor;

        private readonly Flyout flyout = new();

        public ImageSelector(AppViewModel viewModel, IMarkdownEditor markdownEditor)
        {
            ViewModel = viewModel;
            this.markdownEditor = markdownEditor;
            flyout.AreOpenCloseAnimationsEnabled = ViewModel.SettingsViewModel.AnimationEnable;
            flyout.Closing += OnFlyoutClosing;
            InitializeComponent();
        }

        public void Open(Rect rect, JToken imageInfo)
        {
            TextBoxSrc.Text = HttpUtility.UrlDecode(imageInfo?["src"]?.ToString() ?? "");
            TextBoxAlt.Text = imageInfo?["alt"]?.ToString() ?? "";
            TextBoxTitle.Text = imageInfo?["title"]?.ToString() ?? "";
            var options = new FlyoutShowOptions() { Placement = FlyoutPlacementMode.Bottom };
            flyout.ShowAt(markdownEditor.GetDummyRectangle(rect), options);
            flyout.Content = this;
        }

        private void OnFlyoutClosing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            args.Cancel = ImagePickerButton.IsPicking;
        }

        private void OnSaveButtonClick(object sender, RoutedEventArgs e)
        {
            SaveImageSrc(TextBoxSrc.Text, TextBoxTitle.Text, TextBoxAlt.Text);
            flyout.Hide();
        }

        private void OnCancelButtonClick(object sender, RoutedEventArgs e)
        {
            flyout.Hide();
        }

        private void SaveImageSrc(string src, string title = null, string alt = null)
        {
            markdownEditor.PostMessage("ReplaceImage", new { src, alt, title });
        }
    }
}
