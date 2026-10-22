using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Typedown.WinUI.Models;

namespace Typedown.WinUI
{
    // Ported from Typedown.Core\Controls\SidePaneControls\Pages\FolderPage.xaml.cs's
    // ExplorerItemTemplateSelector — picks the folder or file row DataTemplate for MainWindow.xaml's
    // FolderTreeView based on ExplorerItem.Type.
    public class ExplorerItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate FolderTemplate { get; set; }
        public DataTemplate FileTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item) =>
            item is ExplorerItem explorerItem && explorerItem.Type == ExplorerItem.ExplorerItemType.Folder
                ? FolderTemplate
                : FileTemplate;
    }
}
