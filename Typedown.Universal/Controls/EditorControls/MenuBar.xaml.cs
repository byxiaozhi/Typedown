using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reactive.Disposables;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Models;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Typedown.Universal.Controls
{
    public sealed partial class MenuBar : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;
        public EditorViewModel Editor => ViewModel?.EditorViewModel;
        public FileViewModel File => ViewModel?.FileViewModel;
        public FloatViewModel Float => ViewModel?.FloatViewModel;
        public FormatViewModel Format => ViewModel?.FormatViewModel;
        public ParagraphViewModel Paragraph => ViewModel?.ParagraphViewModel;
        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        public FloatViewModel.SearchOpenType OpenSearch => FloatViewModel.SearchOpenType.Search;
        public FloatViewModel.SearchOpenType OpenReplace => FloatViewModel.SearchOpenType.Replace;

        private readonly CompositeDisposable disposables = new();

        public MenuBar()
        {
            InitializeComponent();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await Dispatcher.RunIdleAsync(_ => HotkeyRegister());
        }

        private void HotkeyRegister()
        {
            void RegisterWindow(ShortcutKey key, MenuFlyoutItem item) => RegisterMenuItemShortcut(OnWindowShortcutEvent, key, item);
            void RegisterEditor(ShortcutKey key, MenuFlyoutItem item) => RegisterMenuItemShortcut(OnEditorShortcutEvent, key, item);
            RegisterWindow(Settings.ShortcutNewFile, NewFileItem);
            RegisterWindow(Settings.ShortcutNewWindow, NewWindowItem);
            RegisterWindow(Settings.ShortcutOpenFile, OpenFileItem);
            RegisterWindow(Settings.ShortcutOpenFolder, OpenFolderItem);
            RegisterWindow(Settings.ShortcutClearRecentFiles, ClearRecentFilesItem);
            RegisterWindow(Settings.ShortcutSave, SaveItem);
            RegisterWindow(Settings.ShortcutSaveAs, SaveAsItem);
            RegisterWindow(Settings.ShortcutExportSettings, ExportSettingsItem);
            RegisterWindow(Settings.ShortcutPrint, PrintItem);
            RegisterWindow(Settings.ShortcutClose, CloseItem);
            RegisterEditor(Settings.ShortcutUndo, UndoItem);
            RegisterEditor(Settings.ShortcutRedo, RedoItem);
            RegisterEditor(Settings.ShortcutCut, CutItem);
            RegisterEditor(Settings.ShortcutCopy, CopyItem);
            RegisterEditor(Settings.ShortcutPaste, PasteItem);
            RegisterEditor(Settings.ShortcutCopyAsPlainText, CopyAsPlainTextItem);
            RegisterEditor(Settings.ShortcutCopyAsMarkdown, CopyAsMarkdownItem);
            RegisterEditor(Settings.ShortcutCopyAsHTMLCode, CopyAsHTMLCodeItem);
            RegisterEditor(Settings.ShortcutPasteAsPlainText, PasteAsPlainTextItem);
            RegisterEditor(Settings.ShortcutDelete, DeleteItem);
            RegisterEditor(Settings.ShortcutSelectAll, SelectAllItem);
            RegisterEditor(Settings.ShortcutFind, FindItem);
            RegisterEditor(Settings.ShortcutFindNext, FindNextItem);
            RegisterEditor(Settings.ShortcutFindPrevious, FindPreviousItem);
            RegisterEditor(Settings.ShortcutReplace, ReplaceItem);
            RegisterEditor(Settings.ShortcutHeading1, Heading1Item);
            RegisterEditor(Settings.ShortcutHeading2, Heading2Item);
            RegisterEditor(Settings.ShortcutHeading3, Heading3Item);
            RegisterEditor(Settings.ShortcutHeading4, Heading4Item);
            RegisterEditor(Settings.ShortcutHeading5, Heading5Item);
            RegisterEditor(Settings.ShortcutHeading6, Heading6Item);
            RegisterEditor(Settings.ShortcutParagraph, ParagraphItem);
            RegisterEditor(Settings.ShortcutIncreaseHeadingLevel, IncreaseHeadingLevelItem);
            RegisterEditor(Settings.ShortcutDecreaseHeadingLevel, DecreaseHeadingLevelItem);
            RegisterEditor(Settings.ShortcutTable, TableItem);
            RegisterEditor(Settings.ShortcutCodeFences, CodeFencesItem);
            RegisterEditor(Settings.ShortcutMathBlock, MathBlockItem);
            RegisterEditor(Settings.ShortcutQuote, QuoteItem);
            RegisterEditor(Settings.ShortcutOrderedList, OrderedListItem);
            RegisterEditor(Settings.ShortcutUnordered, UnorderedItem);
            RegisterEditor(Settings.ShortcutTaskList, TaskListItem);
            RegisterEditor(Settings.ShortcutInsertParagraphBefore, InsertParagraphBeforeItem);
            RegisterEditor(Settings.ShortcutInsertParagraphAfter, InsertParagraphAfterItem);
            RegisterEditor(Settings.ShortcutVegaChart, VegaChartItem);
            RegisterEditor(Settings.ShortcutFlowChart, FlowChartItem);
            RegisterEditor(Settings.ShortcutSequenceDiagram, SequenceDiagramItem);
            RegisterEditor(Settings.ShortcutPlantUMLDiagram, PlantUMLDiagramItem);
            RegisterEditor(Settings.ShortcutMermaid, MermaidItem);
            RegisterEditor(Settings.ShortcutLinkReferences, LinkReferencesItem);
            RegisterEditor(Settings.ShortcutFootNote, FootNoteItem);
            RegisterEditor(Settings.ShortcutHorizontalLine, HorizontalLineItem);
            RegisterEditor(Settings.ShortcutToc, TocItem);
            RegisterEditor(Settings.ShortcutYAMLFrontMatter, YAMLFrontMatterItem);
            RegisterEditor(Settings.ShortcutStrong, StrongItem);
            RegisterEditor(Settings.ShortcutEmphasis, EmphasisItem);
            RegisterEditor(Settings.ShortcutUnderline, UnderlineItem);
            RegisterEditor(Settings.ShortcutInlineCode, InlineCodeItem);
            RegisterEditor(Settings.ShortcutInlineMath, InlineMathItem);
            RegisterEditor(Settings.ShortcutStrikethrough, StrikethroughItem);
            RegisterEditor(Settings.ShortcutHighlight, HighlightItem);
            RegisterEditor(Settings.ShortcutHyperlink, HyperlinkItem);
            RegisterEditor(Settings.ShortcutImage, ImageItem);
            RegisterEditor(Settings.ShortcutClearFormat, ClearFormatItem);
            RegisterWindow(Settings.ShortcutSidePane, SidePaneItem);
            RegisterWindow(Settings.ShortcutSourceCodeMode, SourceCodeModeItem);
            RegisterWindow(Settings.ShortcutFocusMode, FocusModeItem);
            RegisterWindow(Settings.ShortcutTypewriterMode, TypewriterModeItem);
            RegisterWindow(Settings.ShortcutStatusBar, StatusBarItem);
        }

        private void RegisterMenuItemShortcut(Func<MenuFlyoutItem, bool> handler, ShortcutKey key, MenuFlyoutItem item)
        {
            var acc = this.GetService<IKeyboardAccelerator>();
            item.KeyboardAcceleratorTextOverride = acc.GetShortcutKeyText(key);
            disposables.Add(acc.Register(key, (s, e) =>
            {
                if (handler(item))
                    e.Handled = true;
            }));
        }

        private bool OnWindowShortcutEvent(MenuFlyoutItem item)
        {
            var windowService = this.GetService<IWindowService>();
            var focused = windowService.GetForegroundWindow();
            if (focused != ViewModel.MainWindow)
                return false;
            TriggerMenuFlyoutItem(item);
            return true;
        }

        private bool OnEditorShortcutEvent(MenuFlyoutItem item)
        {
            var editor = this.GetService<IMarkdownEditor>();
            var focused = FocusManager.GetFocusedElement(XamlRoot);
            if (focused != editor)
                return false;
            TriggerMenuFlyoutItem(item);
            return true;
        }

        private void TriggerMenuFlyoutItem(MenuFlyoutItem item)
        {
            item.Command?.Execute(item.CommandParameter);
            if (item is ToggleMenuFlyoutItem toggle)
                toggle.IsChecked = !toggle.IsChecked;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
        }
    }
}
