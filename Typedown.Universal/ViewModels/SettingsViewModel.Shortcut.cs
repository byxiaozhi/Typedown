using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Models;
using Typedown.Universal.Utilities;
using Key = Windows.System.VirtualKey;
using Mod = Windows.System.VirtualKeyModifiers;

namespace Typedown.Universal.ViewModels
{
    public partial class SettingsViewModel
    {
        [Locale("File/Title", "New/Text")]
        public ShortcutKey ShortcutNewFile { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.N)); set => SetSettingValue(value); }

        [Locale("File/Title", "NewWindow/Text")]
        public ShortcutKey ShortcutNewWindow { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.N)); set => SetSettingValue(value); }

        [Locale("File/Title", "Open/Text")]
        public ShortcutKey ShortcutOpenFile { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.O)); set => SetSettingValue(value); }

        [Locale("File/Title", "OpenFolder/Text")]
        public ShortcutKey ShortcutOpenFolder { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("File/Title", "ClearRecentFiles/Text")]
        public ShortcutKey ShortcutClearRecentFiles { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("File/Title", "Save/Text")]
        public ShortcutKey ShortcutSave { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.S)); set => SetSettingValue(value); }

        [Locale("File/Title", "SaveAs/Text")]
        public ShortcutKey ShortcutSaveAs { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.S)); set => SetSettingValue(value); }

        [Locale("File/Title", "ExportSettings/Text")]
        public ShortcutKey ShortcutExportSettings { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("File/Title", "Print/Text")]
        public ShortcutKey ShortcutPrint { get => GetSettingValue<ShortcutKey>(new(Mod.Menu | Mod.Shift, Key.P)); set => SetSettingValue(value); }

        [Locale("File/Title", "Settings/Text")]
        public ShortcutKey ShortcutSettings { get => GetSettingValue<ShortcutKey>(new(Mod.Control, (Key)0xBC)); set => SetSettingValue(value); }

        [Locale("File/Title", "Close/Text")]
        public ShortcutKey ShortcutClose { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.W)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutUndo { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Z)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutRedo { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Y)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutCut { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.X)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutCopy { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.C)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutPaste { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.V)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutCopyAsPlainText { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }
        public ShortcutKey ShortcutCopyAsMarkdown { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.C)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutCopyAsHTMLCode { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }
        public ShortcutKey ShortcutPasteAsPlainText { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.V)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutDelete { get => GetSettingValue<ShortcutKey>(new(Mod.None, Key.Delete)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutSelectAll { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.A)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutFind { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.F)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutFindNext { get => GetSettingValue<ShortcutKey>(new(Mod.None, Key.F3)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutFindPrevious { get => GetSettingValue<ShortcutKey>(new(Mod.Shift, Key.F3)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutReplace { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.H)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutHeading1 { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Number1)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutHeading2 { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Number2)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutHeading3 { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Number3)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutHeading4 { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Number4)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutHeading5 { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Number5)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutHeading6 { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Number6)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutParagraph { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Number0)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutIncreaseHeadingLevel { get => GetSettingValue<ShortcutKey>(new(Mod.Control, (Key)187)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutDecreaseHeadingLevel { get => GetSettingValue<ShortcutKey>(new(Mod.Control, (Key)189)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutTable { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.T)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutCodeFences { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.K)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutMathBlock { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.M)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutQuote { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.Q)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutOrderedList { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, (Key)219)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutUnorderedList { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, (Key)221)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutTaskList { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.X)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutInsertParagraphBefore { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.Enter)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutInsertParagraphAfter { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Enter)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutVegaChart { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }
        public ShortcutKey ShortcutFlowChart { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }
        public ShortcutKey ShortcutSequenceDiagram { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }
        public ShortcutKey ShortcutPlantUMLDiagram { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }
        public ShortcutKey ShortcutMermaid { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }
        public ShortcutKey ShortcutLinkReferences { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }
        public ShortcutKey ShortcutFootNote { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }
        public ShortcutKey ShortcutHorizontalLine { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }
        public ShortcutKey ShortcutToc { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }
        public ShortcutKey ShortcutYAMLFrontMatter { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }
        public ShortcutKey ShortcutStrong { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.B)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutEmphasis { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.I)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutUnderline { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.U)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutInlineCode { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, (Key)192)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutInlineMath { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }
        public ShortcutKey ShortcutStrikethrough { get => GetSettingValue<ShortcutKey>(new(Mod.Menu | Mod.Shift, Key.Number5)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutHighlight { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }
        public ShortcutKey ShortcutHyperlink { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.K)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutImage { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.I)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutClearFormat { get => GetSettingValue<ShortcutKey>(new(Mod.Control, (Key)220)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutSidePane { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.L)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutSourceCodeMode { get => GetSettingValue<ShortcutKey>(new(Mod.Control, (Key)191)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutFocusMode { get => GetSettingValue<ShortcutKey>(new(Mod.None, Key.F8)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutTypewriterMode { get => GetSettingValue<ShortcutKey>(new(Mod.None, Key.F9)); set => SetSettingValue(value); }
        public ShortcutKey ShortcutStatusBar { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }
    }
}
