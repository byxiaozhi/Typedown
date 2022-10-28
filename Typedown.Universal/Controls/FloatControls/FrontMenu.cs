using Typedown.Universal.Interfaces;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Typedown.Universal.Controls.FloatControls
{
    public class FrontMenu
    {
        private readonly ResourceDictionary resources = new() { Source = new("ms-appx:///Resources/Styles/Flyouts/EditorFrontMenu.xaml") };

        private MenuFlyout Flyout => resources["EditorFrontMenu"] as MenuFlyout;

        private readonly IMarkdownEditor markdownEditor;

        public FrontMenu(IMarkdownEditor markdownEditor)
        {
            this.markdownEditor = markdownEditor;
            Flyout.Closed += OnClosed;
        }

        private void OnClosed(object sender, object e)
        {
            markdownEditor.PostMessage("FrontMenuClosed", null);
        }

        public void Open(Rect rect)
        {
            var options = new FlyoutShowOptions() { Placement = FlyoutPlacementMode.Bottom };
            Flyout.OverlayInputPassThroughElement = (markdownEditor as UIElement).XamlRoot.Content;
            Flyout.ShowAt(markdownEditor.GetDummyRectangle(rect), options);
        }
    }
}
