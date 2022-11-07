using Typedown.Universal.Interfaces;
using Typedown.Universal.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.Controls.FloatControls
{
    public sealed partial class TableTools : MenuFlyout
    {
        public bool IsRow { get; private set; }

        public Visibility RowItemVisibility => IsRow ? Visibility.Visible : Visibility.Collapsed;

        public Visibility ColumnItemVisibility => !IsRow ? Visibility.Visible : Visibility.Collapsed;

        private readonly IMarkdownEditor markdownEditor;

        public TableTools(SettingsViewModel settings, IMarkdownEditor markdownEditor)
        {
            this.markdownEditor = markdownEditor;
            AreOpenCloseAnimationsEnabled = settings.AnimationEnable;
            InitializeComponent();
        }

        public void Open(Rect rect, string type)
        {
            IsRow = type != "bottom";
            OverlayInputPassThroughElement = (markdownEditor as UIElement).XamlRoot.Content;
            UpdateItemVisibility();
            ShowAt(markdownEditor.GetDummyRectangle(rect));
        }

        private void UpdateItemVisibility()
        {
            InsertPreviousRow.Visibility = InsertNextRow.Visibility = RemoveNurrentRow.Visibility = RowItemVisibility;
            InsertLeftColumn.Visibility = InsertRightColumn.Visibility = RemoveCurrentColumn.Visibility = ColumnItemVisibility;
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
            markdownEditor.PostMessage("EditTable", args);
        }
    }
}
