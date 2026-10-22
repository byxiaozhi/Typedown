using System;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI.Models
{
    // A row of the Folders column of the Home page: the folder's name, where it is (the last two folders above it, "Documents › Reports"), and whether it
    // is pinned.
    public class HomeFolderEntry
    {
        public HomeFolderEntry(string fullPath, bool isPinned)
        {
            FullPath = fullPath;
            IsPinned = isPinned;
        }

        public string FullPath { get; }

        public bool IsPinned { get; }

        public string Name => Path.GetFileName(FullPath) is { Length: > 0 } name ? name : FullPath;

        public string Parent
        {
            get
            {
                var parts = (Path.GetDirectoryName(FullPath) ?? "").Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                return string.Join(" › ", parts.Skip(Math.Max(0, parts.Length - 2)));
            }
        }

        public Visibility PinnedVisibility => IsPinned ? Visibility.Visible : Visibility.Collapsed;

        public Visibility NotPinnedVisibility => IsPinned ? Visibility.Collapsed : Visibility.Visible;

        public double PinRestOpacity => IsPinned ? 1 : 0;

        public string PinName => Locale.GetString(IsPinned ? "HomeUnpin" : "HomePin");
    }
}
