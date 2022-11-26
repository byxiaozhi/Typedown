using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Utilities
{
    public static class CollectionExtensions
    {
        public static void InsertByOrder<T>(this IList<T> list, T item, Func<T, T, int> cmp)
        {
            int i = 0;
            while (i < list.Count && cmp(item, list[i]) >= 0)
                i++;
            list.Insert(i, item);
        }

        public static void UpdateList<T>(this IList<T> collection, IList<T> source)
        {
            collection.Clear();
            source.ToList().ForEach(collection.Add);
        }

        public static void UpdateCollection<T>(this ObservableCollection<T> collection, IList<T> source, Func<T, T, bool> equals)
        {
            foreach (var item in collection.Where(x => source.All(y => !equals(x, y))).ToList())
            {
                collection.Remove(item);
            }

            int i = 0;
            foreach (var item in source)
            {
                if (collection.Count > i && equals(collection[i], item))
                {
                    item.CopyProperties(collection[i]);
                }
                else
                {
                    var oldItem = collection.Where(x => equals(x, item)).FirstOrDefault();
                    if (oldItem != null)
                    {
                        item.CopyProperties(oldItem);
                        collection.Move(collection.IndexOf(oldItem), i);
                    }
                    else
                    {
                        collection.Insert(i, item);
                    }
                }
                i++;
            }
        }
    }
}
