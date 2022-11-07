using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Typedown.Universal.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Typedown.Universal.Controls.EditorControls.MenuBarItems
{
    public sealed partial class ParagraphItem : MenuBarItemBase
    {
        public ParagraphViewModel Paragraph => ViewModel?.ParagraphViewModel;

        public EditorViewModel Editor => ViewModel?.EditorViewModel;

        public ParagraphItem()
        {
            InitializeComponent();
        }

        protected override void OnRegisterShortcut()
        {
            RegisterEditorShortcut(Settings.ShortcutHeading1, Heading1Item);
            RegisterEditorShortcut(Settings.ShortcutHeading2, Heading2Item);
            RegisterEditorShortcut(Settings.ShortcutHeading3, Heading3Item);
            RegisterEditorShortcut(Settings.ShortcutHeading4, Heading4Item);
            RegisterEditorShortcut(Settings.ShortcutHeading5, Heading5Item);
            RegisterEditorShortcut(Settings.ShortcutHeading6, Heading6Item);
            RegisterEditorShortcut(Settings.ShortcutParagraph, ItemParagraphItem);
            RegisterEditorShortcut(Settings.ShortcutIncreaseHeadingLevel, IncreaseHeadingLevelItem);
            RegisterEditorShortcut(Settings.ShortcutDecreaseHeadingLevel, DecreaseHeadingLevelItem);
            RegisterEditorShortcut(Settings.ShortcutTable, TableItem);
            RegisterEditorShortcut(Settings.ShortcutCodeFences, CodeFencesItem);
            RegisterEditorShortcut(Settings.ShortcutMathBlock, MathBlockItem);
            RegisterEditorShortcut(Settings.ShortcutQuote, QuoteItem);
            RegisterEditorShortcut(Settings.ShortcutOrderedList, OrderedListItem);
            RegisterEditorShortcut(Settings.ShortcutUnordered, UnorderedItem);
            RegisterEditorShortcut(Settings.ShortcutTaskList, TaskListItem);
            RegisterEditorShortcut(Settings.ShortcutInsertParagraphBefore, InsertParagraphBeforeItem);
            RegisterEditorShortcut(Settings.ShortcutInsertParagraphAfter, InsertParagraphAfterItem);
            RegisterEditorShortcut(Settings.ShortcutVegaChart, VegaChartItem);
            RegisterEditorShortcut(Settings.ShortcutFlowChart, FlowChartItem);
            RegisterEditorShortcut(Settings.ShortcutSequenceDiagram, SequenceDiagramItem);
            RegisterEditorShortcut(Settings.ShortcutPlantUMLDiagram, PlantUMLDiagramItem);
            RegisterEditorShortcut(Settings.ShortcutMermaid, MermaidItem);
            RegisterEditorShortcut(Settings.ShortcutLinkReferences, LinkReferencesItem);
            RegisterEditorShortcut(Settings.ShortcutFootNote, FootNoteItem);
            RegisterEditorShortcut(Settings.ShortcutHorizontalLine, HorizontalLineItem);
            RegisterEditorShortcut(Settings.ShortcutToc, TocItem);
            RegisterEditorShortcut(Settings.ShortcutYAMLFrontMatter, YAMLFrontMatterItem);
        }
    }
}
