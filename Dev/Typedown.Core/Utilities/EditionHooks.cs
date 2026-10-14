using System;
using System.Collections.Generic;
using System.Linq;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Utilities
{
    /// <summary>
    /// Places where an edition (Edition.Initialize) adds to every window: styles for the editor page, entries in the File
    /// menu, its Export submenu and the View menu, the word counts the page reports, and the documents read from and written to disk. Typedown itself
    /// adds nothing here.
    /// </summary>
    public static class EditionHooks
    {
        private static string editorCss = "";

        /// <summary>
        /// CSS the editor page applies after the theme's and before the user's own (Settings > Editor), so a user's rule
        /// still has the last word. HTML and PDF exports carry it too, as they carry the other two.
        /// </summary>
        public static string EditorCss => editorCss;

        /// <summary><see cref="EditorCss"/> changed: every open editor page applies it.</summary>
        public static event Action EditorCssChanged;

        public static void SetEditorCss(string css)
        {
            css ??= "";
            if (css == editorCss) return;
            editorCss = css;
            EditorCssChanged?.Invoke();
        }

        private static readonly List<Func<AppViewModel, IEnumerable<MenuFlyoutItemBase>>> viewMenu = new();
        private static readonly List<Func<AppViewModel, IEnumerable<MenuFlyoutItemBase>>> fileMenu = new();
        private static readonly List<Func<AppViewModel, IEnumerable<MenuFlyoutItemBase>>> exportMenu = new();

        /// <summary>
        /// Entries for the View menu, below the theme submenu. <paramref name="build"/> makes them for one window when its
        /// menu bar is built, so each window has entries of its own.
        /// </summary>
        public static void AddViewMenuItems(Func<AppViewModel, IEnumerable<MenuFlyoutItemBase>> build)
        {
            lock (viewMenu) viewMenu.Add(build);
        }

        /// <summary>Entries for the File menu, below Save As; made for each window as the View menu's are.</summary>
        public static void AddFileMenuItems(Func<AppViewModel, IEnumerable<MenuFlyoutItemBase>> build)
        {
            lock (fileMenu) fileMenu.Add(build);
        }

        /// <summary>Entries for File > Export, after the export configurations; made for each window as the View menu's are.</summary>
        public static void AddExportMenuItems(Func<AppViewModel, IEnumerable<MenuFlyoutItemBase>> build)
        {
            lock (exportMenu) exportMenu.Add(build);
        }

        internal static IReadOnlyList<MenuFlyoutItemBase> ExportMenuItems(AppViewModel viewModel) => MenuItems(exportMenu, viewModel);

        internal static IReadOnlyList<MenuFlyoutItemBase> ViewMenuItems(AppViewModel viewModel) => MenuItems(viewMenu, viewModel);

        internal static IReadOnlyList<MenuFlyoutItemBase> FileMenuItems(AppViewModel viewModel) => MenuItems(fileMenu, viewModel);

        private static IReadOnlyList<MenuFlyoutItemBase> MenuItems(List<Func<AppViewModel, IEnumerable<MenuFlyoutItemBase>>> menu, AppViewModel viewModel)
        {
            List<Func<AppViewModel, IEnumerable<MenuFlyoutItemBase>>> builders;
            lock (menu) builders = menu.ToList();
            var items = new List<MenuFlyoutItemBase>();
            foreach (var build in builders)
            {
                try
                {
                    items.AddRange(build(viewModel) ?? Enumerable.Empty<MenuFlyoutItemBase>());
                }
                catch (Exception ex)
                {
                    Log.WriteLocal("EditionMenu", ex.ToString());
                }
            }
            return items;
        }

        /// <summary>
        /// The word count of a document as the editor page reported it: on loading it (a new <see cref="WordCountReport.LoadId"/>)
        /// and after its changes. Raised on the window's thread.
        /// </summary>
        public static event Action<WordCountReport> WordCountReported;

        /// <summary>
        /// A document's text as it is on disk: read when the file was opened, or written by a save. Raised on the window's
        /// thread, after the file was read or written.
        /// </summary>
        public static event Action<DocumentOnDiskReport> DocumentOnDisk;

        internal static void ReportDocumentOnDisk(string path, string text, bool written)
        {
            try
            {
                DocumentOnDisk?.Invoke(new DocumentOnDiskReport { Path = path, Text = text, Written = written });
            }
            catch (Exception ex)
            {
                Log.WriteLocal("EditionDocumentOnDisk", ex.ToString());
            }
        }

        internal static void ReportWordCount(WordCountReport report)
        {
            try
            {
                WordCountReported?.Invoke(report);
            }
            catch (Exception ex)
            {
                Log.WriteLocal("EditionWordCount", ex.ToString());
            }
        }
    }

    /// <summary>A document's text as read from its file or written to it (<see cref="EditionHooks.DocumentOnDisk"/>).</summary>
    public sealed class DocumentOnDiskReport
    {
        public string Path { get; set; }

        public string Text { get; set; }

        /// <summary>Written (a save, an automatic one too) rather than read (the file opened).</summary>
        public bool Written { get; set; }
    }

    /// <summary>A word count the editor page reported (<see cref="EditionHooks.WordCountReported"/>).</summary>
    public sealed class WordCountReport
    {
        /// <summary>The window's editor the report came from: counts of different editors are of different documents.</summary>
        public object Editor { get; set; }

        /// <summary>
        /// Which load of a document the count is of. Within one load a change in the count is an edit; a new load (another
        /// document, a tab switched to, the file reloaded) starts again from its first count.
        /// </summary>
        public int LoadId { get; set; }

        public int Words { get; set; }

        public int Characters { get; set; }

        /// <summary>
        /// The count is of text an automation client wrote (document.replace and the like, an edition's own edits through
        /// the same path), nobody having typed since: a change in it is not the reader's writing.
        /// </summary>
        public bool Automation { get; set; }
    }
}
