using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
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

        private readonly CompositeDisposable disposables = new();

        public FrontMenu(IMarkdownEditor markdownEditor)
        {
            this.markdownEditor = markdownEditor;
            Flyout.Closed += OnClosed;
        }

        private void OnClosed(object sender, object e)
        {
            markdownEditor.PostMessage("FrontMenuClosed", null);
            disposables.Clear();
        }

        public void Open(Rect rect)
        {
            var options = new FlyoutShowOptions() { Placement = FlyoutPlacementMode.Bottom };
            Flyout.ShowAt(markdownEditor.GetDummyRectangle(rect), options);
        }
    }
}
