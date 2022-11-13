using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Pages.SettingPages;

namespace Typedown.Universal.Pages
{
    public static class Route
    {
        public static Type GetRootPageType(string name) => name switch
        {
            "Main" => typeof(MainPage),
            "Settings" => typeof(SettingsPage),
            _ => null
        };

        public static Type GetSettingsPageType(string name) => name switch
        {
            "About" => typeof(AboutPage),
            "Editor" => typeof(EditorPage),
            "Export" => typeof(ExportPage),
            "General" => typeof(GeneralPage),
            "Shortcut" => typeof(ShortcutPage),
            "MenuBar" => typeof(MenuBarPage),
            "SidePane" => typeof(SidePanePage),
            "StatusBar" => typeof(StatusBarPage),
            "Image" => typeof(ImagePage),
            "View" => typeof(ViewPage),
            _ => null
        };
    }
}
