using System;
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
            "ExportConfig" => typeof(ExportConfigPage),
            "Export" => typeof(ExportPage),
            "General" => typeof(GeneralPage),
            "Image" => typeof(ImagePage),
            "ImageUpload" => typeof(ImageUploadPage),
            "Shortcut" => typeof(ShortcutPage),
            "UploadConfig" => typeof(UploadConfigPage),
            "View" => typeof(ViewPage),
            _ => null
        };
    }
}
