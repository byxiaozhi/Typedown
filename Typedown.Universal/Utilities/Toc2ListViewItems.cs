using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.ViewModels;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml;

namespace Typedown.Universal.Utilities
{
    public static class Toc2ListViewItems
    {
        public static void Convert(EditorViewModel editor, JToken toc, ObservableCollection<ListViewItem> TocListViewItems)
        {
            for (var i = 0; i < TocListViewItems.Count && i < toc.Count(); i++)
            {
                var content = toc[i]["content"].ToString();
                var lvl = toc[i]["lvl"].ToObject<int>();
                var slug = toc[i]["slug"].ToObject<string>();
                var textBlock = TocListViewItems[i].Content as TextBlock;
                textBlock.Text = content;
                textBlock.Name = lvl.ToString();
                textBlock.Margin = new Thickness(16 * (lvl - 1), 0, 0, 0);
            }
            var start = toc.Count();
            var total = TocListViewItems.Count;
            for (var i = start; i < total; i++)
            {
                TocListViewItems.RemoveAt(start);
            }
            for (var i = TocListViewItems.Count; i < toc.Count(); i++)
            {
                var content = toc[i]["content"].ToString();
                var lvl = toc[i]["lvl"].ToObject<int>();
                var slug = toc[i]["slug"].ToObject<string>();
                var listViewItem = new ListViewItem()
                {
                    Content = new TextBlock()
                    {
                        Text = content,
                        Name = lvl.ToString(),
                        Margin = new Thickness(16 * (lvl - 1), 0, 0, 0),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    },
                    Height = 8,
                    AllowFocusOnInteraction = false,
                };
                int idx = i;
                listViewItem.Tapped += (object sender, TappedRoutedEventArgs arg) => editor.JumpByIndex(idx);
                TocListViewItems.Add(listViewItem);
            }
        }
    }
}
