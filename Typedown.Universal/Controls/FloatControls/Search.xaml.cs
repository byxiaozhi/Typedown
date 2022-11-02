using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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
using static Typedown.Universal.ViewModels.FloatViewModel;

namespace Typedown.Universal.Controls.FloatControls
{
    public sealed partial class Search : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public FloatViewModel Float => ViewModel.FloatViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        private readonly CompositeDisposable disposables = new();

        public Search()
        {
            InitializeComponent();
            SharedShadow.Receivers.Add(BackgroundGrid);
            DialogGrid.Translation += new Vector3(0, 0, 32);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            LeftSplitter.ColumnMaxWidth = RightSplitter.ColumnMaxWidth = ActualWidth - 64;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await Task.Yield();
            TextBoxSearch.Focus(FocusState.Pointer);
            TextBoxSearch.SelectionStart = 0;
            TextBoxSearch.SelectionLength = TextBoxSearch.Text.Length;
            disposables.Add(Float.WhenPropertyChanged(nameof(Float.SearchOpen)).Cast<SearchOpenType>().Subscribe(s => SearchOpenChanged(s, true)));
            SearchOpenChanged(Float.SearchOpen, false);
        }

        private void SearchOpenChanged(SearchOpenType state, bool useTransitions)
        {
            switch (state)
            {
                case SearchOpenType.Search:
                    VisualStateManager.GoToState(this, "SearchMode", useTransitions && Settings.AnimationEnable);
                    break;
                case SearchOpenType.Replace:
                    VisualStateManager.GoToState(this, "ReplaceMode", useTransitions && Settings.AnimationEnable);
                    break;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Float.SearchOpen = 0;
        }

        private void OnReplaceButtonClick(object sender, RoutedEventArgs e)
        {
            PostReplaceMessage(true);
        }

        private void OnReplaceAllButtonClick(object sender, RoutedEventArgs e)
        {
            PostReplaceMessage(false);
        }

        private void OnTextBoxSearchKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
                ViewModel.EditorViewModel.FindCommand.Execute("next");
        }

        private void OnTextBoxReplaceKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
                PostReplaceMessage(true);
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
                Float.SearchOpen = 0;
        }

        private void PostReplaceMessage(bool isSingle)
        {
            ViewModel?.MarkdownEditor?.PostMessage("Replace", new
            {
                searchValue = ViewModel.EditorViewModel.SearchValue,
                value = TextBoxReplace.Text,
                opt = new
                {
                    isSingle,
                    isCaseSensitive = ViewModel.SettingsViewModel.SearchIsCaseSensitive,
                    isWholeWord = ViewModel.SettingsViewModel.SearchIsWholeWord,
                    isRegexp = ViewModel.SettingsViewModel.SearchIsRegexp,
                }
            });
        }

        private void OnSwitchButtonClick(object sender, RoutedEventArgs e)
        {
            Float.SearchOpen = Float.SearchOpen == SearchOpenType.Search ? SearchOpenType.Replace : SearchOpenType.Search;
        }
    }
}
