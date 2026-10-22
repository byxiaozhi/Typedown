using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Typedown.WinUI.Models;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;
using Windows.ApplicationModel.DataTransfer;

namespace Typedown.WinUI
{
    // New since the fork: where a file is (Services/FileLocation.cs). What Word and Excel have in their Info page, for the file that is
    // open (File menu, the tab) and for the files in the lists of recent files, favorites and Go to File, on a right-click: open the
    // folder with the file selected, copy the path, copy a link to the file.
    public sealed partial class MainWindow
    {
        // The folder with the file selected; the folder alone when the file is not there any more, and a message when nothing is.
        private void RevealFile(string path)
        {
            var (outcome, arguments) = FileLocation.Reveal(path);
            if (outcome == FileLocation.Outcome.NotFound)
            {
                ShowToast(Locale.GetString("FileLocationNothing"), 6000);
                return;
            }
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", arguments);
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                Log($"FileLocation: explorer did not start: {ex.Message}");
                return;
            }
            if (outcome == FileLocation.Outcome.OpenedFolder) ShowToast(Locale.GetString("FileLocationFolderOnly"), 6000);
        }

        private void CopyFileText(string text, string message)
        {
            if (string.IsNullOrEmpty(text)) return;
            var package = new DataPackage();
            package.SetText(text);
            try
            {
                Clipboard.SetContent(package);
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException)
            {
                // another program holds the clipboard: say so, and do not say the text was copied
                Log($"FileLocation: the clipboard could not be written: {ex.Message}");
                ShowToast(Locale.GetString("FileLocationCopyFailed"), 6000);
                return;
            }
            try { Clipboard.Flush(); } catch { /* the text is on the clipboard; Flush only keeps it after Caret closes */ }
            ShowToast(Locale.GetString(message));
        }

        private void CopyFilePath(string path) => CopyFileText(path, "FileLocationPathCopied");

        private void CopyFileLink(string path) => CopyFileText(FileLocation.Link(path), "FileLocationLinkCopied");

        // The right-click menu of an entry of a list of files (NavFileEntry): reveal, copy the path, copy the link.
        private void AttachFileLocationMenu(ListView list) => list.ContextRequested += FileEntry_ContextRequested;

        private void FileEntry_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            // the lists show a NavFileEntry (sidebar, Go to File) or a StartPageEntry (start page); both know the path
            var context = (args.OriginalSource as FrameworkElement)?.DataContext;
            var path = context switch { NavFileEntry nav => nav.FullPath, StartPageEntry start => start.FullPath, _ => null };
            if (string.IsNullOrEmpty(path)) return;
            var menu = BuildFileLocationMenu(path);
            if (args.TryGetPosition(sender, out var point)) menu.ShowAt(sender, new FlyoutShowOptions { Position = point });
            else menu.ShowAt((FrameworkElement)args.OriginalSource); // from the keyboard (the menu key, Shift+F10)
            args.Handled = true;
        }

        private MenuFlyout BuildFileLocationMenu(string path)
        {
            var menu = new MenuFlyout();
            MenuFlyoutItem Item(string key, Action action)
            {
                var item = new MenuFlyoutItem { Text = Locale.GetString(key) };
                item.Click += (s, e) => action();
                return item;
            }
            menu.Items.Add(Item("RevealInFileExplorer", () => RevealFile(path)));
            menu.Items.Add(Item("CopyAsPath", () => CopyFilePath(path)));
            menu.Items.Add(Item("CopyFileLink", () => CopyFileLink(path)));
            return menu;
        }

        private void InitFileLocationMenus()
        {
            foreach (var list in new[] { RecentNavListView, FavoritesNavListView, StartPageRecentList, QuickOpenListView })
                AttachFileLocationMenu(list);
        }

        // File menu: for the document on screen.
        private void FileRevealMenuItem_Click(object sender, RoutedEventArgs e) => RevealFile(file.FilePath);

        private void FileCopyPathMenuItem_Click(object sender, RoutedEventArgs e) => CopyFilePath(file.FilePath);

        private void FileCopyLinkMenuItem_Click(object sender, RoutedEventArgs e) => CopyFileLink(file.FilePath);
    }
}
