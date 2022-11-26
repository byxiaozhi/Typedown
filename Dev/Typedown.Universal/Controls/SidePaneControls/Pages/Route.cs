using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Controls.SidePanelControls.Pages;
using Typedown.Universal.Pages;

namespace Typedown.Universal.Controls.SidePaneControls.Pages
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
