using Avalonia.Interactivity;
using Typedown.Core.Interfaces;
using Typedown.Core.ViewModels;
using Avalonia.Utilities;
using Avalonia;
using Avalonia.Controls;

namespace Typedown.Core.Controls.FloatControls
{
    public sealed partial class TableTools : MenuFlyout
    {
        public bool IsRow { get; private set; }

        public bool RowItemVisible => IsRow;

        public bool ColumnItemVisible => !IsRow;

        private IMarkdownEditor MarkdownEditor { get; }
        private AppViewModel ViewModel { get; }

        private MenuItem InsertPreviousRowItem => (MenuItem)Items[0];
        private MenuItem InsertNextRowItem => (MenuItem)Items[1];
        private MenuItem RemoveCurrentRowItem => (MenuItem)Items[2];
        private MenuItem InsertLeftColumnItem => (MenuItem)Items[3];
        private MenuItem InsertRightColumnItem => (MenuItem)Items[4];
        private MenuItem RemoveCurrentColumnItem => (MenuItem)Items[5];

        public TableTools(AppViewModel viewModel, IMarkdownEditor markdownEditor)
        {
            ViewModel = viewModel;
            MarkdownEditor = markdownEditor;
            Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);

            InsertPreviousRowItem.Click += InsertPreviousRow_Click;
            InsertNextRowItem.Click += InsertNextRow_Click;
            RemoveCurrentRowItem.Click += RemoveNurrentRow_Click;
            InsertLeftColumnItem.Click += InsertLeftColumn_Click;
            InsertRightColumnItem.Click += InsertRightColumn_Click;
            RemoveCurrentColumnItem.Click += RemoveCurrentColumn_Click;
        }

        public void Open(Rect rect, string type)
        {
            IsRow = type != "bottom";
            UpdateItemVisible();
            ShowAt((Control)MarkdownEditor);
        }

        private void UpdateItemVisible()
        {
            InsertPreviousRowItem.IsVisible = InsertNextRowItem.IsVisible = RemoveCurrentRowItem.IsVisible = RowItemVisible;
            InsertLeftColumnItem.IsVisible = InsertRightColumnItem.IsVisible = RemoveCurrentColumnItem.IsVisible = ColumnItemVisible;
        }

        private void InsertPreviousRow_Click(object sender, RoutedEventArgs e)
        {
            PostEditTableMessage(new { action = "insert", location = "previous", target = "row" });
        }

        private void InsertNextRow_Click(object sender, RoutedEventArgs e)
        {
            PostEditTableMessage(new { action = "insert", location = "next", target = "row" });
        }

        private void RemoveNurrentRow_Click(object sender, RoutedEventArgs e)
        {
            PostEditTableMessage(new { action = "remove", location = "current", target = "row" });
        }

        private void InsertLeftColumn_Click(object sender, RoutedEventArgs e)
        {
            PostEditTableMessage(new { action = "insert", location = "left", target = "column" });
        }

        private void InsertRightColumn_Click(object sender, RoutedEventArgs e)
        {
            PostEditTableMessage(new { action = "insert", location = "right", target = "column" });
        }

        private void RemoveCurrentColumn_Click(object sender, RoutedEventArgs e)
        {
            PostEditTableMessage(new { action = "remove", location = "current", target = "column" });
        }

        private void PostEditTableMessage(object args)
        {
            MarkdownEditor.PostMessage("EditTable", args);
        }
    }
}

