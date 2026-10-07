using System.IO;

namespace Typedown.WinUI.Models
{
    // Display item for the sidebar's Recent/Favorites/Templates panels (MainWindow.xaml's
    // RecentNavListView/FavoritesNavListView/TemplatesNavListView) — those lists are backed by plain
    // List<string> paths (RecentFilesService.Files, FavoritesService.Files, a Templates folder
    // listing), and a ListView showing raw paths one after another isn't very readable. This just
    // pairs each path with the filename to actually display.
    public class NavFileEntry
    {
        public string FullPath { get; }
        public string Name => Path.GetFileName(FullPath);

        public NavFileEntry(string fullPath) => FullPath = fullPath;
    }
}
