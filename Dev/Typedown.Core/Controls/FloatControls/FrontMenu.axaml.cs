using Avalonia.Controls.Primitives;
using System.Collections.Generic;
using Typedown.Core.ViewModels;
using Avalonia.Utilities;
using Avalonia;
using Avalonia.Controls;

namespace Typedown.Core.Controls.FloatControls
{
    public sealed partial class FrontMenu : MenuFlyout
    {
        public AppViewModel ViewModel { get; }

        public FrontMenu(AppViewModel viewModel)
        {
            ViewModel = viewModel;
            Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        }

        private void OnClosed(object sender, object e)
        {
            ViewModel.MarkdownEditor.PostMessage("FrontMenuClosed", null);
        }

        public void Open(Rect rect)
        {
            BindingDataContext(Items);
            var dummyControl = ViewModel.MarkdownEditor.GetDummyRectangle(rect);
            if (dummyControl.Parent == null) 
            {
               // it shouldn't happen if properly hooked, but if needed, we'll ensure it's in tree
            }
            ShowAt(dummyControl);
        }

        private void BindingDataContext(IEnumerable<object> items)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (item is MenuItem menuItem)
                {
                    menuItem.DataContext = ViewModel;
                    BindingDataContext(menuItem.Items);
                }
            }
        }
    }
}

