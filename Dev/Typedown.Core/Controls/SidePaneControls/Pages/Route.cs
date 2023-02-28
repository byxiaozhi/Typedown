using System;
using Typedown.Core.Controls.SidePanelControls.Pages;

namespace Typedown.Core.Controls.SidePaneControls.Pages
{
    public static class Route
    {
        public static Type GetSidePanePageType(string name) => name switch
        {
            "Toc" => typeof(TocPage),
            "Folder" => typeof(FolderPage),
            _ => null
        };
    }
}
