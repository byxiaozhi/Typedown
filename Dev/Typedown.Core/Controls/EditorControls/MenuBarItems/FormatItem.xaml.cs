using Typedown.Core.ViewModels;

namespace Typedown.Core.Controls.EditorControls.MenuBarItems
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

        private void OnUnloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Bindings?.StopTracking();
        }
    }
}
