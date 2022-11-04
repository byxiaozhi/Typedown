using System.Collections.Generic;
using Typedown.Universal.Interfaces;
using Typedown.Universal.ViewModels;
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

        private readonly AppViewModel viewModel;

        public FrontMenu(AppViewModel viewModel)
        {
            this.viewModel = viewModel;
            this.markdownEditor = viewModel.MarkdownEditor;
            Flyout.Closed += OnClosed;
            Flyout.AreOpenCloseAnimationsEnabled = viewModel.SettingsViewModel.AnimationEnable;
            BindingDataContext(Flyout.Items);
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

        public void BindingDataContext(IList<MenuFlyoutItemBase> items)
        {
            foreach (var item in items)
            {
                item.DataContext = viewModel;
                if (item is MenuFlyoutSubItem sub)
                    BindingDataContext(sub.Items);
            }
        }
    }
}
