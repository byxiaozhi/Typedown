using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.SidePanelControls.Pages
{
    public sealed partial class TocPage : Page
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public EditorViewModel Editor => ViewModel?.EditorViewModel;

        public TocPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private EditorViewModel watched;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            watched = Editor;
            if (watched != null) watched.OutlineHighlighted += OnOutlineHighlighted;
        }

        // The mark is set on the item model and reaches the row through the IsSelected binding of its
        // container — and the rows are virtualized: only those in view when the pane was built have a
        // container at all. A row further down gets one when it is scrolled to, and in preparing it the
        // list resets IsSelected from its own selection state, which never heard of the model's mark. So
        // the highlight worked for the first thirty-odd headings and vanished for the rest, from 5.4 in a
        // tall window and 5.3 in a shorter one. Selecting through the tree's own SelectedNode puts the mark
        // where the list keeps it, and it survives the row being rebuilt. The row is then brought into
        // view, moving the list as little as possible.
        // The selection is the tree's alone now. The row used to bind IsSelected both ways to the item model,
        // and the tree, the binding and the model then kept each other informed of every change while the
        // whole outline was being torn down and rebuilt twice per tab switch — twenty quick switches ended
        // in a stack overflow inside Windows.UI.Xaml. Nothing writes the model's mark back from the tree.
        private bool marking;

        private void OnOutlineHighlighted(string slug)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () =>
            {
                marking = true;
                try
                {
                    var node = FindNode(TreeView.RootNodes, slug);
                    if (node == null) { Utilities.Log.Debug($"outline: no row for {slug} in the pane"); return; }
                    if (TreeView.SelectedNode != node)
                    {
                        TreeView.SelectedNode = node;
                        Utilities.Log.Debug($"outline: marked {(node.Content as Models.TocTreeItem)?.TocItem?.Content} (load {Editor?.LoadId})");
                    }
                    FindList(TreeView)?.ScrollIntoView(node);
                }
                catch (System.Exception ex) { Utilities.Log.Debug($"outline: could not mark {slug}: {ex.Message}"); }
                finally { marking = false; }
            });
        }

        // The reader moving the selection with the keyboard goes to that heading; the page marking a heading
        // (above) does not come back through here.
        // The tree raises this after the fact, not inside the SelectedNode setter, so the flag above is down
        // again by the time it arrives; and while the outline is replaced for another document it raises it
        // for rows nobody chose. What tells the reader's keyboard from all of that is focus: a reader moving
        // the selection has the tree focused, the page marking a heading does not.
        private bool TreeHasFocus()
        {
            var focused = Windows.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            while (focused != null)
            {
                if (focused == TreeView) return true;
                focused = Windows.UI.Xaml.Media.VisualTreeHelper.GetParent(focused);
            }
            return false;
        }

        private void OnSelectionChanged(Microsoft.UI.Xaml.Controls.TreeView sender, Microsoft.UI.Xaml.Controls.TreeViewSelectionChangedEventArgs args)
        {
            if (marking || !TreeHasFocus()) return;
            foreach (var added in args.AddedItems)
                if (added is Models.TocTreeItem item && item.TocItem?.Slug != null && item.TocItem.Slug != Editor?.AppliedCurSlug)
                { Editor?.JumpBySlug(item.TocItem.Slug); return; }
        }

        private static Microsoft.UI.Xaml.Controls.TreeViewNode FindNode(System.Collections.Generic.IList<Microsoft.UI.Xaml.Controls.TreeViewNode> nodes, string slug)
        {
            foreach (var node in nodes)
            {
                if ((node.Content as Models.TocTreeItem)?.TocItem?.Slug == slug) return node;
                var found = FindNode(node.Children, slug);
                if (found != null) return found;
            }
            return null;
        }

        private ListView list;

        // The TreeView shows its flattened nodes in a ListView of its own; that is what can scroll a row into view.
        private ListView FindList(DependencyObject root)
        {
            if (list != null) return list;
            var count = Windows.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = Windows.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
                if (child is ListView lv) return list = lv;
                var found = FindList(child);
                if (found != null) return found;
            }
            return null;
        }

        // Selection-change already jumps, but clicking the heading the cursor is currently in (after scrolling
        // away) changed nothing, which reads as "single click does not work" (upstream #59, #2). Always jump on click.
        private void OnItemInvoked(Microsoft.UI.Xaml.Controls.TreeView sender, Microsoft.UI.Xaml.Controls.TreeViewItemInvokedEventArgs args)
        {
            if (args.InvokedItem is Models.TocTreeItem item && item.TocItem?.Slug != null)
            {
                Editor?.JumpBySlug(item.TocItem.Slug);
                // The reader picked the heading to write there: the keys go to the text, not to the outline (where
                // they started a search in it). Moving through the outline with the arrows does not come here.
                _ = Dispatcher.RunIdleAsync(_ => ViewModel?.MarkdownEditor?.FocusEditor());
            }
        }

        private void OnExpandAllClick(object sender, RoutedEventArgs e) => Editor?.Toc?.SetExpandedRecursive(true);

        private void OnCollapseAllClick(object sender, RoutedEventArgs e) => Editor?.Toc?.SetExpandedRecursive(false);

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (watched != null) watched.OutlineHighlighted -= OnOutlineHighlighted;
            watched = null;
            list = null;
            Bindings?.StopTracking();
        }
    }
}
