using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Typedown.Universal.Controls
{
    public sealed partial class EditorContainer : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public FloatViewModel Float => ViewModel?.FloatViewModel;

        public EditorContainer()
        {
            this.InitializeComponent();
            HeaderButtons.AddHandler(Button.PointerReleasedEvent, new PointerEventHandler(OnMenuFlyoutItemPointerReleased), true);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            MarkdownEditorPresenter.Content = this.GetService<IMarkdownEditor>();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            MarkdownEditorPresenter.Content = null;
        }

        public static bool GetSearchFloatLoad(FloatViewModel.SearchOpenType type)
        {
            return type != FloatViewModel.SearchOpenType.None;
        }

        private void OnMenuFlyoutItemPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            Flyout.Hide();
        }
    }
}
