using Newtonsoft.Json.Linq;
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
        private AppViewModel ViewModel { get; }

        private IMarkdownEditor MarkdownEditor { get; }

        private readonly Flyout flyout = new() { Placement = FlyoutPlacementMode.Bottom };

        public ImageSelector(AppViewModel viewModel, IMarkdownEditor markdownEditor)
        {
            ViewModel = viewModel;
            MarkdownEditor = markdownEditor;
            flyout.AreOpenCloseAnimationsEnabled = this.ViewModel.SettingsViewModel.AnimationEnable;
            flyout.Closing += OnFlyoutClosing;
            InitializeComponent();
        }

        public void Open(Rect rect, JToken imageInfo)
        {
            TextBoxSrc.Text = HttpUtility.UrlDecode(imageInfo?["src"]?.ToString() ?? "");
            TextBoxAlt.Text = imageInfo?["alt"]?.ToString() ?? "";
            TextBoxTitle.Text = imageInfo?["title"]?.ToString() ?? "";
            flyout.Content = this;
            flyout.ShowAt(MarkdownEditor.GetDummyRectangle(new(rect.X, rect.Y, rect.Width, 0)));
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
            MarkdownEditor.PostMessage("ReplaceImage", new { src, alt, title });
        }
    }
}
