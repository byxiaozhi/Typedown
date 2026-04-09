using Avalonia.Interactivity;
using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using System.Linq;
using System.Numerics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Typedown.Core.Controls.FloatControls
{
    public sealed partial class FindReplace : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public FloatViewModel Float => ViewModel.FloatViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        private readonly CompositeDisposable disposables = new();

        public FindReplace()
        {
            InitializeComponent();
            // SharedShadow.Receivers.Add(BackgroundGrid);
            // DialogGrid.Translation += new Vector3(0, 0, 32);
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            // LeftSplitter.ColumnMaxWidth = RightSplitter.ColumnMaxWidth = Bounds.Width - 64;
        }

        private async void OnLoaded(object? sender, RoutedEventArgs e)
        {
            await Task.Yield();
            TextBoxSearch.Focus();
            TextBoxSearch.SelectionStart = 0;
            TextBoxSearch.SelectionEnd = TextBoxSearch.Text?.Length ?? 0;
            disposables.Add(Float.WhenPropertyChanged(nameof(Float.FindReplaceDialogOpen)).Cast<FloatViewModel.FindReplaceDialogState>().Subscribe(s => SearchOpenChanged(s, true)));
            SearchOpenChanged(Float.FindReplaceDialogOpen, false);
        }

        private void SearchOpenChanged(FloatViewModel.FindReplaceDialogState state, bool useTransitions)
        {
            // TODO: Use Classes or IsVisible instead of VisualStateManager
            // switch (state)
            // {
            //     case FloatViewModel.FindReplaceDialogState.Search:
            //         break;
            //     case FloatViewModel.FindReplaceDialogState.Replace:
            //         break;
            // }
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            disposables.Clear();
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Float.FindReplaceDialogOpen = 0;
        }

        private void OnReplaceButtonClick(object sender, RoutedEventArgs e)
        {
            PostReplaceMessage(true);
        }

        private void OnReplaceAllButtonClick(object sender, RoutedEventArgs e)
        {
            PostReplaceMessage(false);
        }

        private void OnTextBoxSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
                ViewModel.EditorViewModel.FindCommand.Execute("next");
        }

        private void OnTextBoxReplaceKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
                PostReplaceMessage(true);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Escape)
                Float.FindReplaceDialogOpen = 0;
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
                    searchIsCaseSensitive = ViewModel.SettingsViewModel.SearchIsCaseSensitive,
                    searchIsWholeWord = ViewModel.SettingsViewModel.SearchIsWholeWord,
                    searchIsRegexp = ViewModel.SettingsViewModel.SearchIsRegexp,
                }
            });
        }

        private void OnSwitchButtonClick(object sender, RoutedEventArgs e)
        {
            Float.FindReplaceDialogOpen = Float.FindReplaceDialogOpen == FloatViewModel.FindReplaceDialogState.Search ? FloatViewModel.FindReplaceDialogState.Replace : FloatViewModel.FindReplaceDialogState.Search;
        }
    }
}

