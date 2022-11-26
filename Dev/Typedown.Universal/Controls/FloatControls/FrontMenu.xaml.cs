using System.Collections.Generic;
using Typedown.Universal.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Typedown.Universal.Controls.FloatControls
{
    public sealed partial class FrontMenu : MenuFlyout
    {
        public AppViewModel ViewModel { get; }

        public FrontMenu(AppViewModel viewModel)
        {
            ViewModel = viewModel;
            InitializeComponent();
        }

        private void OnClosed(object sender, object e)
        {
            ViewModel.MarkdownEditor.PostMessage("FrontMenuClosed", null);
        }

        public void Open(Rect rect)
        {
            BindingDataContext(Items);
            OverlayInputPassThroughElement = (ViewModel.MarkdownEditor as UIElement).XamlRoot.Content;
            AreOpenCloseAnimationsEnabled = ViewModel.SettingsViewModel.AnimationEnable;
            ShowAt(ViewModel.MarkdownEditor.GetDummyRectangle(rect));
        }

        private void BindingDataContext(IList<MenuFlyoutItemBase> items)
        {
            foreach (var item in items)
            {
                item.DataContext = ViewModel;
                if (item is MenuFlyoutSubItem sub)
                    BindingDataContext(sub.Items);
            }
        }
    }
}
