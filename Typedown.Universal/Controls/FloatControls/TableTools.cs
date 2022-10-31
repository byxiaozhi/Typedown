using System.Linq;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Typedown.Universal.Controls.FloatControls
{
    public class TableTools : ObservableObject
    {
        private readonly ResourceDictionary resources = new() { Source = new("ms-appx:///Resources/Styles/Flyouts/EditorTableTools.xaml") };

        private MenuFlyout Flyout => resources["EditorTableTools"] as MenuFlyout;

        public bool IsRow { get; private set; }

        public Visibility RowItemVisibility => IsRow ? Visibility.Visible : Visibility.Collapsed;

        public Visibility ColumnItemVisibility => !IsRow ? Visibility.Visible : Visibility.Collapsed;

        private readonly IMarkdownEditor markdownEditor;

        public TableTools(SettingsViewModel settings, IMarkdownEditor markdownEditor)
        {
            this.markdownEditor = markdownEditor;
            Flyout.AreOpenCloseAnimationsEnabled = settings.AnimationEnable;
            InitializeBinding();
        }

        public void Open(Rect rect, string type)
        {
            IsRow = type != "bottom";
            Flyout.OverlayInputPassThroughElement = (markdownEditor as UIElement).XamlRoot.Content;
            Flyout.ShowAt(markdownEditor.GetDummyRectangle(rect));
        }

        private void InitializeBinding()
        {
            foreach (var item in Flyout.Items.OfType<MenuFlyoutItem>())
            {
                switch (item.Name)
                {
                    case "InsertPreviousRow":
                        item.Click += (s, e) => PostEditTableMessage(new { action = "insert", location = "previous", target = "row" });
                        item.SetBinding(UIElement.VisibilityProperty, new Binding() { Path = new(nameof(RowItemVisibility)), Source = this });
                        break;
                    case "InsertNextRow":
                        item.Click += (s, e) => PostEditTableMessage(new { action = "insert", location = "next", target = "row" });
                        item.SetBinding(UIElement.VisibilityProperty, new Binding() { Path = new(nameof(RowItemVisibility)), Source = this });
                        break;
                    case "RemoveNurrentRow":
                        item.Click += (s, e) => PostEditTableMessage(new { action = "remove", location = "current", target = "row" });
                        item.SetBinding(UIElement.VisibilityProperty, new Binding() { Path = new(nameof(RowItemVisibility)), Source = this });
                        break;
                    case "InsertLeftColumn":
                        item.Click += (s, e) => PostEditTableMessage(new { action = "insert", location = "left", target = "column" });
                        item.SetBinding(UIElement.VisibilityProperty, new Binding() { Path = new(nameof(ColumnItemVisibility)), Source = this });
                        break;
                    case "InsertRightColumn":
                        item.Click += (s, e) => PostEditTableMessage(new { action = "insert", location = "right", target = "column" });
                        item.SetBinding(UIElement.VisibilityProperty, new Binding() { Path = new(nameof(ColumnItemVisibility)), Source = this });
                        break;
                    case "RemoveVurrentColumn":
                        item.Click += (s, e) => PostEditTableMessage(new { action = "remove", location = "current", target = "column" });
                        item.SetBinding(UIElement.VisibilityProperty, new Binding() { Path = new(nameof(ColumnItemVisibility)), Source = this });
                        break;
                }
            }
        }

        private void PostEditTableMessage(object args)
        {
            markdownEditor.PostMessage("EditTable", args);
        }
    }
}
