using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.InteropServices.WindowsRuntime;
using Typedown.Universal.Services;
using Typedown.Universal.Utilities;
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
    public sealed partial class EditItem : MenuBarItemBase
    {
        public EditorViewModel Editor => ViewModel?.EditorViewModel;

        public FloatViewModel Float => ViewModel?.FloatViewModel;

        public FloatViewModel.FindReplaceDialogState OpenSearch => FloatViewModel.FindReplaceDialogState.Search;
        public FloatViewModel.FindReplaceDialogState OpenReplace => FloatViewModel.FindReplaceDialogState.Replace;

        private readonly CompositeDisposable disposables = new();

        public AccessHistory FileHistory => this.GetService<AccessHistory>();

        public EditItem()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            
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
            RegisterEditorShortcut(Settings.ShortcutDelete, DeleteItem);
            RegisterEditorShortcut(Settings.ShortcutSelectAll, SelectAllItem);
            RegisterWindowShortcut(Settings.ShortcutFind, FindItem);
            RegisterWindowShortcut(Settings.ShortcutFindNext, FindNextItem);
            RegisterWindowShortcut(Settings.ShortcutFindPrevious, FindPreviousItem);
            RegisterWindowShortcut(Settings.ShortcutReplace, ReplaceItem);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
        }
    }
}
