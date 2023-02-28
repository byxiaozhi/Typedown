using System.Collections.Generic;
using System.Reactive.Disposables;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Services;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Key = Windows.System.VirtualKey;
using Mod = Windows.System.VirtualKeyModifiers;

namespace Typedown.Core.Controls.EditorControls.MenuBarItems
{
    public sealed partial class EditItem : MenuBarItemBase
    {
        public EditorViewModel Editor => ViewModel?.EditorViewModel;

        public FloatViewModel Float => ViewModel?.FloatViewModel;

        public FloatViewModel.FindReplaceDialogState OpenSearch => FloatViewModel.FindReplaceDialogState.Search;
        public FloatViewModel.FindReplaceDialogState OpenReplace => FloatViewModel.FindReplaceDialogState.Replace;

        private readonly CompositeDisposable disposables = new();

        public AccessHistory FileHistory => this.GetService<AccessHistory>();

        public HashSet<ShortcutKey> handledKey;

        public EditItem()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var acc = this.GetService<IKeyboardAccelerator>();
            disposables.Add(acc.RegisterGlobal((s, e) =>
            {
                handledKey ??= new()
                {
                    new(Mod.Control, Key.Z),
                    new(Mod.Control, Key.Y),
                    new(Mod.Control, Key.X),
                    new(Mod.Control, Key.C),
                    new(Mod.Control, Key.V),
                    new(Mod.Control, Key.A),
                    // new(Mod.None, Key.Delete),
                };
                if (handledKey.Contains(new(e.Modifiers, e.Key)))
                {
                    var editor = this.GetService<IMarkdownEditor>();
                    var focused = FocusManager.GetFocusedElement(XamlRoot);
                    if (focused == editor)
                        e.Handled = true;
                }
            }));
        }

        protected override void OnRegisterShortcut()
        {
            RegisterEditorShortcut(Settings.ShortcutUndo, UndoItem);
            RegisterEditorShortcut(Settings.ShortcutRedo, RedoItem);
            RegisterEditorShortcut(Settings.ShortcutCut, CutItem);
            RegisterEditorShortcut(Settings.ShortcutCopy, CopyItem);
            RegisterEditorShortcut(Settings.ShortcutPaste, PasteItem);
            RegisterEditorShortcut(Settings.ShortcutCopyAsPlainText, CopyAsPlainTextItem);
            RegisterEditorShortcut(Settings.ShortcutCopyAsMarkdown, CopyAsMarkdownItem);
            RegisterEditorShortcut(Settings.ShortcutCopyAsHTMLCode, CopyAsHTMLCodeItem);
            RegisterEditorShortcut(Settings.ShortcutPasteAsPlainText, PasteAsPlainTextItem);
            // RegisterEditorShortcut(Settings.ShortcutDelete, DeleteItem);
            RegisterEditorShortcut(Settings.ShortcutSelectAll, SelectAllItem);
            RegisterWindowShortcut(Settings.ShortcutFind, FindItem);
            RegisterWindowShortcut(Settings.ShortcutFindNext, FindNextItem);
            RegisterWindowShortcut(Settings.ShortcutFindPrevious, FindPreviousItem);
            RegisterWindowShortcut(Settings.ShortcutReplace, ReplaceItem);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
            Bindings.StopTracking();
        }
    }
}
