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
    public sealed partial class FormatItem : MenuBarItemBase
    {
        public FormatViewModel Format => ViewModel?.FormatViewModel;

        public EditorViewModel Editor => ViewModel?.EditorViewModel;

        public FormatItem()
        {
            InitializeComponent();
        }

        protected override void OnRegisterShortcut()
        {
            RegisterEditorShortcut(Settings.ShortcutStrong, StrongItem);
            RegisterEditorShortcut(Settings.ShortcutEmphasis, EmphasisItem);
            RegisterEditorShortcut(Settings.ShortcutUnderline, UnderlineItem);
            RegisterEditorShortcut(Settings.ShortcutInlineCode, InlineCodeItem);
            RegisterEditorShortcut(Settings.ShortcutInlineMath, InlineMathItem);
            RegisterEditorShortcut(Settings.ShortcutStrikethrough, StrikethroughItem);
            RegisterEditorShortcut(Settings.ShortcutHighlight, HighlightItem);
            RegisterEditorShortcut(Settings.ShortcutHyperlink, HyperlinkItem);
            RegisterEditorShortcut(Settings.ShortcutImage, ImageItem);
            RegisterEditorShortcut(Settings.ShortcutClearFormat, ClearFormatItem);
        }
    }
}
