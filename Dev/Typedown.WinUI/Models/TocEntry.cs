using Microsoft.UI.Xaml;

namespace Typedown.WinUI.Models
{
    // Not present in the original as a standalone type — Typedown.Core built a full TocTreeItem/TocItem
    // tree (Models/RuntimeModels/TocItem.cs) for its nested TreeView. This is the flat equivalent for
    // our flat, indented ListView (see MainWindow.xaml's TocListView).
    public class TocEntry
    {
        public string Content { get; set; }
        public string Slug { get; set; }
        public int Lvl { get; set; }
        public Thickness Indent => new(Lvl <= 1 ? 8 : (Lvl - 1) * 16 + 8, 0, 4, 0);
    }
}
