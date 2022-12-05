using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Utilities;
using Key = Windows.System.VirtualKey;
using Mod = Windows.System.VirtualKeyModifiers;

namespace Typedown.Core.ViewModels
{
    public partial class SettingsViewModel
    {
        [Locale("File", "New")]
        public ShortcutKey ShortcutNewFile { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.N)); set => SetSettingValue(value); }

        [Locale("File", "NewWindow")]
        public ShortcutKey ShortcutNewWindow { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.N)); set => SetSettingValue(value); }

        [Locale("File", "Open")]
        public ShortcutKey ShortcutOpenFile { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.O)); set => SetSettingValue(value); }

        [Locale("File", "OpenFolder")]
        public ShortcutKey ShortcutOpenFolder { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("File", "ClearRecentFiles")]
        public ShortcutKey ShortcutClearRecentFiles { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("File", "Save")]
        public ShortcutKey ShortcutSave { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.S)); set => SetSettingValue(value); }

        [Locale("File", "SaveAs")]
        public ShortcutKey ShortcutSaveAs { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.S)); set => SetSettingValue(value); }

        [Locale("File", "ExportSettings")]
        public ShortcutKey ShortcutExportSettings { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("File", "Print")]
        public ShortcutKey ShortcutPrint { get => GetSettingValue<ShortcutKey>(new(Mod.Menu | Mod.Shift, Key.P)); set => SetSettingValue(value); }

        [Locale("File", "Settings")]
        public ShortcutKey ShortcutSettings { get => GetSettingValue<ShortcutKey>(new(Mod.Control, (Key)0xBC)); set => SetSettingValue(value); }

        [Locale("File", "Close")]
        public ShortcutKey ShortcutClose { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.W)); set => SetSettingValue(value); }

        [Locale("Edit", "Undo")]
        public ShortcutKey ShortcutUndo { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Z)); set => SetSettingValue(value); }

        [Locale("Edit", "Redo")]
        public ShortcutKey ShortcutRedo { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Y)); set => SetSettingValue(value); }

        [Locale("Edit", "Cut")]
        public ShortcutKey ShortcutCut { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.X)); set => SetSettingValue(value); }

        [Locale("Edit", "Copy")]
        public ShortcutKey ShortcutCopy { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.C)); set => SetSettingValue(value); }

        [Locale("Edit", "Paste")]
        public ShortcutKey ShortcutPaste { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.V)); set => SetSettingValue(value); }

        [Locale("Edit", "CopyAsPlainText")]
        public ShortcutKey ShortcutCopyAsPlainText { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("Edit", "CopyAsMarkdown")]
        public ShortcutKey ShortcutCopyAsMarkdown { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.C)); set => SetSettingValue(value); }

        [Locale("Edit", "CopyAsHTMLCode")]
        public ShortcutKey ShortcutCopyAsHTMLCode { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("Edit", "PasteAsPlainText")]
        public ShortcutKey ShortcutPasteAsPlainText { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.V)); set => SetSettingValue(value); }

        [Locale("Edit", "Delete")]
        public ShortcutKey ShortcutDelete { get => GetSettingValue<ShortcutKey>(new(Mod.None, Key.Delete)); set => SetSettingValue(value); }

        [Locale("Edit", "SelectAll")]
        public ShortcutKey ShortcutSelectAll { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.A)); set => SetSettingValue(value); }

        [Locale("Edit", "FindAndReplace", "Find")]
        public ShortcutKey ShortcutFind { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.F)); set => SetSettingValue(value); }

        [Locale("Edit", "FindAndReplace", "FindNext")]
        public ShortcutKey ShortcutFindNext { get => GetSettingValue<ShortcutKey>(new(Mod.None, Key.F3)); set => SetSettingValue(value); }

        [Locale("Edit", "FindAndReplace", "FindPrevious")]
        public ShortcutKey ShortcutFindPrevious { get => GetSettingValue<ShortcutKey>(new(Mod.Shift, Key.F3)); set => SetSettingValue(value); }

        [Locale("Edit", "FindAndReplace", "Replace")]
        public ShortcutKey ShortcutReplace { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.H)); set => SetSettingValue(value); }

        [Locale("Paragraph", "Heading", "Heading1")]
        public ShortcutKey ShortcutHeading1 { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Number1)); set => SetSettingValue(value); }

        [Locale("Paragraph", "Heading", "Heading2")]
        public ShortcutKey ShortcutHeading2 { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Number2)); set => SetSettingValue(value); }

        [Locale("Paragraph", "Heading", "Heading3")]
        public ShortcutKey ShortcutHeading3 { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Number3)); set => SetSettingValue(value); }

        [Locale("Paragraph", "Heading", "Heading4")]
        public ShortcutKey ShortcutHeading4 { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Number4)); set => SetSettingValue(value); }

        [Locale("Paragraph", "Heading", "Heading5")]
        public ShortcutKey ShortcutHeading5 { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Number5)); set => SetSettingValue(value); }

        [Locale("Paragraph", "Heading", "Heading6")]
        public ShortcutKey ShortcutHeading6 { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Number6)); set => SetSettingValue(value); }

        [Locale("Paragraph", "Paragraph")]
        public ShortcutKey ShortcutParagraph { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Number0)); set => SetSettingValue(value); }

        [Locale("Paragraph", "IncreaseHeadingLevel")]
        public ShortcutKey ShortcutIncreaseHeadingLevel { get => GetSettingValue<ShortcutKey>(new(Mod.Control, (Key)187)); set => SetSettingValue(value); }

        [Locale("Paragraph", "DecreaseHeadingLevel")]
        public ShortcutKey ShortcutDecreaseHeadingLevel { get => GetSettingValue<ShortcutKey>(new(Mod.Control, (Key)189)); set => SetSettingValue(value); }

        [Locale("Paragraph", "Table")]
        public ShortcutKey ShortcutTable { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.T)); set => SetSettingValue(value); }

        [Locale("Paragraph", "CodeFences")]
        public ShortcutKey ShortcutCodeFences { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.K)); set => SetSettingValue(value); }

        [Locale("Paragraph", "MathBlock")]
        public ShortcutKey ShortcutMathBlock { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.M)); set => SetSettingValue(value); }

        [Locale("Paragraph", "Quote")]
        public ShortcutKey ShortcutQuote { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.Q)); set => SetSettingValue(value); }

        [Locale("Paragraph", "OrderedList")]
        public ShortcutKey ShortcutOrderedList { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, (Key)219)); set => SetSettingValue(value); }

        [Locale("Paragraph", "UnorderedList")]
        public ShortcutKey ShortcutUnorderedList { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, (Key)221)); set => SetSettingValue(value); }

        [Locale("Paragraph", "TaskList")]
        public ShortcutKey ShortcutTaskList { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.X)); set => SetSettingValue(value); }

        [Locale("Paragraph", "InsertParagraphBefore")]
        public ShortcutKey ShortcutInsertParagraphBefore { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.Enter)); set => SetSettingValue(value); }

        [Locale("Paragraph", "InsertParagraphAfter")]
        public ShortcutKey ShortcutInsertParagraphAfter { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.Enter)); set => SetSettingValue(value); }

        [Locale("Paragraph", "Chart", "VegaChart")]
        public ShortcutKey ShortcutVegaChart { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("Paragraph", "Chart", "FlowChart")]
        public ShortcutKey ShortcutFlowChart { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("Paragraph", "Chart", "SequenceDiagram")]
        public ShortcutKey ShortcutSequenceDiagram { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("Paragraph", "Chart", "PlantUMLDiagram")]
        public ShortcutKey ShortcutPlantUMLDiagram { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("Paragraph", "Chart", "Mermaid")]
        public ShortcutKey ShortcutMermaid { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("Paragraph", "LinkReference")]
        public ShortcutKey ShortcutLinkReferences { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("Paragraph", "Footnote")]
        public ShortcutKey ShortcutFootNote { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("Paragraph", "HorizontalLine")]
        public ShortcutKey ShortcutHorizontalLine { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("Paragraph", "Toc")]
        public ShortcutKey ShortcutToc { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("Paragraph", "YAMLFrontMatter")]
        public ShortcutKey ShortcutYAMLFrontMatter { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("Format", "Strong")]
        public ShortcutKey ShortcutStrong { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.B)); set => SetSettingValue(value); }

        [Locale("Format", "Emphasis")]
        public ShortcutKey ShortcutEmphasis { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.I)); set => SetSettingValue(value); }

        [Locale("Format", "Underline")]
        public ShortcutKey ShortcutUnderline { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.U)); set => SetSettingValue(value); }

        [Locale("Format", "InlineCode")]
        public ShortcutKey ShortcutInlineCode { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, (Key)192)); set => SetSettingValue(value); }

        [Locale("Format", "InlineMath")]
        public ShortcutKey ShortcutInlineMath { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("Format", "Strikethrough")]
        public ShortcutKey ShortcutStrikethrough { get => GetSettingValue<ShortcutKey>(new(Mod.Menu | Mod.Shift, Key.Number5)); set => SetSettingValue(value); }

        [Locale("Format", "Highlight")]
        public ShortcutKey ShortcutHighlight { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }

        [Locale("Format", "Hyperlink")]
        public ShortcutKey ShortcutHyperlink { get => GetSettingValue<ShortcutKey>(new(Mod.Control, Key.K)); set => SetSettingValue(value); }

        [Locale("Format", "Image")]
        public ShortcutKey ShortcutImage { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.I)); set => SetSettingValue(value); }

        [Locale("Format", "ClearFormat")]
        public ShortcutKey ShortcutClearFormat { get => GetSettingValue<ShortcutKey>(new(Mod.Control, (Key)220)); set => SetSettingValue(value); }

        [Locale("View", "SidePane")]
        public ShortcutKey ShortcutSidePane { get => GetSettingValue<ShortcutKey>(new(Mod.Control | Mod.Shift, Key.L)); set => SetSettingValue(value); }

        [Locale("View", "SourceCodeMode")]
        public ShortcutKey ShortcutSourceCodeMode { get => GetSettingValue<ShortcutKey>(new(Mod.Control, (Key)191)); set => SetSettingValue(value); }

        [Locale("View", "FocusMode")]
        public ShortcutKey ShortcutFocusMode { get => GetSettingValue<ShortcutKey>(new(Mod.None, Key.F8)); set => SetSettingValue(value); }

        [Locale("View", "TypewriterMode")]
        public ShortcutKey ShortcutTypewriterMode { get => GetSettingValue<ShortcutKey>(new(Mod.None, Key.F9)); set => SetSettingValue(value); }

        [Locale("View", "StatusBar")]
        public ShortcutKey ShortcutStatusBar { get => GetSettingValue<ShortcutKey>(null); set => SetSettingValue(value); }
    }
}
