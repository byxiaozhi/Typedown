using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using System.Web;
using Typedown.Core.Interfaces;
using Typedown.Core.Models.RuntimeModels;
using Typedown.Core.Services;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Typedown.Core.Controls.FloatControls
{
    public sealed partial class ImageSelector : UserControl
    {
        private AppViewModel ViewModel { get; }

        private IMarkdownEditor MarkdownEditor { get; }

        private readonly Flyout flyout = new() { Placement = FlyoutPlacementMode.Bottom };

        private static string currentSrc;

        private Rect rect;

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
            this.rect = rect;
            TextBoxSrc.Text = HttpUtility.UrlDecode(imageInfo?["src"]?.ToString() ?? "");
            TextBoxAlt.Text = imageInfo?["alt"]?.ToString() ?? "";
            TextBoxTitle.Text = imageInfo?["title"]?.ToString() ?? "";
            flyout.Content = this;
            flyout.ShowAt(MarkdownEditor.GetDummyRectangle(new(rect.X, rect.Y, rect.Width, 0)));
            currentSrc = TextBoxSrc.Text;
        }

        private void OnFlyoutClosing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            args.Cancel = ImagePickerButton.IsPicking || SaveButton.IsLoading;
        }

        private async void OnSaveButtonClick(object sender, RoutedEventArgs e)
        {
            SaveButton.IsLoading = true;
            try
            {
                await SaveImageSrc(TextBoxSrc.Text, TextBoxTitle.Text, TextBoxAlt.Text);
                SaveButton.IsLoading = false;
                flyout.Hide();
            }
            catch (Exception ex)
            {
                await AppContentDialog.Create(Locale.GetString("Error"), ex.Message, Locale.GetDialogString("Ok")).ShowAsync(XamlRoot);
            }
            finally
            {
                SaveButton.IsLoading = false;
            }
        }

        private void OnCancelButtonClick(object sender, RoutedEventArgs e)
        {
            flyout.Hide();
        }

        private async Task SaveImageSrc(string src, string title = null, string alt = null)
        {
            if (src != currentSrc)
            {
                var imageAction = this.GetService<ImageAction>();
                if (UriHelper.IsWebUrl(src))
                {
                    src = await imageAction.DoWebFileAction(src);
                }
                else if (UriHelper.TryGetLocalPath(src, out _))
                {
                    src = await imageAction.DoLocalFileAction(src);
                    src = src.Replace('\\', '/');
                }
            }
            if (flyout.IsOpen)
            {
                if (ViewModel.SettingsViewModel.AutoEncodeImageURL)
                {
                    // TODO
                }
                MarkdownEditor.PostMessage("ReplaceImage", new HtmlImgTag(src, alt, title));
            }

        }

        private void OnImagePickerButtonPicked(object sender, PickedEventArgs e)
        {
            if (!flyout.IsOpen)
                flyout.ShowAt(MarkdownEditor.GetDummyRectangle(new(rect.X, rect.Y, rect.Width, 0)));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings.StopTracking();
        }
    }
}
