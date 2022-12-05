using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Typedown.Core.Models
{
    public partial class TocItem : INotifyPropertyChanged
    {
        public string Slug { get; set; }

        public int Lvl { get; set; }

        public string Content { get; set; }

        [OnChangedMethod(nameof(OnIsSelectedChanged))]
        public bool IsSelected { get; set; }

        public event EventHandler<bool> SelectedChanged;

        private void OnIsSelectedChanged()
        {
            SelectedChanged?.Invoke(this, IsSelected);
        }
    }

    public partial class TocTreeItem : INotifyPropertyChanged
    {
        public TocItem TocItem { get; set; }

        public int Depth { get; set; } = 1;

        public bool IsExpanded { get; set; } = true;

        public ObservableCollection<TocTreeItem> Children { get; } = new();

        public void UpdateChildren(List<TocItem> tocList, int depth = 1)
        {
            Depth = depth;
            if (!tocList.Any())
            {
                Children.Clear();
                return;
            }
            var (generate, close) = GetTocTreeItemGenerator();
            var queue = new Queue<TocItem>(tocList);
            var descendants = new List<TocItem>();
            var parent = queue.Dequeue();
            var child = generate(parent);
            while (queue.TryDequeue(out var item))
            {
                if (item.Lvl > parent.Lvl)
                {
                    descendants.Add(item);
                }
                else
                {
                    child.UpdateChildren(descendants, depth + 1);
                    descendants.Clear();
                    parent = item;
                    child = generate(parent);
                }
            }
            child.UpdateChildren(descendants, depth + 1);
            close();
        }

        public (Func<TocItem, TocTreeItem>, Action) GetTocTreeItemGenerator()
        {
            var index = 0;
            var generate = (TocItem item) =>
            {
                while (index < Children.Count)
                {
                    if (Children[index].TocItem.Slug == item.Slug)
                    {
                        var reuseItem = Children[index];
                        reuseItem.TocItem = item;
                        index++;
                        return reuseItem;
                    }
                    else
                    {
                        Children.RemoveAt(index);
                    }
                }
                var newItem = new TocTreeItem() { TocItem = item };
                Children.Add(newItem);
                index++;
                return newItem;
            };
            var close = () =>
            {
                while (index < Children.Count)
                    Children.RemoveAt(Children.Count - 1);
            };
            return (generate, close);
        }
    }
}
