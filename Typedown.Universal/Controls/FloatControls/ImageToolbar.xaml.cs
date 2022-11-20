using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Web;
using Typedown.Universal.Interfaces;
using Typedown.Universal.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Typedown.Universal.Controls.FloatControls
{
    public sealed partial class ImageToolbar : Flyout
    {
        public AppViewModel ViewModel { get; }

        public IMarkdownEditor MarkdownEditor { get; }

        public IKeyboardAccelerator KeyboardAccelerator { get; }

        private readonly CompositeDisposable disposables = new();

        public ImageToolbar(AppViewModel viewModel, IMarkdownEditor markdownEditor, IKeyboardAccelerator keyboardAccelerator)
        {
            ViewModel = viewModel;
            MarkdownEditor = markdownEditor;
            KeyboardAccelerator = keyboardAccelerator;
            InitializeComponent();
        }

        public void Open(Rect rect)
        {
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
            Hide();
        }

        private void PostEditTableMessage(object args)
        {
            MarkdownEditor.PostMessage("ImageEditToolbarClick", args);
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
