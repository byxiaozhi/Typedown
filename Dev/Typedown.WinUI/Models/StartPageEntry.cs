using Microsoft.UI.Xaml;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI.Models
{
    // A row of the start page's file list (interface review, phase 2): the name, where it is — so two
    // files with the same name can be told apart — when it last changed, and whether it's a favourite.
    public class StartPageEntry
    {
        public StartPageEntry(string fullPath, bool isFavorite)
        {
            FullPath = fullPath;
            IsFavorite = isFavorite;
        }

        public string FullPath { get; }

        public bool IsFavorite { get; }

        public string Name => Path.GetFileName(FullPath);

        // The last two folders, "Documents › Reports", which is usually enough to recognise a place.
        public string Folder
        {
            get
            {
                var parts = (Path.GetDirectoryName(FullPath) ?? "").Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                return string.Join(" › ", parts.Skip(Math.Max(0, parts.Length - 2)));
            }
        }

        public string Modified
        {
            get
            {
                DateTime time;
                try { time = File.GetLastWriteTime(FullPath); }
                catch { return ""; }
                var culture = Locale.CurrentLang is { Length: > 0 } lang ? new CultureInfo(lang) : CultureInfo.CurrentCulture;
                var days = (DateTime.Today - time.Date).Days;
                return days switch
                {
                    0 => time.ToString("t", culture),
                    1 => Locale.GetString("Yesterday"),
                    < 7 => time.ToString("dddd", culture),
                    _ => time.ToString(time.Year == DateTime.Today.Year ? "d MMM" : "d MMM yyyy", culture),
                };
            }
        }

        public Visibility FavoriteVisibility => IsFavorite ? Visibility.Visible : Visibility.Collapsed;

        public Visibility NotFavoriteVisibility => IsFavorite ? Visibility.Collapsed : Visibility.Visible;
    }
}
