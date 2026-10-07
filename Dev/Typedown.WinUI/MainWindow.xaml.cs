using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;
using PropertyChanged;
using Typedown.WinUI.Enums;
using Typedown.WinUI.Interfaces;
using Typedown.WinUI.Models;
using Typedown.WinUI.Services;
using Typedown.WinUI.Services.Conversion;
using Typedown.WinUI.Utilities;
using Typedown.WinUI.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.ViewManagement;
using WinRT.Interop;

namespace Typedown.WinUI
{
    // [DoNotNotify]: MainWindow implements IMarkdownEditor, which extends INotifyPropertyChanged.
    // Fody's PropertyChanged weaver auto-weaves every type that implements that interface — including
    // this XAML-codegen partial class — which corrupts the WinUI3-generated activation IL and crashes
    // the native XAML engine on startup (0xc000027b) with no managed exception to catch. Excluding
    // MainWindow keeps Fody scoped to plain ViewModels like SettingsViewModel, where it belongs.
    [DoNotNotify]
    public sealed partial class MainWindow : Window, IMarkdownEditor
    {
        private readonly string logPath = Path.Combine(Path.GetTempPath(), "caret_winui_probe.log");

        private readonly RemoteInvoke remoteInvoke = new();
        private readonly EventCenter eventCenter = new();
        private readonly Transport transport;
        private readonly SettingsViewModel settings;
        private readonly RecentFilesService recentFiles = new();
        private readonly FavoritesService favoritesService = new();
        private readonly TrashService trashService = new();
        private readonly ObservableCollection<TocEntry> tocEntries = new();
        private readonly UISettings uiSettings = new();

        public bool IsEditorLoadFailed { get; private set; }
        public bool IsEditorLoaded { get; private set; }

#pragma warning disable CS0067 // required by INotifyPropertyChanged; unused until real ViewModels replace this scaffold
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067

        public void Dispose()
        {
            remoteInvoke.Dispose();
            eventCenter.Dispose();
        }

        // --- Multi-window support ---
        // Ported in spirit from the original's AppViewModel.GetInstances()/FileViewModel.
        // TryGetOpenedWindow. Any number of MainWindow instances can coexist in this one process, each
        // with its own FileViewModel/EditorView/WebView2 — Settings.json/RecentFiles.json/Backup are
        // shared files each window's own SettingsViewModel/AutoBackup instance reads and writes
        // independently, same as the original (last write wins on a race, which the original doesn't
        // guard against either). The single-instance-process layer (Typedown\App.cs's Mutex +
        // NamedPipeServerStream, redirecting a second `Typedown.exe` launch into a new window here
        // instead of starting a second process) is ported too, via Program.cs's AppInstance
        // redirection and OpenOrFocus below — that's the entry point it calls into.
        private static readonly List<MainWindow> openWindows = new();

        // Non-null only for a window opened via "Open in New Window" — see MainWindow_Loaded, which
        // branches on this instead of reading process command-line args (this isn't a new process, so
        // Environment.GetCommandLineArgs() would just repeat whatever launched the first window).
        private readonly string startupFilePath;

        // Brings an already-open window for this path to the foreground instead of loading the same
        // file into two places at once — ported from TryGetOpenedWindow's role in the original's
        // LoadFile. Returns false (do nothing special) for a path already open in THIS window, or not
        // open anywhere yet.
        private bool FocusIfOpenElsewhere(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;
            // A tab of this window: bring it to the front instead of opening the file twice.
            var here = FindDocument(filePath);
            if (here != null)
            {
                if (here != activeDoc || startPageShown) _ = ActivateDocument(here);
                return true;
            }
            var existing = openWindows.FirstOrDefault(w => w != this && w.FindDocument(filePath) != null);
            if (existing == null) return false;
            existing.BringToFront();
            _ = existing.ActivateDocument(existing.FindDocument(filePath));
            return true;
        }

        private void BringToFront()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            if (Win32Window.IsIconic(hwnd)) Win32Window.ShowWindow(hwnd, Win32Window.SW_RESTORE);
            Win32Window.SetForegroundWindow(hwnd);
        }

        // Entry point for a redirected activation (see Program.cs's OnActivated) — a second
        // `Caret.exe` launch that got handed off to this already-running process instead of starting
        // its own. Static because, unlike FocusIfOpenElsewhere, there's no "current window" the
        // redirect is happening in relation to; it's driven purely by whatever file path (if any) the
        // second launch's command line carried. Ported in spirit from the original's
        // Utilities.Common.OpenNewWindow (Typedown\App.cs's pipe handler called this): focus an
        // already-open window for that path if there is one, otherwise open a new window for it (or a
        // blank one if no markdown file was on the redirected command line at all). Must run on the UI
        // thread — callers marshal via DispatcherQueue first.
        public static void OpenOrFocus(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                var existing = openWindows.FirstOrDefault(w => w.FindDocument(filePath) != null);
                if (existing != null)
                {
                    existing.BringToFront();
                    _ = existing.ActivateDocument(existing.FindDocument(filePath));
                    return;
                }
                // With tabs, a file opened from Explorer joins the window last used, as a new tab.
                var target = lastActiveWindow ?? openWindows.LastOrDefault();
                if (target != null && target.TabsEnabled)
                {
                    target.BringToFront();
                    _ = target.OpenDocument(filePath, "Activation");
                    return;
                }
            }
            var newWindow = new MainWindow(filePath);
            newWindow.Activate();
        }

        /// <summary>
        /// Initializes a window without an explicit startup file path.
        /// </summary>
        public MainWindow() : this((string)null) { }

        /// <summary>
        /// Initializes the window, applies saved settings, and subscribes to editor state changes.
        /// </summary>
        /// <param name="startupFilePath">
        /// The file path to open in this window, or null to use the normal startup behavior.
        /// </param>
        public MainWindow(string startupFilePath) : this(startupFilePath, null) { }

        // "Move to new window" (MainWindow.Tabs.cs): the document arrives with its unsaved text and
        // its recovery-backup slot.
        private MainWindow(DocumentTransfer transfer) : this(transfer.Path, transfer) { }

        private readonly DocumentTransfer startupTransfer;

        private static bool appearanceApplied;

        private MainWindow(string startupFilePath, DocumentTransfer transfer)
        {
            this.startupFilePath = startupFilePath;
            startupTransfer = transfer;
            InitializeComponent();
            // The saved colour scheme, once per process, when the first window has loaded (see
            // ColorSchemes.Apply for why not earlier); Refresh makes the window read it.
            if (!appearanceApplied)
            {
                appearanceApplied = true;
                void ApplySaved(object s, RoutedEventArgs e)
                {
                    ((FrameworkElement)Content).Loaded -= ApplySaved;
                    var saved = new SettingsViewModel();
                    ColorSchemes.Apply(saved.ColorScheme, saved.AccentSource, Config.IsMicaSupported ? saved.WindowMaterial : "solid");
                    ColorSchemes.Refresh((FrameworkElement)Content);
                    // The editor was given Copper's page colour as the window was built, and the section
                    // colours were checked against Copper's text: both again with the scheme (this also
                    // updates the editor's background and theme).
                    ApplySectionColors();
                }
                ((FrameworkElement)Content).Loaded += ApplySaved;
            }
            openWindows.Add(this);
            // Ctrl+, opens Settings (as in Windows Terminal and VS Code). VirtualKey has no name for the
            // comma key, so it's added here by its code (188, VK_OEM_COMMA) rather than in XAML.
            var settingsKey = new KeyboardAccelerator { Key = (VirtualKey)188, Modifiers = VirtualKeyModifiers.Control };
            settingsKey.Invoked += (s, e) => { e.Handled = true; SettingsMenuItem_Click(this, null); };
            ((UIElement)Content).KeyboardAccelerators.Add(settingsKey);
            // Ctrl+/ steps through View, Code and Split (the status bar switch). 191 is VK_OEM_2.
            var viewModeKey = new KeyboardAccelerator { Key = (VirtualKey)191, Modifiers = VirtualKeyModifiers.Control };
            viewModeKey.Invoked += (s, e) => { e.Handled = true; CycleViewMode(); };
            ((UIElement)Content).KeyboardAccelerators.Add(viewModeKey);
            // The sidebar's decorative card gives its space to the lists in a short window.
            ((FrameworkElement)Content).SizeChanged += (s, e) =>
            {
                ApplyDecorativeCardVisibility();
                if (SettingsPageShown) SizeSettingsContent();
            };
            Closed += (s, e) =>
            {
                openWindows.Remove(this);
                // WinUI 3 desktop apps don't exit on last-window-closed the way WPF's default
                // ShutdownMode does — without this, closing every window leaves the process running
                // with nothing visible.
                if (openWindows.Count == 0) Application.Current.Exit();
            };
            transport = new Transport(remoteInvoke, eventCenter);
            settings = new SettingsViewModel(this);
            SetUpDocuments(transfer?.UntitledKey);
            RegisterHandlers();
            SetUpTitleBar();
            SetUpWindowPlacement();
            SetUpClosingPrompt();
            SetUpAutoSaveTimer();
            // Both apply a persisted appearance setting that, until now, only ever took effect once
            // the user opened Settings and touched the corresponding control — a real startup gap for
            // anyone who'd already set a non-default theme or turned Mica off, not something specific
            // to this Mica pass. Fixing them together since they're the same shape of bug.
            ApplyNativeTheme();
            ApplyBackdrop();
            ApplyEditorBackground();
            ApplyTopmost();
            ApplyStatusBarVisibility();
            UpdateViewModeUi();
            ApplyLayout();
            SetUpLayoutKeys();
            // A window that opens in Distraction-free opens full screen, once it's shown.
            ((FrameworkElement)Content).Loaded += (s, e) => SyncPresenter();
            SetUpThemePush();
            UpdateTitle();
            RefreshRecentFilesMenu();
            TocListView.ItemsSource = tocEntries;
            eventCenter.GetObservable<EditorEventArgs>("StateChange").Subscribe(x => { historyUpdating = false; file.EndEcho(); UpdateToc(x.Args); UpdateWordCount(x.Args); });
            SetUpHistory();
            SetUpEditorPopups();
            SetUpConvertPage();
            EditorView.Loaded += MainWindow_Loaded;
        }

        // Reimplemented against WinUI 3's own custom-title-bar API (ExtendsContentIntoTitleBar +
        // SetTitleBar) rather than ported from Caption.xaml, which drew its own caption buttons and
        // drag region via the private Typedown.XamlUI NuGet package (no source available) plus a full
        // AppViewModel for back-navigation we haven't built. WinUI 3's native support does the same
        // job — draggable custom region, OS-drawn caption buttons — with far less code.
        private void SetUpTitleBar()
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            // Frees the reserved top-left "system icon + menu" hit-test slot so it doesn't compete with
            // our own custom title bar content there — doesn't affect painting (see the Image's
            // VerticalAlignment note below for what actually caused the invisible-icon bug).
            AppWindow.TitleBar.IconShowOptions = Microsoft.UI.Windowing.IconShowOptions.HideIconAndSystemMenu;
            AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "logo.ico"));
            // TitleBarIconImage.Source is set from code, not a XAML relative "Assets/..." Source — this
            // unpackaged build has no ms-appx package identity for XAML's relative-URI resolver to use,
            // so it silently rendered nothing. An absolute file:// URI to the copied-output Assets
            // folder always resolves regardless of packaged/unpackaged.
            TitleBarIconImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(
                new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Square44x44Logo.scale-100.png")));
        }

        // --- Window placement ---
        // Reimplemented against WinUI 3's own AppWindow/OverlappedPresenter APIs rather than a literal
        // port of the original's TrySaveWindowPlacement/ShowWindowWithSavedPlacement (Typedown\Utilities
        // \Common.cs), which used the raw Win32 WINDOWPLACEMENT struct via PInvoke.GetWindowPlacement/
        // SetWindowPlacement on the WPF host window's HWND. AppWindow.MoveAndResize + Presenter.State is
        // the modern WinUI 3-native equivalent of the same "position + size + maximized" triple, and
        // this app already uses AppWindow elsewhere (Closing, above) — no raw struct interop needed.
        //
        // AppWindow.Changed fires continuously during a drag-move/drag-resize (every intermediate frame,
        // not just the final one), so writes are debounced the same way the original throttled its own
        // LocationChanged/SizeChanged handlers with a 100ms delay — otherwise every pixel of a drag would
        // rewrite Settings.json.
        private bool placementSaveScheduled;

        private void SetUpWindowPlacement()
        {
            if (settings.WindowX is int x && settings.WindowY is int y &&
                settings.WindowWidth is int w && settings.WindowHeight is int h)
            {
                AppWindow.MoveAndResize(new RectInt32(x, y, w, h));
            }
            if (settings.WindowMaximized && AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Maximize();
            }
            AppWindow.Changed += async (s, args) =>
            {
                if (allowClose || placementSaveScheduled) return;
                placementSaveScheduled = true;
                await Task.Delay(300);
                placementSaveScheduled = false;
                if (!allowClose) SavePlacementNow();
            };
        }

        private void SavePlacementNow()
        {
            if (AppWindow?.Presenter is not OverlappedPresenter presenter) return;
            if (presenter.State == OverlappedPresenterState.Maximized)
            {
                settings.WindowMaximized = true;
            }
            else if (presenter.State == OverlappedPresenterState.Restored)
            {
                settings.WindowMaximized = false;
                settings.WindowX = AppWindow.Position.X;
                settings.WindowY = AppWindow.Position.Y;
                settings.WindowWidth = AppWindow.Size.Width;
                settings.WindowHeight = AppWindow.Size.Height;
            }
            // Minimized: leave whatever was last recorded (maximized or restored bounds) alone —
            // there's nothing meaningful to capture about a minimized window's "shape".
        }

        private void UpdateTitle()
        {
            var dirtyMark = file.IsDirty ? "● " : ""; // ● — matches the original's DisplaySaved-driven title dot
            TitleTextBlock.Text = startPageShown ? "Caret" : dirtyMark + activeDoc.DisplayName + " - Caret";
            Title = TitleTextBlock.Text;
            UpdateFavoriteButton();
            UpdateEncodingStatus();
        }

        // Status bar: "Markdown", or — for a file that wasn't UTF-8 and was read in Windows' legacy
        // code page (Utilities/TextFileEncoding.cs) — which encoding, and that saving converts it.
        private void UpdateEncodingStatus()
        {
            var legacy = file.LegacyEncodingName;
            StatusBarFormatText.Text = legacy == null ? Locale.GetString("Markdown") : Locale.Format("StatusLegacyEncoding", legacy);
            ToolTipService.SetToolTip(StatusBarFormatText, legacy == null ? null : Locale.Format("StatusLegacyEncodingTip", legacy));
        }

        // File > Favorite, checked for a favorite; the tabs show a star for theirs (UpdateTabHeader).
        private void UpdateFavoriteButton()
        {
            FavoriteMenuItem.IsEnabled = !startPageShown && !string.IsNullOrEmpty(file.FilePath);
            FavoriteMenuItem.IsChecked = !startPageShown && favoritesService.Contains(file.FilePath);
            foreach (var doc in documents)
                if (doc.FavoriteStar != null) doc.FavoriteStar.Visibility = favoritesService.Contains(doc.Path) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void FavoriteMenuItem_Click(object sender, RoutedEventArgs e) => ToggleFavorite(file.FilePath);

        private void ToggleFavorite(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var isFavorite = favoritesService.Toggle(path);
            UpdateFavoriteButton();
            if (FavoritesPanel.Visibility == Visibility.Visible) RefreshFavoritesNavList();
            Log($"Favorite: {(isFavorite ? "added" : "removed")} {path}");
        }

        // --- Unsaved-changes prompt ---
        // The original's AskToSave (FileViewModel.cs) gated every New/Open/window-close behind this;
        // this scaffold didn't have it until now, which meant New and Open silently discarded unsaved
        // work. IsDirty is tracked in FileViewModel by comparing the live text against the
        // last-loaded-or-saved snapshot, same idea as the original's FileHash/CurrentHash comparison.
        //
        // allowClose exists because AppWindow.Closing can't simply be "awaited then let close happen" —
        // it's synchronous-looking but we need to show an async dialog first, so we always cancel the
        // first close attempt, run the check, and re-invoke Close() ourselves once it's confirmed.
        private bool allowClose;

        private void SetUpClosingPrompt()
        {
            AppWindow.Closing += (s, args) =>
            {
                if (allowClose) return;
                args.Cancel = true;
                RequestClose();
            };
        }

        // Every way of closing the window ends here. AppWindow.Closing only fires for the title bar's
        // X and Alt+F4 — Window.Close() from Ctrl+Shift+W or the menu doesn't raise it, so calling
        // Close() directly used to close the window without asking about unsaved changes.
        private bool closeRequested;

        private async void RequestClose()
        {
            if (closeRequested) return;
            closeRequested = true;
            try
            {
                // An autosave or backup already under way finishes first: once "Don't Save" has
                // deleted the backups, nothing may write them back.
                await Task.WhenAny(autoSaveTick);
                if (!await ConfirmCloseAllDocuments()) return;
                foreach (var doc in documents) doc.File.CompleteDiscard(); // "Don't Save": backups go too
                SaveSession();
                allowClose = true;
                SavePlacementNow(); // final capture — don't wait for the debounced save below
                Close();
            }
            finally
            {
                closeRequested = false;
            }
        }

        // Returns true if it's OK to proceed (no unsaved changes, or the user chose Save/Don't Save);
        // false if the user cancelled, in which case the caller should not continue.
        private async System.Threading.Tasks.Task<bool> ConfirmDiscardChangesIfNeeded()
        {
            if (!file.IsDirty) return true;
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = Locale.GetString("UnsavedChanges"),
                Content = file.WouldBlankSavedFile
                    ? Locale.Format("SaveWouldEraseFile", activeDoc.DisplayName)
                    : Locale.Format("SaveChangesPrompt", activeDoc.DisplayName),
                PrimaryButtonText = Locale.GetString("SaveButton"),
                SecondaryButtonText = Locale.GetString("DontSave"),
                CloseButtonText = Locale.GetString("Cancel"),
                DefaultButton = file.WouldBlankSavedFile ? ContentDialogButton.Secondary : ContentDialogButton.Primary,
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if (!await file.Save())
                    await SaveAsInternal();
                return !file.IsDirty; // still dirty means the Save As picker was cancelled — don't proceed
            }
            if (result != ContentDialogResult.Secondary) return false; // Cancel (or dismissed) = stop
            // Don't Save: the text the user chose to throw away must not come back as a "recovered"
            // backup later. It isn't deleted yet: a file picker may still follow, the backup timer keeps
            // running meanwhile, and if the picker is cancelled the still-open text keeps its protection.
            // The backup goes once the document is actually replaced or the window closes.
            file.DiscardOnSwitch();
            return true;
        }

        // --- Auto-save / AutoBackup ---
        // Ported the shape of FileViewModel's saveFileTimer (5-second DispatcherTimer tick in the
        // original): when Auto save is on and the document already has a real path, silently re-save
        // it. Otherwise — untitled documents (no path to autosave to yet) or Auto save turned off —
        // fall back to FileViewModel.BackupTick(), which writes the dirty content to a hash-named
        // recovery file instead. This closes the gap noted here previously: an unsaved untitled
        // document now has a safety net even though there's nowhere to Ctrl+S it to.
        private void SetUpAutoSaveTimer()
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            var blankGuardLogged = new HashSet<FileViewModel>();
            // A closed window's documents must not be saved or backed up again (after "Don't Save",
            // that would write the discarded text back).
            Closed += (s, e) => timer.Stop();
            timer.Tick += async (s, e) =>
            {
                // No tick while the window is closing (RequestClose waits for one already running),
                // and never two at once.
                if (closeRequested || !autoSaveTick.IsCompleted) return;
                autoSaveTick = AutoSaveTick(blankGuardLogged);
                await autoSaveTick;
            };
            timer.Start();
        }

        private Task autoSaveTick = Task.CompletedTask;

        private async Task AutoSaveTick(HashSet<FileViewModel> blankGuardLogged)
        {
            // Every open document, not only the one on screen: a tab in the background keeps its
            // unsaved text too. Tabs restored but never opened have nothing to save yet.
            // (A document changing windows is left alone: it is in two places for a moment, and the arriving
            // copy is still blank until its text is in, which would delete the recovery backup they share.)
            foreach (var doc in documents.Where(d => d.PendingPath == null && !d.InTransfer).ToList())
            {
                var f = doc.File;
                var blankGuard = f.WouldBlankSavedFile;
                if (blankGuard && blankGuardLogged.Add(f)) Log($"AutoSave: skipped blanking '{f.FilePath}'");
                if (!blankGuard) blankGuardLogged.Remove(f);
                if (settings.AutoSave && f.IsDirty && !string.IsNullOrEmpty(f.FilePath) && !blankGuard)
                {
                    await f.Save();
                    Log($"AutoSave: {f.FilePath}");
                }
                else if (await f.BackupTick())
                {
                    Log($"AutoBackup: wrote recovery backup for '{f.BackupKey}'");
                }
            }
        }

        // --- AutoBackup recovery prompt ---
        // Ported from the original's FileViewModel.CheckBackup(), split so the dialog (which needs
        // XamlRoot) lives here instead of in the dialog-free FileViewModel. Call after any load — New,
        // Open, startup — so a leftover backup from a previous crash gets offered back instead of
        // silently sitting unused. If the backup matches what was just loaded there's nothing to
        // recover, so it's left alone (same as the original, which also doesn't clean up a
        // stale-but-matching backup here).
        private async Task OfferBackupRecoveryIfAny(string path)
        {
            var backup = await file.PeekBackup(path);
            if (backup == null || backup == file.Markdown) return;
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = Locale.GetString("RecoverUnsavedChanges"),
                Content = string.IsNullOrEmpty(path) ? Locale.GetString("RecoverUntitledPrompt") : Locale.Format("RecoverFilePrompt", Path.GetFileName(path)),
                PrimaryButtonText = Locale.GetString("Recover"),
                SecondaryButtonText = Locale.GetString("Discard"),
                DefaultButton = ContentDialogButton.Primary,
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                file.ApplyRecoveredBackup(backup);
                UpdateTitle();
                Log($"AutoBackup: recovered backup for '{path}'");
            }
            else
            {
                file.DiscardBackup(path);
                Log($"AutoBackup: discarded backup for '{path}'");
            }
        }

        // GetSettings/GetStringResources/Markdown/BasePath are real now (backed by SettingsViewModel,
        // Locale, and FileViewModel). GetCurrentTheme now returns the real {theme, accentColor,
        // background} shape (see BuildThemePayload) instead of a bare theme-name string — the bare
        // string was a real gap, not a simplification: Typedown.Editor's theme.ts (bundled JS, used
        // as-is) destructures accentColor/background out of whatever GetCurrentTheme resolves to, so
        // a string here meant those two silently came out undefined.
        private void RegisterHandlers()
        {
            remoteInvoke.Handle("GetCurrentTheme", () => BuildThemePayload());
            remoteInvoke.Handle("GetStringResources", (JToken args) =>
            {
                var names = args["names"]?.ToObject<string[]>() ?? Array.Empty<string>();
                var result = new JObject();
                foreach (var name in names) result[name] = Locale.GetString(name);
                return result;
            });
            remoteInvoke.Handle("GetSettings", () => new
            {
                settings.FocusMode,
                settings.Typewriter,
                settings.SourceCode,
                settings.SplitPreview,
                settings.SpeechMode,
                settings.FontSize,
                settings.LineHeight,
                settings.AutoPairBracket,
                settings.AutoPairQuote,
                settings.TrimUnnecessaryCodeBlockEmptyLines,
                settings.PreferLooseListItem,
                settings.AutoPairMarkdownSyntax,
                EditorAreaWidth = EffectivePageWidth,
                settings.TabSize,
                file.Markdown,
                BasePath = file.ImageBasePath,
            });
            // Traced from FileViewModel.cs's Export()/ExportCallback() and Editor/index.tsx's Export
            // listener: we push {type, context, basePath, title} via PostMessage("Export", ...), the
            // editor's own ExportHtml (JS) renders it to a clean HTML string, and calls this back with
            // {html, context}. context is opaque to the editor — just handed back verbatim — so a
            // single in-flight TaskCompletionSource is enough since only one export runs at a time.
            remoteInvoke.Handle("ExportCallback", (JToken args) =>
            {
                pendingExportHtml?.TrySetResult(args["html"]?.ToString());
                return true;
            });
            // Not used by anything we send (Print uses WebView2's own ShowPrintUI instead of routing
            // through the editor's HTML export), but registered for API completeness — an unexpected
            // call would otherwise throw "function does not exist" back at the editor.
            remoteInvoke.Handle("PrintHTML", (JToken args) => true);
            remoteInvoke.Handle("SetClipboard", (JToken args) => SetClipboardFromEditor(args));
            // The rest of the editor's host calls (services/remote/common.ts), audited against what's
            // registered here: before this, Ctrl+clicking a link and the table toolbar's resize button
            // both called a function the host didn't have, so they silently did nothing.
            // ContentLoaded only drove the original's fade-in; acknowledged so the editor's call resolves.
            // LoadImage/UnhandledException are declared in the editor but never called.
            remoteInvoke.Handle("ContentLoaded", () => true);
            remoteInvoke.Handle<string>("OpenNewWindow", OpenLink);
            remoteInvoke.Handle<JToken, object>("ResizeTable", args =>
                ShowTableSizeDialog(Locale.GetString("ResizeTable"), args?["rows"]?.ToObject<int>() ?? 3, args?["columns"]?.ToObject<int>() ?? 3));
        }

        // Ctrl+click on a link in the editor (plain clicks just place the cursor). Ported from the
        // original's MarkdownEditor.OpenNewWindow, with one deliberate change: the original handed any
        // local non-markdown file to the shell, so a document linking to e.g. setup.exe would run it.
        // Here only http/https/mailto go to the browser, linked markdown notes open in Caret (focusing
        // an existing window if one has that file), and other local files open only if their type is
        // on an explicit allowlist of documents/media; anything else is just shown selected in File
        // Explorer. Launcher alone is not enough: for an unpackaged desktop app it happily ran a linked
        // .bat file in testing. Other schemes (javascript:, ms-settings:, ...) are ignored.
        private static readonly HashSet<string> LinkOpenableExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".jfif", ".tif", ".tiff",
            ".pdf", ".txt", ".csv", ".json", ".xml",
            ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".odp", ".rtf",
            ".mp3", ".wav", ".m4a", ".flac", ".ogg", ".mp4", ".mov", ".webm", ".mkv", ".avi",
        };

        private async void OpenLink(string href)
        {
            if (string.IsNullOrWhiteSpace(href)) return;
            try
            {
                var isAbsolute = Uri.TryCreate(href, UriKind.Absolute, out var uri);
                if (isAbsolute && uri.Scheme is "http" or "https" or "mailto")
                {
                    await Launcher.LaunchUriAsync(uri);
                    Log($"OpenLink: browser {uri}");
                    return;
                }
                string path = null;
                if (isAbsolute && uri.IsFile)
                    path = uri.LocalPath;
                else if (!isAbsolute && !string.IsNullOrEmpty(file.FilePath))
                    path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file.FilePath), Uri.UnescapeDataString(href.Split('#')[0])));
                if (path == null || !File.Exists(path))
                {
                    Log($"OpenLink: ignored '{href}'");
                    return;
                }
                if (FileTypeHelper.IsMarkdownFile(path))
                {
                    OpenOrFocus(path);
                    Log($"OpenLink: note {path}");
                }
                else if (LinkOpenableExtensions.Contains(Path.GetExtension(path)))
                {
                    await Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(path));
                    Log($"OpenLink: file {path}");
                }
                else
                {
                    // explorer.exe /select only opens the folder; a Windows path can't contain '"'.
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                    Log($"OpenLink: not an openable type, revealed in Explorer: {path}");
                }
            }
            catch (Exception ex)
            {
                Log($"OpenLink EXCEPTION for '{href}': {ex.Message}");
            }
        }

        // Shared by Insert Table and the editor's own table-resize button (ResizeTable). Returns null on
        // Cancel, which the editor treats as "no change" (same as the original's InsertTableDialog).
        private async Task<object> ShowTableSizeDialog(string title, int rows, int columns)
        {
            var rowsBox = new NumberBox { Header = Locale.GetString("Rows"), Value = rows, Minimum = 1, Maximum = 200, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
            var columnsBox = new NumberBox { Header = Locale.GetString("Columns"), Value = columns, Minimum = 1, Maximum = 30, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = title,
                Content = new StackPanel { Spacing = 12, Children = { rowsBox, columnsBox } },
                PrimaryButtonText = Locale.GetString("OK"),
                CloseButtonText = Locale.GetString("Cancel"),
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;
            return new
            {
                rows = double.IsNaN(rowsBox.Value) ? rows : (int)rowsBox.Value,
                columns = double.IsNaN(columnsBox.Value) ? columns : (int)columnsBox.Value,
            };
        }

        private async void InsertTableMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var size = await ShowTableSizeDialog(Locale.GetString("InsertTableTitle"), 3, 3);
            if (size != null) PostMessage("InsertTable", size);
        }

        // The editor's own Cut/Copy (copyCutCtrl.js) writes the clipboard by calling back into the host
        // — ported from the original's EditorViewModel.OnSetClipboard. It always sends a pair: text/html
        // first, then text/plain (the selection as markdown), so the html is held until the plain text
        // arrives and both go into one DataPackage. Flush keeps the content on the clipboard after
        // Caret closes; without it WinRT drops an app's clipboard data when the app exits.
        private string pendingClipboardHtml;

        private bool SetClipboardFromEditor(JToken args)
        {
            var type = args["type"]?.ToString();
            var data = args["data"]?.ToString() ?? "";
            if (type == "text/html")
            {
                pendingClipboardHtml = data;
                return true;
            }
            if (type != "text/plain") return false;
            var package = new DataPackage();
            package.SetText(data);
            if (!string.IsNullOrEmpty(pendingClipboardHtml))
                package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(pendingClipboardHtml));
            pendingClipboardHtml = null;
            Clipboard.SetContent(package);
            Clipboard.Flush();
            return true;
        }

        private void Log(string message) => File.AppendAllText(logPath, $"{DateTime.Now:O} {message}\n");

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // A window opened via "Open in New Window" already knows what to load and isn't a
                // separate process — LoadStartUpMarkdown reads Environment.GetCommandLineArgs(), which
                // would just repeat whatever launched the very first window in this process.
                if (!string.IsNullOrEmpty(startupFilePath))
                    await file.OpenFile(startupFilePath);
                else if (startupTransfer == null)
                    await file.LoadStartUpMarkdown();
                if (startupTransfer?.UnsavedText != null)
                    file.ApplyRecoveredBackup(startupTransfer.UnsavedText);
                if (startupTransfer == null && openWindows.Count <= 1)
                    await RestoreSession();
                // New since the fork: FileStartupAction/FolderStartupAction existed as dormant ported
                // settings with nothing reading them — the original's own OnLoad/OnStartup (traced in
                // Typedown.Core\ViewModels\FileViewModel.cs) read AccessHistory (EF Core, not ported)
                // for "the last file"/"the last folder"; RecentFilesService and the new
                // settings.LastOpenedFolder above serve the same purpose here. Guarded to only the
                // process's own first window (openWindows.Count is 1 — this window already added
                // itself, see the constructor) so a manually opened "New Window" stays genuinely blank
                // rather than silently reloading whatever the first window already has open.
                if (string.IsNullOrEmpty(file.FilePath) && startupTransfer == null && settings.FileStartupAction == FileStartupAction.OpenLast && openWindows.Count <= 1)
                {
                    var lastFile = recentFiles.Files.FirstOrDefault(File.Exists);
                    if (lastFile != null) await file.OpenFile(lastFile);
                }
                if (rootExplorerItem == null && openWindows.Count <= 1)
                {
                    switch (settings.FolderStartupAction)
                    {
                        case FolderStartupAction.OpenLast:
                            if (!string.IsNullOrEmpty(settings.LastOpenedFolder) && Directory.Exists(settings.LastOpenedFolder))
                                OpenFolderTree(settings.LastOpenedFolder);
                            break;
                        case FolderStartupAction.OpenFolder:
                            if (Directory.Exists(settings.StartupOpenFolder))
                                OpenFolderTree(settings.StartupOpenFolder);
                            break;
                    }
                }
                if (!string.IsNullOrEmpty(file.FilePath))
                {
                    recentFiles.Record(file.FilePath);
                    RefreshRecentFilesMenu();
                }
                if (!string.IsNullOrEmpty(file.FilePath) && startupTransfer == null)
                    await OfferBackupRecoveryIfAny(file.FilePath);
                if (openWindows.Count <= 1 && startupTransfer == null)
                    await RecoverUntitledBackups();
                UpdateTitle();
                Log($"LoadStartUpMarkdown: FilePath={file.FilePath}, chars={file.Markdown.Length}");
                // Started by File Explorer's "Convert to Markdown": the files go straight to the Convert page.
                if (Program.StartupConvertPaths is { Count: > 0 } convertPaths && startupTransfer == null)
                {
                    Program.StartupConvertPaths = null;
                    convertPageHeldAtStartup = true;
                    ShowConvertPage("Convert");
                    _ = ConvertPathsAsync(convertPaths);
                }
                // Config.WebView2Args (ported back in #2) is still applied via this documented
                // environment-variable path — verified by inspecting the spawned msedgewebview2.exe
                // command line, --disable-web-security and --allow-file-access-from-files really do
                // reach the browser process. It's harmless (this WebView2 only ever shows our own
                // bundled editor, never arbitrary web content) but it turned out NOT to be what fixes
                // local image rendering below: Chromium's file:// subresource block for non-file
                // origins isn't a web-security-policy check --disable-web-security lifts, it's a lower
                // level "not allowed to load local resource" restriction that these flags don't touch.
                // Kept for parity with the original's args list and because some of the other flags
                // (msOverlayScrollbarWinStyle) are still meaningful.
                //
                // CoreWebView2Environment.CreateAsync's overloads didn't match what either the base
                // Microsoft.Web.WebView2.Core.dll or its WinUI3-specific .Projection.dll counterpart
                // actually expose here (tried 1-arg and 3-arg forms, both rejected by the compiler) —
                // rather than keep guessing at an API surface that clearly differs from the plain .NET
                // docs in this WinUI3+projection combination, the well-documented environment-variable
                // configuration path sidesteps the ambiguity entirely and needs no API call at all.
                Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", string.Join(" ", Config.WebView2Args));
                await EditorView.EnsureCoreWebView2Async();
                Log("CoreWebView2 initialized OK");
                var staticsPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Statics");
                EditorView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "typedown.editor.local", staticsPath, CoreWebView2HostResourceAccessKind.Allow);
                // WebView2 has its own built-in accelerator keys (Ctrl+F opens its native find-on-page
                // bar, Ctrl+P prints, F5 refreshes, F12 opens DevTools...) that would otherwise compete
                // with our shortcuts and the app's own menu actions. Turning this off makes WebView2
                // behave like a plain content host instead of a mini-browser.
                EditorView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                // The actual fix for local images (see the WebView2Args comment above for the dead
                // end): Muya's renderer.js/getImageInfo.js build a plain file:/// URI for any local
                // image path and hand it to the DOM as-is (traced in Typedown.Editor's Muya lib) — we
                // can't change what URI scheme the editor emits without forking that JS. Rather than
                // fight Chromium's local-resource block, we intercept every file:/// request ourselves
                // and serve the bytes directly through WebView2's response pipeline, so the request
                // never reaches Chromium's own file loader (and its origin restriction) at all. Scoped
                // to CoreWebView2WebResourceContext.Image since that's the only local-file scheme this
                // app needs to serve — anything else falls through to the (still blocked) default.
                EditorView.CoreWebView2.AddWebResourceRequestedFilter("file:///*", CoreWebView2WebResourceContext.Image);
                EditorView.CoreWebView2.WebResourceRequested += EditorView_WebResourceRequested;
                EditorView.CoreWebView2.WebMessageReceived += (s, args) =>
                {
                    var raw = args.TryGetWebMessageAsString();
                    Log($"WebMessage: {raw}");
                    transport.EmitWebViewMessage(this, raw);
                };
                EditorView.CoreWebView2.NavigationCompleted += (s, args) =>
                    Log($"NavigationCompleted: IsSuccess={args.IsSuccess}, WebErrorStatus={args.WebErrorStatus}");
                await EditorView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(HostShortcutScript);
                await EditorView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BuildSpellcheckScript(settings.SpellcheckEnabled));
                await EditorView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(ReviewScript);
                eventCenter.GetObservable<EditorEventArgs>("HostShortcut").Subscribe(x => HandleHostShortcut(x.Args));
                eventCenter.GetObservable<EditorEventArgs>("ContextMenu").Subscribe(x => ShowEditorContextMenu(x.Args));
                eventCenter.GetObservable<EditorEventArgs>("SpellCheck").Subscribe(x => SpellCheckRequest(x.Args));
                EditorView.Source = new Uri("https://typedown.editor.local/index.html");
                IsEditorLoaded = true;
                _ = CheckForUpdatesOnStartupAsync();
            }
            catch (Exception ex)
            {
                IsEditorLoadFailed = true;
                Log($"EXCEPTION: {ex}");
            }
        }

        private static readonly Dictionary<string, string> ImageMimeTypes = new()
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".jfif"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".svg"] = "image/svg+xml",
            [".webp"] = "image/webp",
        };

        // Serves file:/// requests for <img> tags ourselves instead of letting Chromium's own file
        // loader handle them — see the registration comment in MainWindow_Loaded for why. Synchronous
        // and fast (local disk read), so no CoreWebView2Deferral is needed; per the WebView2 docs a
        // deferral is only required when the response is produced asynchronously.
        private void EditorView_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
        {
            try
            {
                var localPath = new Uri(args.Request.Uri).LocalPath;
                if (!File.Exists(localPath))
                {
                    Log($"WebResourceRequested: not found, {localPath}");
                    return;
                }
                var ext = Path.GetExtension(localPath).ToLowerInvariant();
                var contentType = ImageMimeTypes.TryGetValue(ext, out var mime) ? mime : "application/octet-stream";
                var stream = File.OpenRead(localPath);
                args.Response = EditorView.CoreWebView2.Environment.CreateWebResourceResponse(
                    stream.AsRandomAccessStream(), 200, "OK", $"Content-Type: {contentType}");
            }
            catch (Exception ex)
            {
                Log($"WebResourceRequested EXCEPTION: {ex}");
            }
        }

        // Wire contract per Typedown.Editor/src/services/transport.ts: the web side does
        // `JSON.parse(event.data)` itself, so the host must post a STRING (PostWebMessageAsString),
        // not PostWebMessageAsJson (which would hand the web side an already-parsed object and break
        // its JSON.parse call). The envelope is flat: { name, args }.
        public bool PostMessage(string name, object arg)
        {
            try
            {
                var payload = Newtonsoft.Json.JsonConvert.SerializeObject(
                    new { name, args = arg }, Config.EditorJsonSerializerSettings);
                Log($"PostMessage: {payload}");
                EditorView.CoreWebView2.PostWebMessageAsString(payload);
                return true;
            }
            catch (Exception ex)
            {
                Log($"PostMessage EXCEPTION: {ex}");
                return false;
            }
        }

        // --- Keyboard shortcuts while focus is inside the editor ---
        // XAML KeyboardAccelerator (see the MenuFlyoutItems in MainWindow.xaml) only fires when
        // keyboard focus is somewhere in the native XAML tree. WebView2 hosts an actual separate child
        // HWND for its Chromium content — when that HWND has focus (i.e. whenever you're actually
        // typing in the document, the overwhelmingly common case), the OS delivers key presses
        // straight to it, and XAML-declared accelerators never fire. This is a documented WebView2
        // limitation across every XAML host (WPF/UWP/WinUI3 alike), not specific to our setup, and
        // it's exactly what made Ctrl+S silently do nothing.
        //
        // The natural fix — CoreWebView2Controller.AcceleratorKeyPressed — turned out to be a dead
        // end: WinUI 3's XAML WebView2 control deliberately does not expose CoreWebView2Controller
        // ("WinUI takes care of the environment and window creation behind the scenes," per
        // Microsoft's own guidance). So instead we catch the keys in the web content itself, in the
        // capture phase (before the page's own handlers can see or stop them), and post them back to
        // the host over the same message channel Transport already listens on — no different from how
        // GetCurrentTheme/GetSettings/etc. work. AreBrowserAcceleratorKeysEnabled=false (set right
        // after EnsureCoreWebView2Async, above) stops WebView2's own built-in shortcuts (its native
        // Ctrl+F find-on-page bar in particular) from grabbing the key first.
        private const string HostShortcutScript = @"
            // Alt pressed and released on its own brings the menu forward, as in any Windows app.
            var altAlone = false;
            window.addEventListener('keyup', function (e) {
                if (e.key === 'Alt' && altAlone)
                    window.chrome.webview.postMessage(JSON.stringify({ type: 'message', name: 'HostShortcut', args: { key: 'alt', shift: false } }));
                altAlone = false;
            }, true);
            // The page has no context menu of its own (App.tsx suppresses the browser's): the host shows
            // one where the click was. Reported a tick later, once the editor has moved its caret to the
            // click and the selection is what the user sees.
            window.addEventListener('contextmenu', function (e) {
                e.preventDefault();
                var x = e.clientX, y = e.clientY, target = e.target;
                setTimeout(function () {
                    var spell = window.__caretSpell && window.__caretSpell.atPoint ? window.__caretSpell.atPoint(x, y) : null;
                    var box = target && target.closest ? target.closest('.CodeMirror') : null;
                    var cm = box && box.CodeMirror;
                    var selection = window.getSelection();
                    var has = cm ? cm.somethingSelected() : !!selection && !selection.isCollapsed;
                    var review = !cm && window.__caretReview && window.__caretReview.chainAt ? window.__caretReview.chainAt(x, y) : '';
                    window.chrome.webview.postMessage(JSON.stringify({ type: 'message', name: 'ContextMenu', args: { x: x, y: y, hasSelection: has, code: !!cm, spell: spell || '', review: review || '' } }));
                }, 0);
            }, true);
            window.addEventListener('keydown', function (e) {
                altAlone = e.key === 'Alt';
                // F6 (areas of the window), F10 (the menu) and F11 (distraction-free) belong to the window.
                if (!e.ctrlKey && !e.altKey && (e.key === 'F6' || e.key === 'F10' || e.key === 'F11')) {
                    e.preventDefault();
                    e.stopPropagation();
                    // Holding F11 would switch full screen on and off; holding F6 keeps moving, as intended.
                    if (e.repeat && e.key === 'F11') return;
                    window.chrome.webview.postMessage(JSON.stringify({ type: 'message', name: 'HostShortcut', args: { key: e.key.toLowerCase(), shift: e.shiftKey } }));
                    return;
                }
                // Ctrl+Alt is AltGr on many layouts (Hungarian AltGr+B/V/X/F type { @ # [) — never ours.
                if (!e.ctrlKey || e.altKey) return;
                var key = e.key.toLowerCase();
                var code = e.code || '';
                // Formatting keys, by physical position so they work on any layout. Without these the
                // browser's own Ctrl+B/I/U would edit the page behind the editor's model.
                var formatting = /^(KeyB|KeyI|KeyU|Digit[0-6])$/.test(code) && !e.shiftKey
                    || /^(KeyK|KeyQ|KeyX|KeyT)$/.test(code) && e.shiftKey;
                var hostKeys = ['s', 'o', 'n', 'w', 'f', 'p', 'k', 'v', 'z', 'y', 'a', 'c', 't', 'tab', 'pageup', 'pagedown', ',', '/'];
                if (!formatting && hostKeys.indexOf(key) < 0) return;
                // In the Code/Split source pane, CodeMirror's own undo/redo/select-all/copy/paste work
                // on plain text with no model to desync, and the formatting commands don't apply there.
                var inCode = document.activeElement && document.activeElement.closest && document.activeElement.closest('.CodeMirror');
                if (inCode && (formatting || 'vzyac'.indexOf(key) >= 0)) return;
                e.preventDefault();
                e.stopPropagation();
                window.chrome.webview.postMessage(JSON.stringify({
                    type: 'message', name: 'HostShortcut', args: { key: key, code: code, shift: e.shiftKey, formatting: formatting }
                }));
            }, true);
        ";

        private void HandleHostShortcut(JToken args)
        {
            var key = args["key"]?.ToString();
            var shift = args["shift"]?.ToObject<bool>() ?? false;
            var code = args["code"]?.ToString();
            Log($"HostShortcut: key={key}, code={code}, shift={shift}");
            if (args["formatting"]?.ToObject<bool>() == true && TryHandleFormattingShortcut(code, shift)) return;
            switch (key)
            {
                case "n" when shift: NewWindowMenuItem_Click(this, null); break;
                case "n": NewMenuItem_Click(this, null); break;
                case "o": OpenMenuItem_Click(this, null); break;
                case "s" when shift: SaveAsMenuItem_Click(this, null); break;
                case "s": SaveMenuItem_Click(this, null); break;
                case "f": ShowFindReplace(); break;
                case "k": ShowQuickOpen(); break;
                case "w" when shift: RequestClose(); break;
                case "w": CloseDocumentMenuItem_Click(this, null); break;
                case "t": NewMenuItem_Click(this, null); break;
                case "tab": _ = SwitchTabRelative(shift ? -1 : +1); break;
                case "pagedown": _ = SwitchTabRelative(+1); break;
                case "pageup": _ = SwitchTabRelative(-1); break;
                case "p": PrintMenuItem_Click(this, null); break;
                case ",": SettingsMenuItem_Click(this, null); break;
                case "/": CycleViewMode(); break;
                case "f6": CycleArea(shift ? -1 : +1); break;
                case "f10": case "alt": RevealCommandRow(true); break;
                case "f11": ToggleDistractionFree(); break;
                case "v": PasteFromClipboard(); break;
                case "z" when shift: RedoMenuItem_Click(this, null); break;
                case "z": UndoMenuItem_Click(this, null); break;
                case "y": RedoMenuItem_Click(this, null); break;
                case "a": SelectAllMenuItem_Click(this, null); break;
                case "c": CopyMenuItem_Click(this, null); break;
            }
        }

        // Copy goes through the editor's own Copy, as the original did: it puts the selection on the
        // clipboard as markdown text (+ HTML) through SetClipboard (see RegisterHandlers) instead of the
        // rendered page's plain text, and doesn't touch the document. Cut deliberately stays native:
        // the editor's Cut (cutHandler → partialRender) reproducibly left a removed paragraph on screen
        // that was no longer in the document, while the browser's own cut is reconciled correctly by
        // Muya's input handler.
        private void CopyMenuItem_Click(object sender, RoutedEventArgs e) => PostMessage("Copy", new { type = "normal" });

        // --- Undo / Redo ---
        // Same story as Paste below: the editor (Muya) has no undo of its own, and the original app
        // kept the history on the host (Typedown.Core's ContentHistory + EditorViewModel.Undo/Redo),
        // restoring a snapshot by sending SetMarkdown { text, cursor } — a message the editor still
        // listens for (Typedown.Editor/src/components/Editor/index.tsx). That host half was never
        // ported, so Ctrl+Z fell through to the browser's native contenteditable undo, which edits the
        // DOM behind Muya's back — the same desync that made paste lose content. Ctrl+Z/Ctrl+Y/
        // Ctrl+Shift+Z are now intercepted in HostShortcutScript and driven from here instead.
        // historyUpdating mirrors the original's contentUpdating: SetMarkdown makes the editor echo a
        // MarkdownChange for the restored text, which must not be recorded as a new edit; the
        // StateChange the editor always sends right after that echo clears it.
        private bool historyUpdating;

        private void SetUpHistory()
        {
            eventCenter.GetObservable<EditorEventArgs>("FileLoaded").Subscribe(x =>
            {
                history.InitHistory(x.Args["text"]?.ToString() ?? "");
                activeDoc.HistoryReady = true;
                if (!editorReady)
                {
                    editorReady = true;
                    // Typing goes to the document: otherwise the first focusable control, the tab
                    // strip's + button, takes keyboard focus and shows its focus ring.
                    DispatcherQueue.TryEnqueue(() => { if (!startPageShown) EditorView.Focus(FocusState.Programmatic); });
                }
            });
            eventCenter.GetObservable<EditorEventArgs>("MarkdownChange").Subscribe(x =>
            {
                lastEditorChange = DateTime.UtcNow;
                if (!historyUpdating) history.ContentChange(x.Args["text"]?.ToString() ?? "");
            });
            eventCenter.GetObservable<EditorEventArgs>("CursorChange").Subscribe(x =>
            {
                var cursor = x.Args["cursor"]?.ToObject<CursorState>();
                history.CursorChange(cursor);
                if (cursor != null) activeDoc.Cursor = cursor; // where this tab's cursor comes back to
            });
        }

        private void UndoMenuItem_Click(object sender, RoutedEventArgs e) => ApplyHistoryState(history.Undo());

        private void RedoMenuItem_Click(object sender, RoutedEventArgs e) => ApplyHistoryState(history.Redo());

        private void ApplyHistoryState(HistoryModel state)
        {
            if (state == null) return;
            historyUpdating = true;
            file.ReplaceBuffer(state.Text);
            PostMessage("SetMarkdown", new { text = state.Text, cursor = state.Cursor, basePath = file.ImageBasePath });
            Log("Undo/Redo: restored history snapshot");
        }

        // Routed through the editor's own SelectAll, as the original did, so Muya's selection model
        // (code blocks, tables) handles it rather than a raw DOM select-all.
        private void SelectAllMenuItem_Click(object sender, RoutedEventArgs e) => PostMessage("SelectAll", null);

        // --- Paste ---
        // The real bug this fixes: the editor bundle (Typedown.Editor) already has a complete,
        // markdown-aware paste pipeline sitting unused — Muya/index.tsx listens for a 'Paste' message
        // and routes it straight to ContentState.pasteHandler (pasteCtrl.js), which correctly parses
        // pasted markdown/HTML into real blocks (headings, lists, code fences, ...). But nothing on the
        // host side ever sent that message, so Ctrl+V was never intercepted here and WebView2's own
        // native contenteditable paste ran instead — which just dumps the pasted text as a raw string
        // into whatever single block the cursor was in, with no markdown parsing at all.
        // Confirmed as a real, reproducible data-loss bug, not a hypothetical: pasting a multi-paragraph
        // AI-chat-style markdown block showed the raw "#"/"-"/backtick syntax literally instead of
        // rendering, and typing afterward inserted characters in the wrong place or dropped them
        // outright (Muya's cursor/selection model was left out of sync with the actual DOM the native
        // paste had produced) — this is what "editing does nothing, then the document loses content"
        // in CHANGES.md's bug report actually was.
        // Reads both plain text and HTML from the Windows clipboard (matching Clipboard.paste's
        // { type, text, html } shape) so copying from a real web page/Word/etc. still gets HTML-aware
        // parsing, not just a markdown guess — GetHtmlFormatAsync() returns the raw CF_HTML clipboard
        // format (a header with Version/StartHTML/EndHTML byte offsets ahead of the actual fragment),
        // so HtmlFormatHelper.GetStaticFragment unwraps it to the clean HTML pasteCtrl.js expects.
        // Images (screenshots, copied image files, browser "Copy image") are handled on the host too,
        // see the Image paste section below — the editor's own pasteImage() is unreachable dead code.
        private void PasteMenuItem_Click(object sender, RoutedEventArgs e) => PasteFromClipboard();

        private async void PasteFromClipboard()
        {
            try
            {
                var dataView = await GetClipboardContent();
                string text = null;
                string html = null;
                if (Offers(dataView, StandardDataFormats.Text))
                    text = await dataView.GetTextAsync();
                if (Offers(dataView, StandardDataFormats.Html))
                    html = HtmlFormatHelper.GetStaticFragment(await dataView.GetHtmlFormatAsync());
                if (WebImageSource(html) is string webImage)
                {
                    PostMessage("InsertImage", new { src = webImage });
                    Log($"Paste: web image {webImage}");
                    return;
                }
                if (!string.IsNullOrEmpty(text) || !string.IsNullOrEmpty(html))
                {
                    PostMessage("Paste", new { type = "normal", text, html });
                    Log("Paste: forwarded clipboard text/html to editor");
                    return;
                }
                if (await PasteImageFromClipboard(dataView)) return;
                Log($"Paste: nothing pasteable on clipboard (format count: {FormatCount(dataView)})");
            }
            catch (Exception ex)
            {
                Log($"PasteFromClipboard EXCEPTION: {ex}");
            }
        }

        // --- Image paste ---
        // Ported from the original's EditorViewModel.Paste + ImageAction.DoClipboardAction, in the same
        // order: text/HTML wins (Word and browsers also put a picture of the selection on the clipboard,
        // which must not replace the text); an HTML fragment that is just one <img> from the web ("Copy
        // image" in a browser) is inserted by its URL; a single copied image file is inserted by its
        // path, like Edit > Insert Image; a bare bitmap (a screenshot) is saved as a PNG.
        // Where the PNG goes follows the ported InsertClipboardImageAction setting (Settings > "Save
        // pasted images to"): None, the original's default, saves to DefaultImageBasePath
        // (Pictures\Caret) with an absolute path; CopyToPath saves to InsertClipboardImageCopyPath
        // (./images) next to the note with a relative link, so the note and its images move together.
        // An Untitled note has no folder yet, so it falls back to Pictures\Caret either way.
        private static readonly System.Text.RegularExpressions.Regex SingleImgFragment = new(
            @"^\s*(?:<!--[\s\S]*?-->\s*)*<img\b[^>]*?\bsrc\s*=\s*[""']([^""']+)[""'][^>]*>\s*(?:<!--[\s\S]*?-->\s*)*$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private static string WebImageSource(string html)
        {
            if (string.IsNullOrEmpty(html)) return null;
            var match = SingleImgFragment.Match(html);
            if (!match.Success) return null;
            var src = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
            return Uri.TryCreate(src, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri.AbsoluteUri : null;
        }

        private async Task<bool> PasteImageFromClipboard(DataPackageView dataView)
        {
            var (paths, _) = await GetClipboardFiles(dataView);
            if (paths.Length == 1 && FileTypeHelper.IsImageFile(paths[0]) && File.Exists(paths[0]))
            {
                PostMessage("InsertImage", new { src = paths[0] });
                Log($"Paste: image file {paths[0]}");
                return true;
            }
            if (!Offers(dataView, StandardDataFormats.Bitmap)) return false;
            var src = await SaveClipboardBitmap(await dataView.GetBitmapAsync());
            PostMessage("InsertImage", new { src });
            Log($"Paste: saved clipboard image as {src}");
            return true;
        }

        private async Task<string> SaveClipboardBitmap(Windows.Storage.Streams.RandomAccessStreamReference bitmap)
        {
            var nextToNote = settings.InsertClipboardImageAction == InsertImageAction.CopyToPath && !string.IsNullOrEmpty(file.FilePath);
            var folder = nextToNote
                ? Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file.FilePath), settings.InsertClipboardImageCopyPath))
                : settings.DefaultImageBasePath;
            Directory.CreateDirectory(folder);
            var baseName = $"pasted-{DateTime.Now:yyyyMMdd-HHmmss}";
            var name = baseName + ".png";
            for (var i = 2; File.Exists(Path.Combine(folder, name)); i++) name = $"{baseName}-{i}.png";
            var path = Path.Combine(folder, name);

            using (var input = await bitmap.OpenReadAsync())
            {
                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(input);
                using var pixels = await decoder.GetSoftwareBitmapAsync(
                    Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
                using var output = File.Create(path).AsRandomAccessStream();
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, output);
                encoder.SetSoftwareBitmap(pixels);
                await encoder.FlushAsync();
            }
            return nextToNote ? $"{settings.InsertClipboardImageCopyPath.TrimEnd('/', '\\')}/{name}".Replace('\\', '/') : path;
        }

        // --- Menu bar handlers ---
        // Unpackaged WinUI 3 apps must initialize file pickers with the owning window's HWND
        // (WinRT.Interop.InitializeWithWindow) — there's no implicit window context like there is
        // for a packaged/UWP app.

        private async void NewMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!await MakeRoomForDocument()) return;
            file.NewFile();
            UpdateTitle();
        }

        // Ported from the original's FileViewModel.NewWindowCommand (Typedown\Utilities\Common.cs's
        // OpenNewWindow, minus the cross-process pipe redirection — see the openWindows field comment
        // above). A blank new window doesn't need ConfirmDiscardChangesIfNeeded — it doesn't touch
        // this window's document at all.
        private void NewWindowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var newWindow = new MainWindow();
            newWindow.Activate();
            Log("NewWindow: opened blank window");
        }

        // Ported from the original's OnOpenInNewWindowClick (FolderPage.xaml.cs).
        private void OpenInNewWindowContext_Click(object sender, RoutedEventArgs e)
        {
            if (GetContextItem(sender) is not ExplorerItem item || item.Type != ExplorerItem.ExplorerItemType.File) return;
            if (FocusIfOpenElsewhere(item.FullPath)) return;
            var newWindow = new MainWindow(item.FullPath);
            newWindow.Activate();
            Log($"NewWindow: opened {item.FullPath}");
        }

        // Picks first, then makes room: with tabs a cancelled picker leaves no empty tab behind, and
        // without them the unsaved-changes prompt only comes once there's really a file to open.
        private async void OpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            foreach (var ext in Utilities.FileTypeHelper.Markdown) picker.FileTypeFilter.Add(ext);
            var pickedFile = await picker.PickSingleFileAsync();
            if (pickedFile == null) return;
            await OpenDocument(pickedFile.Path, "OpenFile");
        }

        private async void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (startPageShown) return;
            if (await file.Save())
            {
                recentFiles.Record(file.FilePath);
                RefreshRecentFilesMenu();
            }
            else
            {
                await SaveAsInternal();
            }
            Log($"Save: {file.FilePath}");
        }

        private async void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!startPageShown) await SaveAsInternal();
        }

        private async System.Threading.Tasks.Task SaveAsInternal()
        {
            var picker = new FileSavePicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.FileTypeChoices.Add("Markdown", new System.Collections.Generic.List<string> { ".md" });
            picker.SuggestedFileName = string.IsNullOrEmpty(file.FilePath) ? Locale.GetString("Untitled") : Path.GetFileNameWithoutExtension(file.DisplayName);
            var pickedFile = await picker.PickSaveFileAsync();
            if (pickedFile == null) return;
            await file.SaveAs(pickedFile.Path);
            recentFiles.Record(pickedFile.Path);
            RefreshRecentFilesMenu();
            UpdateTitle();
            Log($"SaveAs: {pickedFile.Path}");
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => RequestClose();

        // --- Open Recent ---
        // Reimplemented against RecentFilesService (see Services/RecentFilesService.cs) rather than
        // AccessHistory/EF Core. MenuFlyoutSubItem has no Opening event to hook (confirmed by the XAML
        // compiler — WMC0011: Unknown member 'Opening'), so instead of rebuilding lazily on open, this
        // is kept in sync eagerly: RefreshRecentFilesMenu runs once at startup and again after every
        // Record/Remove/Clear call.
        private void RefreshRecentFilesMenu()
        {
            OpenRecentMenu.Items.Clear();
            if (recentFiles.Files.Count == 0)
            {
                OpenRecentMenu.Items.Add(new MenuFlyoutItem { Text = Locale.GetString("NoRecentFiles"), IsEnabled = false });
                return;
            }
            foreach (var path in recentFiles.Files)
            {
                var item = new MenuFlyoutItem { Text = path };
                item.Click += async (s, args) => await OpenRecentFile(path);
                OpenRecentMenu.Items.Add(item);
            }
            OpenRecentMenu.Items.Add(new MenuFlyoutSeparator());
            var clearItem = new MenuFlyoutItem { Text = Locale.GetString("ClearRecentFiles") };
            clearItem.Click += (s, args) => { recentFiles.Clear(); RefreshRecentFilesMenu(); };
            OpenRecentMenu.Items.Add(clearItem);
        }

        private async System.Threading.Tasks.Task OpenRecentFile(string path)
        {
            // Checked before the discard-changes prompt, matching the original's LoadFile (which
            // checks TryGetOpenedWindow before AskToSave) — no reason to ask about unsaved changes in
            // this window when the destination is just switching focus to a different one.
            if (FocusIfOpenElsewhere(path)) return;
            if (!File.Exists(path))
            {
                // Matches the original's behavior in LoadFile's not-found branch: a stale entry gets
                // dropped from history instead of leaving a dead link around.
                recentFiles.Remove(path);
                RefreshRecentFilesMenu();
                Log($"OpenRecentFile: missing {path}, removed from history");
                return;
            }
            await OpenDocument(path, "OpenRecentFile");
        }

        // --- Sidebar nav rail ---
        // New in the sidebar restructure (Phase 2 of the warm-autumn reskin): Home/Recent/Favorites/
        // All Files/Templates/Trash each swap in their own panel below the nav rail — only one is ever
        // visible. "All Files" doesn't get its own panel; it just hides the others so the Folder tree
        // (already below, always shown once a folder's open) is what's visible.

        private readonly string templatesFolder = Path.Combine(Config.GetLocalFolderPath(), "Templates");

        // The sidebar's two navigation lists (Create, Library) act as one: selecting in one clears the other.
        private IEnumerable<ListViewItem> NavItems => NavCreateListView.Items.Concat(NavLibraryListView.Items).OfType<ListViewItem>();

        private string SelectedNavTag => ((NavCreateListView.SelectedItem ?? NavLibraryListView.SelectedItem) as ListViewItem)?.Tag as string;

        // Returns false when that item was already selected (so no SelectionChanged follows).
        private bool SelectNav(string tag)
        {
            var item = NavItems.FirstOrDefault(i => i.Tag as string == tag);
            if (item == null || item.IsSelected) return false;
            (NavCreateListView.Items.Contains(item) ? NavCreateListView : NavLibraryListView).SelectedItem = item;
            return true;
        }

        private bool clearingNav;

        // The panel under the navigation for the selected item (none in the narrow sidebar).
        private void ShowNavPanels(string tag)
        {
            if (SidebarNarrow) tag = null;
            // The Library's lists go with the Library when it is collapsed
            if (settings.LibraryCollapsed && tag is "Recent" or "Favorites" or "Templates" or "Trash") tag = null;
            HomePanel.Visibility = tag == "Home" ? Visibility.Visible : Visibility.Collapsed;
            RecentNavListView.Visibility = tag == "Recent" ? Visibility.Visible : Visibility.Collapsed;
            FavoritesPanel.Visibility = tag == "Favorites" ? Visibility.Visible : Visibility.Collapsed;
            TemplatesPanel.Visibility = tag == "Templates" ? Visibility.Visible : Visibility.Collapsed;
            TrashPanel.Visibility = tag == "Trash" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateLibraryHeader()
        {
            NavLibraryListView.Visibility = settings.LibraryCollapsed && !SidebarNarrow ? Visibility.Collapsed : Visibility.Visible;
            NavLibraryChevron.Glyph = settings.LibraryCollapsed ? "\uE76C" : "\uE70E";
        }

        private void NavLibraryHeaderButton_Click(object sender, RoutedEventArgs e)
        {
            settings.LibraryCollapsed = !settings.LibraryCollapsed;
            UpdateLibraryHeader();
            ShowNavPanels(SelectedNavTag);
            RefreshNavPanel(SelectedNavTag); // what changed while it was hidden shows now
        }

        private void RefreshNavPanel(string tag)
        {
            switch (tag)
            {
                case "Recent": RefreshRecentNavList(); break;
                case "Favorites": RefreshFavoritesNavList(); break;
                case "Templates": RefreshTemplatesNavList(); break;
                case "Trash": RefreshTrashNavList(); break;
            }
        }

        private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (clearingNav) return;
            if (((ListView)sender).SelectedItem != null)
            {
                clearingNav = true;
                (sender == NavCreateListView ? NavLibraryListView : NavCreateListView).SelectedItem = null;
                clearingNav = false;
            }
            var tag = SelectedNavTag;
            // Something chose a Library item (a shortcut, a command) while the Library is collapsed: show it
            if (settings.LibraryCollapsed && tag is "Recent" or "Favorites" or "AllFiles" or "Templates" or "Trash")
            {
                settings.LibraryCollapsed = false;
                UpdateLibraryHeader();
            }
            PeekSidebarFor(tag);
            ShowNavPanels(tag);
            SetConvertPageVisible(tag is "Convert" or "Emails");
            if (tag == "Emails") ConvertEmailCard.StartBringIntoView();
            RefreshNavPanel(tag);
        }

        // After a rename: the recent-files menu, and whichever lists of files are on screen.
        private void RefreshFileLists()
        {
            RefreshRecentFilesMenu();
            if (RecentNavListView.Visibility == Visibility.Visible) RefreshRecentNavList();
            if (FavoritesPanel.Visibility == Visibility.Visible) RefreshFavoritesNavList();
            if (startPageShown) RefreshStartPageList();
        }

        private void RefreshRecentNavList() =>
            RecentNavListView.ItemsSource = recentFiles.Files.Select(p => new NavFileEntry(p)).ToList();

        private async void RecentNavListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not NavFileEntry entry) return;
            await OpenRecentFile(entry.FullPath);
            RefreshRecentNavList(); // opening moves it to the top, or drops it if it no longer exists
        }

        private void RefreshFavoritesNavList()
        {
            favoritesService.Reload();
            UpdateFavoriteButton(); // another window may have changed this file's favorite
            var entries = favoritesService.Files.Select(p => new NavFileEntry(p)).ToList();
            FavoritesNavListView.ItemsSource = entries;
            FavoritesEmptyText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // Not just a call to OpenRecentFile: a missing favorite needs to fall out of favoritesService,
        // not recentFiles, on a stale entry.
        private async void FavoritesNavListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not NavFileEntry entry) return;
            if (FocusIfOpenElsewhere(entry.FullPath)) return;
            if (!File.Exists(entry.FullPath))
            {
                favoritesService.Remove(entry.FullPath);
                RefreshFavoritesNavList();
                Log($"FavoritesNav: missing {entry.FullPath}, removed from favorites");
                return;
            }
            await OpenDocument(entry.FullPath, "FavoritesNav");
        }

        private void FavoriteToggleContext_Click(object sender, RoutedEventArgs e)
        {
            if (GetContextItem(sender) is not ExplorerItem item || item.Type != ExplorerItem.ExplorerItemType.File) return;
            var isFavorite = favoritesService.Toggle(item.FullPath);
            if (FavoritesPanel.Visibility == Visibility.Visible) RefreshFavoritesNavList();
            UpdateFavoriteButton();
            Log($"Favorite: {(isFavorite ? "added" : "removed")} {item.FullPath}");
        }

        // Templates aren't tracked by a service class the way Recent/Favorites are — they're just
        // whatever .md files sit in templatesFolder, so listing it IS the persistence.
        private void RefreshTemplatesNavList()
        {
            try
            {
                Directory.CreateDirectory(templatesFolder);
                TemplatesNavListView.ItemsSource = Directory.GetFiles(templatesFolder, "*.md")
                    .Select(p => new NavFileEntry(p)).ToList();
            }
            catch (Exception ex)
            {
                Log($"RefreshTemplatesNavList EXCEPTION: {ex}");
            }
        }

        // Clicking a template starts a new document pre-filled with its content — same shape as
        // AutoBackup recovery (ApplyRecoveredBackup leaves the new document dirty/unsaved, which is
        // right here too: it's a copy of the template, not the template file itself).
        private async void TemplatesNavListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not NavFileEntry entry) return;
            try
            {
                var content = (await TextFileEncoding.ReadAsync(entry.FullPath)).Text;
                if (!await MakeRoomForDocument()) return;
                file.NewFile();
                file.ApplyRecoveredBackup(content);
                UpdateTitle();
                Log($"Templates: new document from {entry.FullPath}");
            }
            catch (Exception ex)
            {
                await ShowErrorDialog(Locale.GetString("CouldntOpenTemplate"), ex.Message);
            }
        }

        private async void SaveAsTemplate_Click(object sender, RoutedEventArgs e)
        {
            var name = await PromptForName(Locale.GetString("SaveAsTemplate"), Locale.GetString("Untitled") + ".md");
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) name += ".md";
            try
            {
                Directory.CreateDirectory(templatesFolder);
                var path = Path.Combine(templatesFolder, name);
                if (File.Exists(path)) throw new IOException($"'{name}' already exists.");
                await File.WriteAllTextAsync(path, file.Markdown);
                RefreshTemplatesNavList();
                Log($"SaveAsTemplate: {path}");
            }
            catch (Exception ex)
            {
                await ShowErrorDialog(Locale.GetString("CouldntSaveTemplate"), ex.Message);
            }
        }

        private void RefreshTrashNavList()
        {
            trashService.Reload();
            TrashNavListView.ItemsSource = trashService.Entries.ToList(); // a new list, or the ListView keeps the old one
            TrashEmptyText.Visibility = trashService.Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // Not a programmatic restore (that needs IFileOperation/Shell COM interop this project doesn't
        // have) — opens the real Recycle Bin so the user can restore it themselves. See
        // Services/TrashService.cs's header comment for why this is a log, not a reimplementation.
        private void TrashNavListView_ItemClick(object sender, ItemClickEventArgs e) =>
            System.Diagnostics.Process.Start("explorer.exe", "shell:RecycleBinFolder");

        // --- Export & Print ---
        // Reimplemented, not ported: the original's Export()/ExportCallback() went through a whole
        // ExportConfig/IFileExport/PdfiumViewer pipeline (Controls/DialogControls/AddExportConfigDialog,
        // Enums/ExportType, per-format config models) that isn't ported. PDF and Print use WebView2's
        // own native PrintToPdfAsync/ShowPrintUI instead — genuinely simpler than replicating PDF
        // conversion by hand, and it's the current document as actually rendered, not a re-parse.
        // HTML export is the one case that still goes through the editor's own clean HTML generator
        // (ExportHtml, JS-side) via the real Export/ExportCallback wire messages, since WebView2 has no
        // "give me clean semantic HTML" API of its own to substitute.
        private TaskCompletionSource<string> pendingExportHtml;

        private Task<string> RequestExportHtml()
        {
            pendingExportHtml = new TaskCompletionSource<string>();
            PostMessage("Export", new { type = "export", context = (object)null, basePath = file.ImageBasePath, title = file.DisplayName });
            return pendingExportHtml.Task;
        }

        private async void ExportHtmlMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.FileTypeChoices.Add("HTML", new System.Collections.Generic.List<string> { ".html" });
            picker.SuggestedFileName = Path.GetFileNameWithoutExtension(file.DisplayName);
            var pickedFile = await picker.PickSaveFileAsync();
            if (pickedFile == null) return;
            var html = await RequestExportHtml();
            if (html == null)
            {
                Log("ExportHtml: editor returned no html");
                return;
            }
            await File.WriteAllTextAsync(pickedFile.Path, html);
            Log($"ExportHtml: {pickedFile.Path}");
        }

        private async void ExportPdfMenuItem_Click(object sender, RoutedEventArgs e)
        {
            PdfExportOptionsDialog.XamlRoot = Content.XamlRoot;
            if (await PdfExportOptionsDialog.ShowAsync() != ContentDialogResult.Primary) return;
            var picker = new FileSavePicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.FileTypeChoices.Add("PDF", new System.Collections.Generic.List<string> { ".pdf" });
            picker.SuggestedFileName = Path.GetFileNameWithoutExtension(file.DisplayName);
            var pickedFile = await picker.PickSaveFileAsync();
            if (pickedFile == null) return;
            var printSettings = BuildPdfPrintSettings();
            var ok = await EditorView.CoreWebView2.PrintToPdfAsync(pickedFile.Path, printSettings);
            Log($"ExportPdf: {pickedFile.Path}, success={ok}");
        }

        // (width, height) in inches — CoreWebView2PrintSettings.PageWidth/PageHeight's own unit.
        private static readonly (double Width, double Height)[] PdfPageSizes =
        {
            (8.5, 11), // Letter
            (8.27, 11.69), // A4
            (8.5, 14), // Legal
        };

        private CoreWebView2PrintSettings BuildPdfPrintSettings()
        {
            var settings = EditorView.CoreWebView2.Environment.CreatePrintSettings();
            settings.Orientation = PdfOrientationComboBox.SelectedIndex == 1
                ? CoreWebView2PrintOrientation.Landscape : CoreWebView2PrintOrientation.Portrait;
            var (width, height) = PdfPageSizes[PdfPageSizeComboBox.SelectedIndex];
            // MediaSize defaults to Default, which ignores PageWidth/PageHeight entirely (the SDK docs
            // say to use Custom whenever you're setting them) — without this, picking A4 or Legal here
            // silently did nothing and every export used the printer's default media size.
            settings.MediaSize = CoreWebView2PrintMediaSize.Custom;
            settings.PageWidth = width;
            settings.PageHeight = height;
            settings.ShouldPrintBackgrounds = PdfBackgroundsToggle.IsOn;
            settings.ShouldPrintHeaderAndFooter = PdfHeaderFooterToggle.IsOn;
            return settings;
        }

        private async void ExportTextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.FileTypeChoices.Add(Locale.GetString("PlainText"), new System.Collections.Generic.List<string> { ".txt" });
            picker.SuggestedFileName = Path.GetFileNameWithoutExtension(file.DisplayName);
            var pickedFile = await picker.PickSaveFileAsync();
            if (pickedFile == null) return;
            await File.WriteAllTextAsync(pickedFile.Path, file.Markdown);
            Log($"ExportText: {pickedFile.Path}");
        }

        private void PrintMenuItem_Click(object sender, RoutedEventArgs e)
        {
            EditorView.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.System);
            Log("Print: ShowPrintUI invoked");
        }

        // --- MarkItDown import ---
        // New since the fork, not in the original at all: shells out to Microsoft's own MarkItDown
        // (https://github.com/microsoft/markitdown, a Python CLI) to convert Office documents, PDFs,
        // images, audio and more into Markdown, opened as a new Caret document. Genuinely external —
        // there's no .NET port of it and no Python runtime bundled with Caret, so this is a subprocess
        // call against whatever `markitdown` the user has on PATH (pip install markitdown[all]), not a
        // vendored dependency. If it's missing, the failure path explains how to install it rather than
        // failing silently or crashing.
        private static readonly string[] MarkItDownFileTypes =
        {
            ".pdf", ".docx", ".doc", ".pptx", ".ppt", ".xlsx", ".xls", ".html", ".htm",
            ".csv", ".json", ".xml", ".txt", ".png", ".jpg", ".jpeg", ".gif", ".bmp",
            ".mp3", ".wav", ".m4a", ".zip", ".epub", ".msg", ".eml",
        };

        private async void ImportMarkItDownMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            foreach (var ext in MarkItDownFileTypes) picker.FileTypeFilter.Add(ext);
            var pickedFile = await picker.PickSingleFileAsync();
            if (pickedFile == null) return;
            if (!await MakeRoomForDocument()) return;

            // Word, Excel, PowerPoint, PDF, CSV and emails are converted by Caret itself (Services/Conversion) —
            // no Python needed. MarkItDown below is only for the other formats it knows.
            if (DocumentConverter.IsSupported(pickedFile.Path))
            {
                try
                {
                    var result = await DocumentConverter.ConvertAsync(pickedFile.Path, new ConversionOptions
                    {
                        SlideHeadingFormat = Locale.GetString("ConvertSlideHeading"),
                        SlideHeadingUntitledFormat = Locale.GetString("ConvertSlideHeadingUntitled"),
                        NotesLabel = Locale.GetString("ConvertNotesLabel"),
                        EmailRedact = settings.ConvertEmailRedact,
                    });
                    if (string.IsNullOrWhiteSpace(result.Markdown))
                    {
                        await ShowErrorDialog(Locale.GetString("ImportFailed"), Locale.GetString(result.Warnings.Contains(ConversionWarning.PdfHasNoText) ? "ConvertPdfNoText" : "ConvertNoContent"));
                        return;
                    }
                    file.NewFile();
                    file.ApplyRecoveredBackup(result.Markdown);
                    UpdateTitle();
                    Log($"Import: {pickedFile.Path} ({result.Markdown.Length} chars)");
                }
                catch (Exception ex)
                {
                    Log($"Import: failed {pickedFile.Path}: {ex}");
                    await ShowErrorDialog(Locale.GetString("ImportFailed"), FriendlyConversionError(ex));
                }
                return;
            }

            ImportMarkItDownMenuItem.IsEnabled = false;
            // UpdateTitle() sets both of these from file's actual state — setting them directly here is
            // just a transient status message, restored via UpdateTitle() itself in the finally block.
            TitleTextBlock.Text = Locale.GetString("ConvertingWithMarkItDown");
            Title = TitleTextBlock.Text;
            try
            {
                Log($"MarkItDown: converting {pickedFile.Path}");
                var (ok, output, error) = await RunMarkItDown(pickedFile.Path);
                if (error == MarkItDownNotFoundSentinel)
                {
                    if (!await InstallMarkItDownAsync()) return; // user declined, or install itself failed (dialog already shown)
                    Log($"MarkItDown: retrying conversion for {pickedFile.Path} after install");
                    TitleTextBlock.Text = Locale.GetString("ConvertingWithMarkItDown");
                    Title = TitleTextBlock.Text;
                    (ok, output, error) = await RunMarkItDown(pickedFile.Path);
                    if (error == MarkItDownNotFoundSentinel)
                    {
                        await ShowErrorDialog(Locale.GetString("MarkItDownStillNotFound"), Locale.GetString("MarkItDownStillNotFoundDetail"));
                        return;
                    }
                }
                if (!ok || string.IsNullOrWhiteSpace(output))
                {
                    await ShowErrorDialog(Locale.GetString("MarkItDownConversionFailed"),
                        string.IsNullOrWhiteSpace(error) ? Locale.GetString("MarkItDownNoOutput") : error);
                    Log($"MarkItDown: conversion failed for {pickedFile.Path}: {error}");
                    return;
                }
                file.NewFile();
                file.ApplyRecoveredBackup(output);
                UpdateTitle();
                Log($"MarkItDown: imported {pickedFile.Path} ({output.Length} chars)");
            }
            finally
            {
                UpdateTitle();
                ImportMarkItDownMenuItem.IsEnabled = true;
            }
        }

        private const string MarkItDownNotFoundSentinel = "__markitdown_not_found__";

        // Some conversions (audio transcription in particular) genuinely take a while — 2 minutes
        // before giving up and killing it, rather than either blocking forever or timing out too
        // eagerly on a large PDF.
        private static async Task<(bool ok, string output, string error)> RunMarkItDown(string sourcePath)
        {
            var (exitCode, stdout, stderr, timedOut) = await RunProcessAsync("markitdown", new[] { sourcePath }, 120_000);
            if (timedOut) return (false, null, Locale.GetString("MarkItDownTimedOut"));
            if (exitCode == null) return (false, null, MarkItDownNotFoundSentinel);
            return (exitCode == 0, stdout, stderr);
        }

        // --- MarkItDown self-install ---
        // The failure mode this exists for: "MarkItDown not found" used to just print a terminal
        // command and leave the user to run it themselves — fine for a developer, a dead end for
        // anyone who isn't comfortable with pip and PATH. This drives the whole thing from inside
        // Caret instead: find a Python, run `pip install --user markitdown[all]`, then work out where
        // pip put the executable and add it to PATH — both for this already-running process (so the
        // very next conversion attempt, moments later, just works) and persisted to the user's
        // environment (so new terminals and future launches of Caret find it too, without needing a
        // restart). Returns true only if markitdown is ready to use by the time it returns.
        private async Task<bool> InstallMarkItDownAsync()
        {
            // A Store install never downloads and runs code by itself (Store policy), and neither does
            // one whose administrator switched it off: explain how to install MarkItDown instead. The
            // formats most people need are built in anyway.
            if (Config.IsStoreInstall || Config.PolicyDisablesMarkItDownInstall)
            {
                var manual = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = Locale.GetString("MarkItDownNotFound"),
                    Content = Locale.GetString("MarkItDownManualInstall"),
                    PrimaryButtonText = Locale.GetString("LearnMore"),
                    CloseButtonText = Locale.GetString("OK"),
                    DefaultButton = ContentDialogButton.Close,
                };
                if (await manual.ShowAsync() == ContentDialogResult.Primary)
                    await Launcher.LaunchUriAsync(new Uri("https://github.com/microsoft/markitdown"));
                return false;
            }

            var confirm = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = Locale.GetString("MarkItDownNotFound"),
                Content = Locale.GetString("MarkItDownInstallPrompt"),
                PrimaryButtonText = Locale.GetString("Install"),
                CloseButtonText = Locale.GetString("NotNow"),
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return false;

            var python = await FindPythonLauncherAsync();
            if (python == null)
            {
                var noPython = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = Locale.GetString("PythonNotFound"),
                    Content = Locale.GetString("PythonNotFoundDetail"),
                    PrimaryButtonText = Locale.GetString("OpenPythonOrg"),
                    CloseButtonText = Locale.GetString("OK"),
                    DefaultButton = ContentDialogButton.Primary,
                };
                if (await noPython.ShowAsync() == ContentDialogResult.Primary)
                    await Launcher.LaunchUriAsync(new Uri("https://www.python.org/downloads/"));
                return false;
            }

            ImportMarkItDownMenuItem.IsEnabled = false;
            TitleTextBlock.Text = Locale.GetString("InstallingMarkItDown");
            Title = TitleTextBlock.Text;
            Log($"MarkItDown: installing via {python.Value.exe} {string.Join(' ', python.Value.prefixArgs)}");
            try
            {
                var installArgs = python.Value.prefixArgs.Concat(new[] { "-m", "pip", "install", "--user", "markitdown[all]" });
                var (exitCode, _, pipError, timedOut) = await RunProcessAsync(python.Value.exe, installArgs, 600_000);
                if (timedOut || exitCode != 0)
                {
                    Log($"MarkItDown: pip install failed (timedOut={timedOut}): {pipError}");
                    await ShowErrorDialog(Locale.GetString("MarkItDownInstallFailed"),
                        timedOut ? Locale.GetString("MarkItDownInstallTimedOut") :
                        string.IsNullOrWhiteSpace(pipError) ? Locale.GetString("PipNoOutput") : pipError);
                    return false;
                }

                // pip install --user succeeding doesn't guarantee the resulting Scripts folder is on
                // PATH yet (pip's own "not on PATH" warning is common) — ask Python directly where its
                // user site lives rather than hoping the caller's next Process.Start("markitdown")
                // resolves by luck. The user Scripts folder is always the "Scripts" sibling of the
                // user site-packages folder in CPython's own layout, on every Python distribution
                // this has been checked against (python.org installer, Microsoft Store package).
                var siteArgs = python.Value.prefixArgs.Concat(new[] { "-m", "site", "--user-site" });
                var (siteExit, siteOut, _, _) = await RunProcessAsync(python.Value.exe, siteArgs, 30_000);
                if (siteExit == 0 && !string.IsNullOrWhiteSpace(siteOut))
                {
                    var scriptsDir = Path.Combine(Path.GetDirectoryName(siteOut.Trim()), "Scripts");
                    if (File.Exists(Path.Combine(scriptsDir, "markitdown.exe")))
                    {
                        AddToPath(scriptsDir);
                        Log($"MarkItDown: added {scriptsDir} to PATH");
                    }
                }

                Log("MarkItDown: install finished");
                return true;
            }
            finally
            {
                ImportMarkItDownMenuItem.IsEnabled = true;
            }
        }

        private static async Task<(string exe, string[] prefixArgs)?> FindPythonLauncherAsync()
        {
            // "py -3" (the Windows Python launcher) first since it's the most reliable way to find a
            // real Python 3 regardless of what's on PATH; "python"/"python3" as fallbacks for machines
            // without the launcher installed.
            var candidates = new (string exe, string[] prefixArgs)[]
            {
                ("py", new[] { "-3" }),
                ("python", Array.Empty<string>()),
                ("python3", Array.Empty<string>()),
            };
            foreach (var candidate in candidates)
            {
                var (exitCode, _, _, _) = await RunProcessAsync(candidate.exe, candidate.prefixArgs.Concat(new[] { "--version" }), 10_000);
                if (exitCode == 0) return candidate;
            }
            return null;
        }

        // Adds directory to PATH in both the Process scope (so it's visible to this already-running
        // app immediately — no restart needed before the retried conversion below) and the User scope
        // (so it's still there the next time Caret, or any new terminal, starts).
        private void AddToPath(string directory)
        {
            var process = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (!process.Split(';', StringSplitOptions.RemoveEmptyEntries).Contains(directory, StringComparer.OrdinalIgnoreCase))
                Environment.SetEnvironmentVariable("PATH", process.TrimEnd(';') + ";" + directory);

            // The user PATH is read raw from the registry and written back with its own value kind:
            // Environment.Get/SetEnvironmentVariable(User) would expand "%USERPROFILE%\..." entries
            // into fixed paths and turn an expandable value into a plain one.
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Environment", writable: true);
                if (key == null) return;
                var raw = key.GetValue("Path", "", Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
                var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Contains(directory, StringComparer.OrdinalIgnoreCase)) return;
                var kind = key.GetValueNames().Contains("Path", StringComparer.OrdinalIgnoreCase) ? key.GetValueKind("Path") : Microsoft.Win32.RegistryValueKind.ExpandString;
                if (kind != Microsoft.Win32.RegistryValueKind.String) kind = Microsoft.Win32.RegistryValueKind.ExpandString;
                key.SetValue("Path", raw.Length == 0 ? directory : raw.TrimEnd(';') + ";" + directory, kind);
                // Tell Explorer, so new terminals and apps see it without signing out
                SendMessageTimeout(new IntPtr(0xFFFF), 0x001A, IntPtr.Zero, "Environment", 0x0002, 5000, out _);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
            {
                Log($"MarkItDown: couldn't add {directory} to the user PATH: {ex.Message}");
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);

        // Generic subprocess runner shared by MarkItDown's conversion, install, and Python-detection
        // paths. exitCode is null when the executable itself couldn't be found (Win32Exception from
        // Process.Start) — distinct from a real, ran-but-failed exit code — so callers can tell "not
        // installed" apart from "installed but errored".
        private static async Task<(int? exitCode, string stdout, string stderr, bool timedOut)> RunProcessAsync(
            string fileName, IEnumerable<string> args, int timeoutMs)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // Without both of these, non-ASCII text round-trips through the Windows ANSI code
                // page instead of UTF-8 on both ends: Python's own stdout defaults to the console code
                // page on a redirected pipe (mangling anything outside it to "?"), and .NET's Process
                // decodes redirected output with the system codepage by default too, not UTF-8. Any
                // accented/CJK/Cyrillic text in the source document would otherwise come through
                // silently corrupted with no error — this machine's own file paths already have
                // accented characters, so this isn't a hypothetical case.
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
            startInfo.Environment["PYTHONUTF8"] = "1";
            foreach (var arg in args) startInfo.ArgumentList.Add(arg);
            using var process = new System.Diagnostics.Process { StartInfo = startInfo };
            try
            {
                process.Start();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return (null, null, null, false);
            }
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            var exited = await Task.Run(() => process.WaitForExit(timeoutMs));
            if (!exited)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                return (null, null, null, true);
            }
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            return (process.ExitCode, stdout, stderr, false);
        }

        // --- Image handling ---
        // Reimplemented, not ported: the original's ImageToolbar/ImageSelector floating controls and
        // drag-drop-onto-EditorContainer path aren't built — this is the same PostMessage("InsertImage",
        // { src }) the original's drag-drop handler sent (EditorContainer.xaml.cs), just triggered from
        // a menu item instead of a drop event. src is the raw absolute filesystem path, unmodified —
        // that's what the original sent too, not a file:// URI or data URI.
        private async void InsertImageMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            foreach (var ext in FileTypeHelper.Image) picker.FileTypeFilter.Add(ext);
            var pickedFile = await picker.PickSingleFileAsync();
            if (pickedFile == null) return;
            PostMessage("InsertImage", new { src = pickedFile.Path });
            Log($"InsertImage: {pickedFile.Path}");
        }

        // --- Settings dialog ---
        // Reads/writes the real SettingsViewModel directly (no x:Bind — we don't have the
        // AppViewModel-as-DataContext infrastructure the original pages relied on). suppressSettingsEvents
        // stops LoadSettingsIntoDialog's programmatic control updates from bouncing back into the
        // ViewModel as if the user had changed them.

        private bool suppressSettingsEvents;

        // Opens the Settings page, or closes it when it's already open (the gear and Ctrl+, toggle).
        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsPageShown) HideSettingsPage();
            else ShowSettingsPage();
        }

        private void LoadSettingsIntoDialog()
        {
            suppressSettingsEvents = true;
            ThemeComboBox.SelectedIndex = settings.AppTheme switch { AppTheme.Light => 1, AppTheme.Dark => 2, _ => 0 };
            AutoSaveToggle.IsOn = settings.AutoSave;
            AnimationToggle.IsOn = settings.AnimationEnable;
            LoadAppearanceSettings();
            LoadLayoutSettings();
            LoadSectionColorSettings();
            StatusBarToggle.IsOn = settings.StatusBarOpen;
            DecorativeCardToggle.IsOn = settings.ShowDecorativeCard;
            TypewriterToggle.IsOn = settings.Typewriter;
            FocusModeToggle.IsOn = settings.FocusMode;
            SpellcheckToggle.IsOn = settings.SpellcheckEnabled;
            LoadSpellcheckLanguageSettings();
            TopmostToggle.IsOn = settings.Topmost;
            PastedImageLocationComboBox.SelectedIndex = settings.InsertClipboardImageAction == InsertImageAction.CopyToPath ? 1 : 0;
            FileStartupActionComboBox.SelectedIndex = settings.FileStartupAction switch { FileStartupAction.OpenLast => 1, _ => 0 };
            UseTabsToggle.IsOn = settings.UseTabs;
            RestoreTabsToggle.IsOn = settings.RestoreTabs;
            RestoreTabsToggle.IsEnabled = settings.UseTabs;
            FolderStartupActionComboBox.SelectedIndex = settings.FolderStartupAction switch { FolderStartupAction.OpenLast => 1, FolderStartupAction.OpenFolder => 2, _ => 0 };
            StartupOpenFolderBox.Text = settings.StartupOpenFolder;
            // No explicit StartupFolderPickerGrid.Visibility line needed here — setting SelectedIndex
            // just above already fires FolderStartupActionComboBox_SelectionChanged synchronously,
            // which sets it (that line runs unconditionally, before the suppressSettingsEvents guard,
            // specifically so this works during load too).
            FontSizeBox.Value = settings.FontSize;
            LineHeightBox.Value = settings.LineHeight;
            TabSizeBox.Value = settings.TabSize;
            LoadPageWidth();
            AboutAppNameText.Text = Config.AppName;
            AboutAppVersionText.Text = Config.AppVersion;
            CheckForUpdatesToggle.IsOn = settings.CheckForUpdates;
            // Store: the Store delivers updates. Policy: the organisation manages them. The dev build
            // keeps the panel so "Check now" can be tried.
            var updatesManaged = Config.IsStoreInstall || Config.PolicyDisablesUpdateCheck;
            UpdateSettingsPanel.Visibility = updatesManaged ? Visibility.Collapsed : Visibility.Visible;
            StoreUpdatesText.Text = Locale.GetString(Config.IsStoreInstall ? "StoreUpdatesNote" : "PolicyUpdatesNote");
            StoreUpdatesText.Visibility = updatesManaged ? Visibility.Visible : Visibility.Collapsed;
            LanguageComboBox.SelectedIndex = Math.Max(0, Array.IndexOf(new[] { "default", "en", "fr", "es", "pl" }, settings.Language));
            LanguageRestartText.Visibility = Visibility.Collapsed;
            CheckUpdatesStatusText.Text = "";
            CheckUpdatesDownloadLink.Visibility = Visibility.Collapsed;
            suppressSettingsEvents = false;
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            var tag = (ThemeComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
            settings.AppTheme = tag switch { "Light" => AppTheme.Light, "Dark" => AppTheme.Dark, _ => AppTheme.Default };
            ApplyNativeTheme();
            ApplyEditorBackground();
            PushThemeToEditor();
        }

        // Applies to our own chrome (title bar/menu/dialogs) immediately. Pushing the choice into the
        // editor's own live theme is UIViewModel territory (reactive system-theme + AppTheme tracking)
        // — still deferred, same as GetCurrentTheme's note in RegisterHandlers.
        private void ApplyNativeTheme()
        {
            var theme = settings.AppTheme switch { AppTheme.Light => ElementTheme.Light, AppTheme.Dark => ElementTheme.Dark, _ => ElementTheme.Default };
            ((FrameworkElement)Content).RequestedTheme = theme;
            UpdateThemeToggleIcon();
            ApplySectionColors(); // they're kept per theme
        }

        // View > Theme (it was a title bar toggle until the interface review): the same three choices as
        // Settings, checked to match.
        private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            settings.AppTheme = ((FrameworkElement)sender).Tag switch { "Light" => AppTheme.Light, "Dark" => AppTheme.Dark, _ => AppTheme.Default };
            ApplyNativeTheme();
            ApplyEditorBackground();
            PushThemeToEditor();
        }

        private void UpdateThemeToggleIcon()
        {
            ThemeSystemMenuItem.IsChecked = settings.AppTheme == AppTheme.Default;
            ThemeLightMenuItem.IsChecked = settings.AppTheme == AppTheme.Light;
            ThemeDarkMenuItem.IsChecked = settings.AppTheme == AppTheme.Dark;
        }

        // Reimplemented against WinUI 3's own Window.SystemBackdrop property rather than a literal
        // port of whatever manual Mica setup the original's WPF+XAML-Islands host used — WinAppSDK
        // 1.6's SystemBackdrop API (MicaBackdrop/DesktopAcrylicBackdrop) replaced the older
        // WindowsSystemDispatcherQueueHelper + MicaController compositor dance entirely, so there's no
        // controller lifecycle to manage here. Falls back to no backdrop (plain solid chrome) on
        // Windows versions that don't support Mica (Config.IsMicaSupported, build < 22000) or when the
        // Use Mica setting is off. This only affects the window's own chrome (title bar, TocPane) — see
        // ApplyEditorBackground below for making the WebView2 editor area itself show Mica through.
        private void ApplyBackdrop()
        {
            // Settings > Appearance > Window material. Mica Alt is the stronger tint Windows uses behind
            // tabbed title bars (Terminal, File Explorer).
            SystemBackdrop = !Config.IsMicaSupported ? null : settings.WindowMaterial switch
            {
                "mica" => new MicaBackdrop { Kind = MicaKind.Base },
                "micaalt" => new MicaBackdrop { Kind = MicaKind.BaseAlt },
                _ => null,
            };
        }

        // The window-level Mica backdrop above doesn't reach through WebView2 on its own — Chromium's
        // surface is opaque by default regardless of what CSS the document sets. WebView2's XAML
        // control exposes DefaultBackgroundColor for exactly this (it forwards to the underlying
        // CoreWebView2Controller); pairing it with the document's own transparent body background
        // (theme.ts, driven by BuildThemePayload's `background` field below) is what actually lets
        // Mica show through the editor content, matching the original's UseMicaEffect &&
        // UseEditorMicaEffect condition in Common.cs's GetCurrentTheme.
        private void ApplyEditorBackground()
        {
            // The page stays solid under Mica (Fluent: content sits on a solid layer), in the scheme's colour.
            EditorView.DefaultBackgroundColor = PageBackground(IsDark);
        }

        // Ported from Typedown\Utilities\Common.cs's GetCurrentTheme (used by both the original's
        // GetCurrentTheme wire handler and its live ThemeChanged push in MarkdownEditor.cs) — same
        // theme/accentColor/background shape, same colors. One deliberate departure: theme.ts (bundled
        // JS, used as-is) destructures accentColor as {r,g,b,a} but background as {R,G,B,A} — verified
        // by inspecting the actual wire payload, a plain anonymous object serialized through this
        // project's camelCase Config.EditorJsonSerializerSettings comes out {a,r,g,b} for BOTH, which
        // would leave background's rgba() built from four undefined values and silently no-op. A
        // JObject's keys pass through the serializer untouched (the naming strategy only reshapes
        // reflected POCO property names, not JToken trees already holding string keys), so background
        // is built that way here — the only way to actually match what theme.ts reads, not a guess.
        private object BuildThemePayload()
        {
            var isDarkMode = ((FrameworkElement)Content).ActualTheme == ElementTheme.Dark;
            // The brand accent, not uiSettings.GetColorValue(UIColorType.Accent) (the user's Windows
            // system accent color) — so the editor content (cursor, selection, links) matches Caret's
            // own warm-autumn palette instead of whatever color the user picked in Windows Settings.
            // From the colour scheme: in dark, the accent text colour, so links and the cursor stay readable
            // (the brand's #8F4A22 measured 2.82 : 1 on the dark page).
            var palette = ColorSchemes.Current(isDarkMode);
            var accentColor = ColorSchemes.Parse(isDarkMode ? palette.Secondary : palette.Primary);
            var bg = PageBackground(isDarkMode);
            var background = new JObject { ["R"] = bg.R, ["G"] = bg.G, ["B"] = bg.B, ["A"] = bg.A };
            return new { theme = isDarkMode ? "Dark" : "Light", accentColor, background };
        }

        private void PushThemeToEditor() => PostMessage("ThemeChanged", BuildThemePayload());

        // Reimplemented against a plain PropertyChanged subscription rather than the original's
        // Reactive Extensions Merge() chain (UIViewModel.cs / MarkdownEditor.cs) — same three triggers
        // (system theme/accent change, AppTheme setting, the two Mica settings), just without pulling
        // in an Rx observable chain for three property names. uiSettings.ColorValuesChanged fires off
        // the UI thread, so it's marshalled back via DispatcherQueue before touching Content/WebView2.
        // Only wires the system theme/accent-color half (a genuine WinRT event, unrelated to
        // SettingsViewModel). The AppTheme/UseMicaEffect/UseEditorMicaEffect half is NOT wired through
        // settings.PropertyChanged — SettingsViewModel defines its own OnPropertyChanged(name, before,
        // after) hook for the original's notifySet-driven SettingsChanged push (FontSize etc.), and
        // Fody.PropertyChanged uses a class-supplied hook like that as the sole notification path
        // instead of also raising the plain INotifyPropertyChanged event — confirmed by instrumenting
        // it: an external `settings.PropertyChanged +=` subscriber here never fired even across a
        // genuine Dark→Light change. So each of those three settings pushes the theme directly from
        // its own Toggled/SelectionChanged handler below instead, same as ApplyBackdrop already did.
        private void SetUpThemePush()
        {
            uiSettings.ColorValuesChanged += (s, e) => DispatcherQueue.TryEnqueue(() =>
            {
                // The Windows accent (or a contrast theme) changed: re-derive the colours that follow it.
                if (settings.AccentSource == "windows" && ReferenceEquals(openWindows.FirstOrDefault(), this)) ApplyAppearance(save: false);
                ApplyNativeTheme();
                ApplyEditorBackground();
                PushThemeToEditor();
            });
        }

        private void AutoSaveToggle_Toggled(object sender, RoutedEventArgs e) { if (!suppressSettingsEvents) settings.AutoSave = AutoSaveToggle.IsOn; }


        private void AnimationToggle_Toggled(object sender, RoutedEventArgs e) { if (!suppressSettingsEvents) settings.AnimationEnable = AnimationToggle.IsOn; }

        private void SpellcheckToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            settings.SpellcheckEnabled = SpellcheckToggle.IsOn;
            ApplySpellcheckSetting();
        }

        private void TopmostToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            settings.Topmost = TopmostToggle.IsOn;
            ApplyTopmost();
        }

        private void ApplyTopmost()
        {
            if (AppWindow?.Presenter is OverlappedPresenter presenter) presenter.IsAlwaysOnTop = settings.Topmost;
        }

        // --- Update notice ---
        // New since the fork (see Services/UpdateService.cs). The automatic check runs once per process
        // (the first window), at most once a day, and only in installed builds: an unpackaged dev
        // build's assembly version (1.0.0.0) isn't the release version, so it would always look out of
        // date. "Check now" in Settings works everywhere.
        private static bool updateCheckStarted;
        private UpdateService.ReleaseInfo availableUpdate;

        private async Task CheckForUpdatesOnStartupAsync()
        {
            if (updateCheckStarted || !Config.UpdateCheckAvailable || !settings.CheckForUpdates) return;
            updateCheckStarted = true;
            if (settings.LastUpdateCheck is DateTime last && DateTime.UtcNow - last.ToUniversalTime() < TimeSpan.FromHours(20)) return;
            // Out of the way of startup: the editor and the document load first.
            await Task.Delay(TimeSpan.FromSeconds(5));
            if (!settings.CheckForUpdates) { updateCheckStarted = false; return; } // turned off meanwhile
            var release = await UpdateService.GetLatestReleaseAsync();
            Log($"UpdateCheck: latest={release?.DisplayVersion ?? "(unavailable)"}, running={Config.AppVersion}");
            if (release == null) return;
            settings.LastUpdateCheck = DateTime.UtcNow;
            if (UpdateService.IsNewerThanRunning(release) && release.DisplayVersion != settings.SkippedUpdateVersion)
                ShowUpdateNotice(release);
        }

        private void ShowUpdateNotice(UpdateService.ReleaseInfo release)
        {
            availableUpdate = release;
            UpdateInfoBar.Title = Locale.Format("UpdateAvailableTitle", release.DisplayVersion);
            UpdateInfoBar.Message = Locale.Format("UpdateRunningVersion", UpdateService.ToDisplay(Config.AppVersionNumber));
            UpdateInfoBar.IsOpen = true;
        }

        private async void UpdateDownload_Click(object sender, RoutedEventArgs e)
        {
            var url = availableUpdate?.PageUrl ?? UpdateService.ReleasesPageUrl;
            await Launcher.LaunchUriAsync(new Uri(url));
        }

        private void UpdateSkip_Click(object sender, RoutedEventArgs e)
        {
            if (availableUpdate != null) settings.SkippedUpdateVersion = availableUpdate.DisplayVersion;
            UpdateInfoBar.IsOpen = false;
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            settings.Language = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "default";
            LanguageRestartText.Visibility = Visibility.Visible;
        }

        private void CheckForUpdatesToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            settings.CheckForUpdates = CheckForUpdatesToggle.IsOn;
        }

        // Ignores the daily limit and "Skip this version": asking directly means wanting the answer.
        private async void CheckUpdatesNow_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdatesNowButton.IsEnabled = false;
            CheckUpdatesProgress.IsActive = true;
            CheckUpdatesDownloadLink.Visibility = Visibility.Collapsed;
            CheckUpdatesStatusText.Text = Locale.GetString("CheckingForUpdates");
            var release = await UpdateService.GetLatestReleaseAsync();
            Log($"UpdateCheck (manual): latest={release?.DisplayVersion ?? "(unavailable)"}, running={Config.AppVersion}");
            CheckUpdatesProgress.IsActive = false;
            CheckUpdatesNowButton.IsEnabled = true;
            if (release == null)
            {
                CheckUpdatesStatusText.Text = Locale.GetString("UpdateCheckFailed");
                return;
            }
            settings.LastUpdateCheck = DateTime.UtcNow;
            if (UpdateService.IsNewerThanRunning(release))
            {
                CheckUpdatesStatusText.Text = Locale.Format("UpdateAvailableStatus", release.DisplayVersion);
                CheckUpdatesDownloadLink.Content = Locale.Format("DownloadCaretVersion", release.DisplayVersion);
                CheckUpdatesDownloadLink.Visibility = Visibility.Visible;
                ShowUpdateNotice(release);
            }
            else
            {
                CheckUpdatesStatusText.Text = Locale.GetString("UpToDate");
            }
        }

        private void PastedImageLocationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            var tag = (PastedImageLocationComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
            settings.InsertClipboardImageAction = tag == "CopyToPath" ? InsertImageAction.CopyToPath : InsertImageAction.None;
        }

        private void FileStartupActionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            var tag = (FileStartupActionComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
            settings.FileStartupAction = tag == "OpenLast" ? FileStartupAction.OpenLast : FileStartupAction.None;
        }

        private void FolderStartupActionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var tag = (FolderStartupActionComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
            StartupFolderPickerGrid.Visibility = tag == "OpenFolder" ? Visibility.Visible : Visibility.Collapsed;
            if (suppressSettingsEvents) return;
            settings.FolderStartupAction = tag switch { "OpenLast" => FolderStartupAction.OpenLast, "OpenFolder" => FolderStartupAction.OpenFolder, _ => FolderStartupAction.None };
        }

        private async void StartupOpenFolderBrowse_Click(object sender, RoutedEventArgs e)
        {
            var picked = await Win32FolderPicker.PickFolderAsync(WindowNative.GetWindowHandle(this));
            if (picked == null) return;
            settings.StartupOpenFolder = picked;
            StartupOpenFolderBox.Text = picked;
        }

        private void FontSizeBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!suppressSettingsEvents && !double.IsNaN(args.NewValue)) settings.FontSize = args.NewValue; }

        private void LineHeightBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!suppressSettingsEvents && !double.IsNaN(args.NewValue)) settings.LineHeight = args.NewValue; }

        private void TabSizeBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!suppressSettingsEvents && !double.IsNaN(args.NewValue)) settings.TabSize = (int)args.NewValue; }

        private void EditorAreaWidthBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            settings.EditorAreaWidth = EditorAreaWidthBox.Text;
            PushPageWidth(); // Distraction-free keeps its own width
        }

        // --- Find & Replace ---
        // Wire contract traced from Typedown.Editor/src/components/Muya/index.tsx: SearchOpenChange
        // toggles the in-editor highlight overlay, Search sets the term + options, Find moves between
        // matches, Replace does the actual text substitution. All the matching/highlighting logic
        // lives in the web editor — this panel only sends what it's told to.

        private void FindMenuItem_Click(object sender, RoutedEventArgs e) => ShowFindReplace();

        private void ShowFindReplace()
        {
            Log("ShowFindReplace called");
            // New since the fork: SearchIsCaseSensitive/SearchIsWholeWord/SearchIsRegexp existed as
            // dormant ported settings — the checkboxes drove PushSearch directly but nothing persisted
            // their state, so Find & Replace reset to case-insensitive/no-regex every time you reopened
            // it, even within the same session. Loaded here rather than bound directly to the
            // CheckBoxes so SearchOption_Changed's existing PushSearch()-on-change behavior is
            // untouched — this only adds a read on open and a write on change.
            // Setting the first box fires SearchOption_Changed, which would save the other two boxes'
            // not-yet-restored state over their saved values; nothing is saved while restoring.
            restoringSearchOptions = true;
            CaseSensitiveCheck.IsChecked = settings.SearchIsCaseSensitive;
            WholeWordCheck.IsChecked = settings.SearchIsWholeWord;
            RegexCheck.IsChecked = settings.SearchIsRegexp;
            restoringSearchOptions = false;
            FindReplacePanel.Visibility = Visibility.Visible;
            FindTextBox.Focus(FocusState.Programmatic);
            FindTextBox.SelectAll();
            PostMessage("SearchOpenChange", new { open = 1 });
            PushSearch();
        }

        private void HideFindReplace()
        {
            FindReplacePanel.Visibility = Visibility.Collapsed;
            PostMessage("SearchOpenChange", new { open = 0 });
        }

        private void CloseFind_Click(object sender, RoutedEventArgs e) => HideFindReplace();

        private void PushSearch() => PostMessage("Search", new
        {
            value = string.IsNullOrEmpty(FindTextBox.Text) ? null : FindTextBox.Text,
            opt = new
            {
                searchIsCaseSensitive = CaseSensitiveCheck.IsChecked == true,
                searchIsWholeWord = WholeWordCheck.IsChecked == true,
                searchIsRegexp = RegexCheck.IsChecked == true,
            },
        });

        private void FindTextBox_TextChanged(object sender, TextChangedEventArgs e) => PushSearch();

        private bool restoringSearchOptions;

        private void SearchOption_Changed(object sender, RoutedEventArgs e)
        {
            if (restoringSearchOptions) return;
            settings.SearchIsCaseSensitive = CaseSensitiveCheck.IsChecked == true;
            settings.SearchIsWholeWord = WholeWordCheck.IsChecked == true;
            settings.SearchIsRegexp = RegexCheck.IsChecked == true;
            PushSearch();
        }

        private void FindTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
                PostMessage("Find", new { action = "next" });
            else if (e.Key == VirtualKey.Escape)
                HideFindReplace();
        }

        private void FindNext_Click(object sender, RoutedEventArgs e) => PostMessage("Find", new { action = "next" });

        private void FindPrev_Click(object sender, RoutedEventArgs e) => PostMessage("Find", new { action = "prev" });

        private void Replace_Click(object sender, RoutedEventArgs e) => PushReplace(true);

        private void ReplaceAll_Click(object sender, RoutedEventArgs e) => PushReplace(false);

        private void PushReplace(bool isSingle) => PostMessage("Replace", new
        {
            value = ReplaceTextBox.Text,
            opt = new
            {
                isSingle,
                searchIsCaseSensitive = CaseSensitiveCheck.IsChecked == true,
                searchIsWholeWord = WholeWordCheck.IsChecked == true,
                searchIsRegexp = RegexCheck.IsChecked == true,
            },
        });

        // --- Quick Open ("Go to File") ---
        // New since the fork, not in the original at all: a VS Code-style Ctrl+K file switcher. The
        // candidate list (quickOpenAllFiles) is rebuilt each time the panel opens rather than kept
        // live — cheap enough for a project-sized folder, and avoids having to keep a second index in
        // sync with the FileSystemWatcher-driven folder tree (ExplorerItem) for something this
        // short-lived. Falls back to Recent Files when no folder is open, same as the sidebar's other
        // panels do when their own data source is empty.
        private List<string> quickOpenAllFiles = new();

        // Bumped on every open/close so a scan whose await outlives its own invocation (a second
        // Ctrl+K while one scan is still running, or the panel getting hidden mid-scan) can tell it's
        // stale and back off instead of clobbering a newer scan's results or reopening a panel that
        // was just closed.
        private int quickOpenGeneration;

        private void QuickOpenButton_Click(object sender, RoutedEventArgs e) => ShowQuickOpen();

        private void QuickOpenMenuItem_Click(object sender, RoutedEventArgs e) => ShowQuickOpen();

        private async void ShowQuickOpen()
        {
            Log("ShowQuickOpen called");
            var generation = ++quickOpenGeneration;
            // Shows and focuses the panel before the scan below finishes, not after — a folder big
            // enough for CollectMarkdownFiles to take a noticeable moment previously left the panel
            // invisible and untouchable for that whole time. Starts empty and repopulates once the
            // scan resolves, filtered by whatever the user already typed in the meantime.
            quickOpenAllFiles = new List<string>();
            QuickOpenTextBox.Text = "";
            UpdateQuickOpenResults("");
            QuickOpenPanel.Visibility = Visibility.Visible;
            QuickOpenTextBox.Focus(FocusState.Programmatic);
            QuickOpenTextBox.SelectAll();

            var root = rootExplorerItem?.FullPath;
            var files = !string.IsNullOrEmpty(root) && Directory.Exists(root)
                ? await Task.Run(() => CollectMarkdownFiles(root))
                : recentFiles.Files.Where(File.Exists).ToList();
            if (generation != quickOpenGeneration) return; // panel closed or reopened while scanning
            quickOpenAllFiles = files;
            UpdateQuickOpenResults(QuickOpenTextBox.Text);
        }

        private void HideQuickOpen()
        {
            ++quickOpenGeneration;
            QuickOpenPanel.Visibility = Visibility.Collapsed;
        }

        // Plain recursion with a per-directory try/catch, not Directory.EnumerateFiles(..., AllDirectories)
        // — that throws (and abandons the whole walk) on the first access-denied subfolder instead of
        // just skipping it. Starts from ExplorerItem.PassesFilter's own hidden/system/dotfolder/
        // node_modules exclusions (see the bin/obj comment below for where this list intentionally
        // goes further than the folder tree's).
        private static List<string> CollectMarkdownFiles(string folder, int depth = 0)
        {
            var results = new List<string>();
            if (depth > 16) return results; // guards against a pathological symlink loop
            List<FileSystemInfo> entries;
            try
            {
                // Materialized inside the try, not just the EnumerateFileSystemInfos() call — that
                // call itself can't fail (it's lazy), but a foreach over its result can throw mid-walk
                // (e.g. a folder that becomes inaccessible partway through), and an uncaught exception
                // here would escape the Task.Run in ShowQuickOpen's async void and could crash the app.
                entries = new DirectoryInfo(folder).EnumerateFileSystemInfos().ToList();
            }
            catch { return results; }
            foreach (var info in entries)
            {
                if (info.Attributes.HasFlag(System.IO.FileAttributes.Hidden) || info.Attributes.HasFlag(System.IO.FileAttributes.System)) continue;
                // node_modules matches PassesFilter's own exclusion; bin/obj is Quick Open's own
                // addition on top of that — FileTypeHelper.Markdown includes .txt (ported verbatim
                // from the original, same set the folder tree itself uses), which otherwise buries
                // real notes under build-artifact LICENSE.txt/FileListAbsolute.txt noise whenever a
                // dev repo (like this one) is the opened folder. The folder tree still shows them
                // (unchanged, out of scope here) — this only trims Quick Open's own candidate list.
                if (info.Name.StartsWith(".") || info.Name == "node_modules" || info.Name == "bin" || info.Name == "obj") continue;
                if (info.Attributes.HasFlag(System.IO.FileAttributes.Directory))
                    results.AddRange(CollectMarkdownFiles(info.FullName, depth + 1));
                else if (FileTypeHelper.IsMarkdownFile(info.Name))
                    results.Add(info.FullName);
            }
            return results;
        }

        private void UpdateQuickOpenResults(string filter)
        {
            IEnumerable<string> matches = quickOpenAllFiles;
            if (!string.IsNullOrWhiteSpace(filter))
                matches = quickOpenAllFiles.Where(p => Path.GetFileName(p).Contains(filter, StringComparison.OrdinalIgnoreCase));
            QuickOpenListView.ItemsSource = matches.OrderBy(Path.GetFileName).Take(50).Select(p => new NavFileEntry(p)).ToList();
            if (QuickOpenListView.Items.Count > 0)
                QuickOpenListView.SelectedIndex = 0;
        }

        private void QuickOpenTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateQuickOpenResults(QuickOpenTextBox.Text);

        private async void QuickOpenTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Escape:
                    HideQuickOpen();
                    e.Handled = true;
                    break;
                case VirtualKey.Down:
                    MoveQuickOpenSelection(1);
                    e.Handled = true;
                    break;
                case VirtualKey.Up:
                    MoveQuickOpenSelection(-1);
                    e.Handled = true;
                    break;
                case VirtualKey.Enter:
                    e.Handled = true;
                    if (QuickOpenListView.SelectedItem is NavFileEntry entry) await OpenQuickOpenEntry(entry);
                    break;
            }
        }

        private void MoveQuickOpenSelection(int delta)
        {
            var count = QuickOpenListView.Items.Count;
            if (count == 0) return;
            QuickOpenListView.SelectedIndex = (QuickOpenListView.SelectedIndex + delta + count) % count;
            QuickOpenListView.ScrollIntoView(QuickOpenListView.SelectedItem);
        }

        private async void QuickOpenListView_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is NavFileEntry entry) await OpenQuickOpenEntry(entry);
        }

        // Mirrors OpenFolderTreeFile's open sequence (below, in the folder tree region) exactly, just
        // starting from a raw path instead of an ExplorerItem.
        private async Task OpenQuickOpenEntry(NavFileEntry entry)
        {
            HideQuickOpen();
            await OpenDocument(entry.FullPath, "QuickOpen");
        }

        // --- Table of contents pane ---
        // Traced from EditorViewModel.cs (Toc built from ContentState.Toc on every StateChange) and
        // JumpBySlug (PostMessage("ScrollTo", { slug })), confirmed against the ScrollTo listener in
        // Typedown.Editor/src/components/Muya/index.tsx. StateChange always carries the full
        // reconstructed state by the time it reaches EventCenter — Transport already resolves the
        // diff/no-diff distinction before emitting, so there's no partial-JSON handling needed here.

        private void UpdateToc(JToken args)
        {
            try
            {
                var toc = args["state"]?["toc"];
                if (toc == null) return;
                var entries = toc.Select(item => new TocEntry
                {
                    Content = item["content"]?.ToString(),
                    Slug = item["slug"]?.ToString(),
                    Lvl = item["lvl"]?.ToObject<int>() ?? 1,
                }).ToList();
                SyncToc(entries);
                UpdateOutlineHeader();
            }
            catch (Exception ex)
            {
                Log($"UpdateToc EXCEPTION: {ex}");
            }
        }

        // Every keystroke brings the whole state, so the list is brought up to date row by row: typing in
        // a paragraph changes nothing here, and clearing and refilling it made the outline flash.
        private void SyncToc(List<TocEntry> entries)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (i >= tocEntries.Count) tocEntries.Add(entries[i]);
                else if (tocEntries[i].Content != entries[i].Content || tocEntries[i].Slug != entries[i].Slug || tocEntries[i].Lvl != entries[i].Lvl)
                    tocEntries[i] = entries[i];
            }
            while (tocEntries.Count > entries.Count) tocEntries.RemoveAt(tocEntries.Count - 1);
        }

        // Shown only when the document has headings; collapsing hides the list (and is remembered).
        private void UpdateOutlineHeader()
        {
            OutlineHeaderButton.Visibility = tocEntries.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            TocListView.Visibility = tocEntries.Count > 0 && !settings.OutlineCollapsed ? Visibility.Visible : Visibility.Collapsed;
            OutlineChevron.Glyph = settings.OutlineCollapsed ? "\uE76C" : "\uE70E";
        }

        // The folder tree collapses from its heading in the same way.
        private void UpdateFolderHeader()
        {
            FolderTreeView.Visibility = settings.FolderCollapsed ? Visibility.Collapsed : Visibility.Visible;
            FolderChevron.Glyph = settings.FolderCollapsed ? "\uE76C" : "\uE70E";
        }

        private void FolderHeaderButton_Click(object sender, RoutedEventArgs e)
        {
            settings.FolderCollapsed = !settings.FolderCollapsed;
            UpdateFolderHeader();
        }

        private void OutlineHeaderButton_Click(object sender, RoutedEventArgs e)
        {
            settings.OutlineCollapsed = !settings.OutlineCollapsed;
            UpdateOutlineHeader();
        }

        private void TocListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TocEntry entry)
                PostMessage("ScrollTo", new { slug = entry.Slug });
        }

        private void TocMenuItem_Click(object sender, RoutedEventArgs e) => ApplySidebarLayout();

        // --- Status bar ---
        // lastWordCount caches the most recent StateChange payload's wordCount object so the
        // characters/words toggle button can re-render immediately on click, without waiting for
        // another edit to trigger a fresh StateChange.
        private JToken lastWordCount;

        /// <summary>
        /// Caches and displays the editor's latest word counts, leaving the display unchanged
        /// when the payload contains no word count data. Logs failures to process the payload.
        /// </summary>
        /// <param name="args">The StateChange payload containing state.wordCount.</param>
        private void UpdateWordCount(JToken args)
        {
            try
            {
                var wordCount = args["state"]?["wordCount"];
                if (wordCount == null) return;
                lastWordCount = wordCount;
                RenderWordCount();
            }
            catch (Exception ex)
            {
                Log($"UpdateWordCount EXCEPTION: {ex}");
            }
        }

        /// <summary>
        /// Displays the cached count with a singular or plural unit, or does nothing if no count is cached.
        /// </summary>
        /// <remarks>
        /// WordCountMethod uses 0 for characters and 1 for words, matching the original WPF
        /// status bar and preserving the meaning of existing Settings.json values.
        /// </remarks>
        private void RenderWordCount()
        {
            if (lastWordCount == null) return;
            var count = settings.WordCountMethod == 1
                ? lastWordCount["word"]?.ToObject<int>() ?? 0
                : lastWordCount["character"]?.ToObject<int>() ?? 0;
            // French treats 0 as singular too ("0 mot"); English and Spanish use the plural ("0 words").
            var singular = count == 1 || (count == 0 && Locale.CurrentLang == "fr");
            var key = settings.WordCountMethod == 1
                ? (singular ? "WordCountOne" : "WordCountMany")
                : (singular ? "CharacterCountOne" : "CharacterCountMany");
            StatusBarWordCountButton.Content = Locale.Format(key, count.ToString("N0"));
        }

        /// <summary>
        /// Saves the alternate character or word count mode and refreshes the cached count display.
        /// </summary>
        /// <param name="sender">The button that raised the click event.</param>
        /// <param name="e">The click event data.</param>
        private void StatusBarWordCountButton_Click(object sender, RoutedEventArgs e)
        {
            settings.WordCountMethod = settings.WordCountMethod == 1 ? 0 : 1;
            RenderWordCount();
        }

        /// <summary>
        /// Synchronizes the status bar visibility and menu check state with the saved preference.
        /// </summary>
        private void ApplyStatusBarVisibility()
        {
            var visible = settings.StatusBarOpen;
            StatusBar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            StatusBarMenuItem.IsChecked = visible;
        }

        /// <summary>
        /// Saves the status bar menu's checked state and applies the resulting visibility.
        /// </summary>
        /// <param name="sender">The menu item that raised the click event.</param>
        /// <param name="e">The click event data.</param>
        // --- View mode: View (formatted Muya editor) / Code (CodeMirror source) / Split (source +
        // live preview) ---
        // Driven by the ported SourceCode setting plus the new SplitPreview one; both are in
        // SettingsViewModel's live-push set, so the editor switches as soon as they change.
        // SplitPreview is set first so that going View→Split never shows the formatted editor with a
        // stray preview, and Split→View passes briefly through Code, never through a broken state.
        private string CurrentViewMode => !settings.SourceCode ? "view" : settings.SplitPreview ? "split" : "code";

        private void CycleViewMode() => SetViewMode(CurrentViewMode switch { "view" => "code", "code" => "split", _ => "view" });

        private void SetViewMode(string mode)
        {
            settings.SplitPreview = mode == "split";
            settings.SourceCode = mode != "view";
            UpdateViewModeUi();
            Log($"ViewMode: {mode}");
        }

        private void UpdateViewModeUi()
        {
            var mode = CurrentViewMode;
            ViewModeViewButton.IsChecked = mode == "view";
            ViewModeCodeButton.IsChecked = mode == "code";
            ViewModeSplitButton.IsChecked = mode == "split";
            ViewModeViewMenuItem.IsChecked = mode == "view";
            ViewModeCodeMenuItem.IsChecked = mode == "code";
            ViewModeSplitMenuItem.IsChecked = mode == "split";
            SpeechModeMenuItem.IsChecked = settings.SpeechMode;
            UpdateToolbarVisibility();
            ParagraphMenu.IsEnabled = mode == "view";
            FormatMenu.IsEnabled = mode == "view";
        }

        // --- Formatting (toolbar, Paragraph/Format menus, shortcuts) ---
        // Tag format: "f:<type>" → Format, "p:<type>" → UpdateParagraph (the editor's own commands, same
        // parameters the original's Format/Paragraph menus sent), "insert:<what>" → table/image dialogs
        // or InsertParagraph before/after. Only meaningful in View mode: CodeMirror (Code/Split) doesn't
        // listen for these messages.
        private void FormatCommand_Click(object sender, RoutedEventArgs e) => RunFormatCommand((string)((FrameworkElement)sender).Tag);

        private void RunFormatCommand(string tag)
        {
            if (CurrentViewMode != "view") return;
            var parts = tag.Split(':', 2);
            switch (parts[0])
            {
                case "f": PostMessage("Format", parts[1]); break;
                case "p": PostMessage("UpdateParagraph", parts[1]); break;
                case "insert" when parts[1] == "table": InsertTableMenuItem_Click(this, null); break;
                case "insert" when parts[1] == "image": InsertImageMenuItem_Click(this, null); break;
                case "insert": PostMessage("InsertParagraph", parts[1]); break;
            }
            // A toolbar/menu click moves keyboard focus out of the editor; hand it back so typing
            // continues where it was.
            EditorView.Focus(FocusState.Programmatic);
            Log($"Format: {tag}");
        }

        // Matched on the physical key (KeyboardEvent.code) so it doesn't depend on the keyboard layout.
        // HostShortcutScript never forwards Ctrl+Alt combinations: AltGr is reported as Ctrl+Alt, and on
        // layouts like Hungarian AltGr+B/V/X/F… type {, @, #, [ … — those must reach the editor as text.
        private bool TryHandleFormattingShortcut(string code, bool shift)
        {
            string tag = (code, shift) switch
            {
                ("KeyB", false) => "f:strong",
                ("KeyI", false) => "f:em",
                ("KeyU", false) => "f:u",
                ("Digit0", false) => "p:paragraph",
                ("Digit1", false) => "p:heading 1",
                ("Digit2", false) => "p:heading 2",
                ("Digit3", false) => "p:heading 3",
                ("Digit4", false) => "p:heading 4",
                ("Digit5", false) => "p:heading 5",
                ("Digit6", false) => "p:heading 6",
                ("KeyK", true) => "p:pre",
                ("KeyQ", true) => "p:blockquote",
                ("KeyX", true) => "p:ul-task",
                ("KeyT", true) => "insert:table",
                _ => null,
            };
            if (tag == null) return false;
            RunFormatCommand(tag);
            return true;
        }

        // --- Editor popups ---
        // The editor asks the host to draw these (the original rendered them as native flyouts in
        // Typedown.Core's FloatViewModel + Controls/FloatControls); the host half was never ported, so
        // clicking the ⠿ block handle, an image, a table's side bars or a link's open button did
        // nothing. Each sends back the same messages the originals did (Duplicate / UpdateParagraph /
        // InsertParagraph / DeleteParagraph / FrontMenuClosed, EditTable, ImageEditToolbarClick,
        // ReplaceImage), which the editor still listens for. OpenFormatPicker isn't handled: that
        // editor plugin isn't registered in Muya/index.tsx, so it never fires.
        private Popup editorToolTipPopup;

        private void SetUpEditorPopups()
        {
            eventCenter.GetObservable<EditorEventArgs>("OpenFrontMenu").Subscribe(x => ShowFrontMenu(x.Args));
            eventCenter.GetObservable<EditorEventArgs>("OpenTableTools").Subscribe(x => ShowTableTools(x.Args));
            eventCenter.GetObservable<EditorEventArgs>("OpenImageToolbar").Subscribe(x => ShowImageToolbar(x.Args));
            eventCenter.GetObservable<EditorEventArgs>("OpenImageSelector").Subscribe(x => ShowImageSelector(x.Args));
            eventCenter.GetObservable<EditorEventArgs>("OpenToolTip").Subscribe(x => ShowEditorToolTip(x.Args));
            eventCenter.GetObservable<EditorEventArgs>("OpenURI").Subscribe(x => OpenLink(x.Args?["uri"]?.ToString()));
        }

        // boundingClientRect from the editor is in CSS pixels relative to the WebView, which match
        // DIPs relative to EditorView at the default zoom.
        private static Windows.Foundation.Rect ReadEditorRect(JToken args)
        {
            var r = args?["boundingClientRect"];
            double Get(string name) => r?[name]?.ToObject<double>() ?? 0;
            return new Windows.Foundation.Rect(Get("left"), Get("top"), Math.Max(Get("width"), 0), Math.Max(Get("height"), 0));
        }

        private void ShowUnderEditorRect(FlyoutBase flyout, JToken args)
        {
            var rect = ReadEditorRect(args);
            flyout.ShowAt(EditorView, new FlyoutShowOptions { Position = new Windows.Foundation.Point(rect.Left, rect.Bottom) });
        }

        private static MenuFlyoutItem PopupItem(string text, Action action)
        {
            var item = new MenuFlyoutItem { Text = text };
            item.Click += (s, e) => action();
            return item;
        }

        private void ShowFrontMenu(JToken args)
        {
            var menu = new MenuFlyout();
            menu.Items.Add(PopupItem(Locale.GetString("Duplicate"), () => PostMessage("Duplicate", null)));
            var turnInto = new MenuFlyoutSubItem { Text = Locale.GetString("TurnInto") };
            foreach (var (text, type) in new[]
            {
                ("Paragraph", "paragraph"), ("Heading1", "heading 1"), ("Heading2", "heading 2"), ("Heading3", "heading 3"),
                ("Heading4", "heading 4"), ("Heading5", "heading 5"), ("Heading6", "heading 6"),
                ("NumberedList", "ol-order"), ("BulletedList", "ul-bullet"), ("TaskList", "ul-task"),
            })
                turnInto.Items.Add(PopupItem(Locale.GetString(text), () => PostMessage("UpdateParagraph", type)));
            menu.Items.Add(turnInto);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(PopupItem(Locale.GetString("InsertParagraphBefore"), () => PostMessage("InsertParagraph", "before")));
            menu.Items.Add(PopupItem(Locale.GetString("InsertParagraphAfter"), () => PostMessage("InsertParagraph", "after")));
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(PopupItem(Locale.GetString("Delete"), () => PostMessage("DeleteParagraph", null)));
            menu.Closed += (s, e) => PostMessage("FrontMenuClosed", null);
            ShowUnderEditorRect(menu, args);
        }

        private void ShowTableTools(JToken args)
        {
            // Same split as the original TableTools: the bar below the table edits columns, the bar
            // beside a row edits rows.
            var isRow = args?["tableInfo"]?["barType"]?.ToString() != "bottom";
            var menu = new MenuFlyout();
            void Add(string text, string action, string location, string target) =>
                menu.Items.Add(PopupItem(text, () => PostMessage("EditTable", new { action, location, target })));
            if (isRow)
            {
                Add(Locale.GetString("InsertRowAbove"), "insert", "previous", "row");
                Add(Locale.GetString("InsertRowBelow"), "insert", "next", "row");
                Add(Locale.GetString("RemoveRow"), "remove", "current", "row");
            }
            else
            {
                Add(Locale.GetString("InsertColumnLeft"), "insert", "left", "column");
                Add(Locale.GetString("InsertColumnRight"), "insert", "right", "column");
                Add(Locale.GetString("RemoveColumn"), "remove", "current", "column");
            }
            ShowUnderEditorRect(menu, args);
        }

        // Applying an image action re-renders the still-selected image, which makes the editor ask for
        // the toolbar again; without this the menu popped straight back up after every choice.
        private DateTime lastImageToolbarAction;

        private void ShowImageToolbar(JToken args)
        {
            if (DateTime.Now - lastImageToolbarAction < TimeSpan.FromMilliseconds(600)) return;
            var attrs = args?["attrs"];
            var menu = new MenuFlyout();
            void Send(object payload)
            {
                lastImageToolbarAction = DateTime.Now;
                PostMessage("ImageEditToolbarClick", payload);
            }
            void Add(string text, object payload) => menu.Items.Add(PopupItem(text, () => Send(payload)));
            Add(Locale.GetString("EditImageMenu"), new { type = "edit" });
            menu.Items.Add(new MenuFlyoutSeparator());
            Add(Locale.GetString("ImageInline"), new { type = "inline" });
            Add(Locale.GetString("ImageAlignLeft"), new { type = "left" });
            Add(Locale.GetString("ImageAlignCenter"), new { type = "center" });
            Add(Locale.GetString("ImageAlignRight"), new { type = "right" });
            var size = new MenuFlyoutSubItem { Text = Locale.GetString("ImageSize") };
            var currentStyle = attrs?["style"]?.ToString() ?? "";
            foreach (var zoom in new[] { "25%", "33%", "50%", "67%", "80%", "100%", "150%", "200%" })
            {
                // Ported from the original ImageToolbar.ZoomClick: replace any zoom: in the style attribute.
                var value = ImageStyle.WithZoom(currentStyle, zoom);
                // The size the picture has now carries a check mark (a picture with no zoom is at 100%).
                var item = new ToggleMenuFlyoutItem { Text = zoom, IsChecked = ImageStyle.IsZoom(currentStyle, zoom) };
                item.Click += (s, e) => Send(new { type = "updateImage", attrName = "style", attrValue = value });
                size.Items.Add(item);
            }
            menu.Items.Add(size);
            menu.Items.Add(new MenuFlyoutSeparator());
            Add(Locale.GetString("Delete"), new { type = "delete" });
            ShowUnderEditorRect(menu, args);
        }

        private async void ShowImageSelector(JToken args)
        {
            var info = args?["imageInfo"];
            var srcBox = new TextBox { Header = Locale.GetString("ImagePathOrUrl"), Text = info?["src"]?.ToString() ?? "" };
            var browse = new Button { Content = Locale.GetString("Browse"), VerticalAlignment = VerticalAlignment.Bottom };
            browse.Click += async (s, e) =>
            {
                var picker = new FileOpenPicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                foreach (var ext in FileTypeHelper.Image) picker.FileTypeFilter.Add(ext);
                if (await picker.PickSingleFileAsync() is StorageFile picked) srcBox.Text = picked.Path;
            };
            var srcRow = new Grid { ColumnSpacing = 8 };
            srcRow.ColumnDefinitions.Add(new ColumnDefinition());
            srcRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(browse, 1);
            srcRow.Children.Add(srcBox);
            srcRow.Children.Add(browse);
            var altBox = new TextBox { Header = Locale.GetString("AltText"), Text = info?["alt"]?.ToString() ?? "" };
            var titleBox = new TextBox { Header = Locale.GetString("ImageTitle"), Text = info?["title"]?.ToString() ?? "" };
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = Locale.GetString("EditImage"),
                Content = new StackPanel { Spacing = 12, MinWidth = 420, Children = { srcRow, altBox, titleBox } },
                PrimaryButtonText = Locale.GetString("OK"),
                CloseButtonText = Locale.GetString("Cancel"),
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            PostMessage("ReplaceImage", new { src = srcBox.Text.Trim(), alt = altBox.Text, title = titleBox.Text });
            Log($"ReplaceImage: {srcBox.Text.Trim()}");
        }

        // A small popup rather than a XAML ToolTip: a ToolTip registered on EditorView would also show
        // on every ordinary hover over the editor.
        private void ShowEditorToolTip(JToken args)
        {
            if (editorToolTipPopup != null) editorToolTipPopup.IsOpen = false;
            if (args?["open"]?.ToObject<bool>() != true) return;
            var key = args["tooltip"]?.ToString();
            var text = string.IsNullOrEmpty(key) ? null : Locale.GetString(key) ?? key;
            if (string.IsNullOrEmpty(text)) return;
            var rect = ReadEditorRect(args);
            var origin = EditorView.TransformToVisual(null).TransformPoint(new Windows.Foundation.Point(rect.Left, rect.Bottom + 4));
            editorToolTipPopup = new Popup
            {
                XamlRoot = Content.XamlRoot,
                HorizontalOffset = origin.X,
                VerticalOffset = origin.Y,
                IsHitTestVisible = false,
                Child = new Border
                {
                    Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 4, 8, 4),
                    Child = new TextBlock { Text = text, FontSize = 12 },
                },
            };
            editorToolTipPopup.IsOpen = true;
        }

        private void ViewModeButton_Click(object sender, RoutedEventArgs e) => SetViewMode((string)((FrameworkElement)sender).Tag);

        private void ViewModeMenuItem_Click(object sender, RoutedEventArgs e) => SetViewMode((string)((FrameworkElement)sender).Tag);

        private void StatusBarMenuItem_Click(object sender, RoutedEventArgs e)
        {
            settings.StatusBarOpen = StatusBarMenuItem.IsChecked;
            ApplyStatusBarVisibility();
        }

        // --- Folder browsing ---
        // Ported (structurally) from Typedown.Core\Controls\SidePaneControls\Pages\FolderPage.xaml.cs
        // against the ExplorerItem tree (Models\ExplorerItem.cs) — see the XAML comment above
        // FolderTreeView for what's still cut versus the original (drag-drop, clipboard cut/copy/
        // paste). rootExplorerItem's Children is what FolderTreeView is bound to; the root item itself
        // is never shown, matching the original's WorkFolderExplorerItem.
        private readonly HashSet<string> expandedFolderPaths = new();
        private ExplorerItem rootExplorerItem;

        // Windows.Storage.Pickers.FolderPicker (the WinRT picker used everywhere else in this file)
        // throws COMException 0x80004005 (E_FAIL) reliably here — confirmed reproducible, not a
        // one-off. FileOpenPicker/FileSavePicker (this project's Open/Save/SaveAs) don't hit it; the
        // difference is StorageFolder vs. StorageFile, and StorageFolder marshalling back to an
        // unpackaged process is a documented limitation, not something a picker property fixes
        // (SuggestedStartLocation didn't help). Win32FolderPicker talks to the same native dialog
        // through the plain IFileOpenDialog COM interface instead — see its own file for why
        // System.Windows.Forms.FolderBrowserDialog isn't used either (UseWindowsForms breaks the
        // WinUI 3 XAML compiler's resource resolution in this SDK version).
        private async void OpenFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var picked = await Win32FolderPicker.PickFolderAsync(WindowNative.GetWindowHandle(this));
            if (picked == null) return;
            settings.FolderCollapsed = false; // a folder opened on purpose is shown
            OpenFolderTree(picked);
        }

        // Split out of OpenFolderMenuItem_Click so startup (FolderStartupAction.OpenLast/OpenFolder,
        // see MainWindow_Loaded) can reopen a folder the exact same way a manual File > Open Folder...
        // does, instead of duplicating this sequence.
        private void OpenFolderTree(string path)
        {
            if (rootExplorerItem == null)
            {
                rootExplorerItem = new ExplorerItem(expandedFolderPaths, DispatcherQueue);
                FolderTreeView.ItemsSource = rootExplorerItem.Children;
            }
            rootExplorerItem.FullPath = path;
            rootExplorerItem.IsExpanded = true;
            FolderHeaderText.Text = rootExplorerItem.Name;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(FolderHeaderButton, rootExplorerItem.Name);
            FolderSection.Visibility = Visibility.Visible;
            UpdateFolderHeader();
            UpdateFolderSelection();
            settings.LastOpenedFolder = path;
            Log($"OpenFolder: {path}");
        }

        // Keeps the tree's selection highlight on whatever file is currently open, including when it
        // changed via Open/Open Recent/New rather than a click inside the tree itself. Hooked onto
        // FileViewModel.FileStateChanged (see the constructor), which already fires on every load/save.
        private void UpdateFolderSelection()
        {
            if (rootExplorerItem == null) return;
            void Walk(ExplorerItem item)
            {
                item.IsSelected = item.FullPath == file.FilePath;
                foreach (var child in item.Children) Walk(child);
            }
            foreach (var child in rootExplorerItem.Children) Walk(child);
        }

        private async void FolderTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            if (args.InvokedItem is ExplorerItem item && item.Type == ExplorerItem.ExplorerItemType.File)
                await OpenFolderTreeFile(item);
        }

        private async Task OpenFolderTreeFile(ExplorerItem item)
        {
            await OpenDocument(item.FullPath, "FolderTree open");
        }

        // --- Folder tree context menu ---
        // A ContextFlyout's items inherit DataContext from whatever element it was opened on (the
        // TreeViewItem in FolderItemTemplate/FileItemTemplate) — standard WinUI 3/UWP flyout behavior,
        // and the same mechanism the original relied on for its GetExplorerItemFromMenuFlyoutItem.
        private static ExplorerItem GetContextItem(object sender) => (sender as FrameworkElement)?.DataContext as ExplorerItem;

        private async void OpenContext_Click(object sender, RoutedEventArgs e)
        {
            if (GetContextItem(sender) is ExplorerItem item && item.Type == ExplorerItem.ExplorerItemType.File)
                await OpenFolderTreeFile(item);
        }

        private async void NewFileContext_Click(object sender, RoutedEventArgs e)
        {
            if (GetContextItem(sender) is not ExplorerItem item || item.Type != ExplorerItem.ExplorerItemType.Folder) return;
            await CreateNewFile(item);
        }

        private async void NewFolderContext_Click(object sender, RoutedEventArgs e)
        {
            if (GetContextItem(sender) is not ExplorerItem item || item.Type != ExplorerItem.ExplorerItemType.Folder) return;
            await CreateNewFolder(item);
        }

        // Shared by the per-row New File/New Folder above and RootContextFlyout's own (see the
        // Folder tree clipboard & drag-drop region below) — the only difference is which ExplorerItem
        // is the target folder.
        private async Task CreateNewFile(ExplorerItem folder)
        {
            var name = await PromptForName(Locale.GetString("NewFile"), Locale.GetString("Untitled") + ".md");
            if (string.IsNullOrWhiteSpace(name)) return;
            try
            {
                var path = Path.Combine(folder.FullPath, name);
                if (File.Exists(path) || Directory.Exists(path)) throw new IOException($"'{name}' already exists.");
                File.Create(path).Dispose();
                folder.IsExpanded = true;
                Log($"NewFile: {path}");
            }
            catch (Exception ex)
            {
                await ShowErrorDialog(Locale.GetString("CouldntCreateFile"), ex.Message);
            }
        }

        private async Task CreateNewFolder(ExplorerItem folder)
        {
            var name = await PromptForName(Locale.GetString("NewFolder"), Locale.GetString("NewFolder"));
            if (string.IsNullOrWhiteSpace(name)) return;
            try
            {
                var path = Path.Combine(folder.FullPath, name);
                if (File.Exists(path) || Directory.Exists(path)) throw new IOException($"'{name}' already exists.");
                Directory.CreateDirectory(path);
                folder.IsExpanded = true;
                Log($"NewFolder: {path}");
            }
            catch (Exception ex)
            {
                await ShowErrorDialog(Locale.GetString("CouldntCreateFolder"), ex.Message);
            }
        }

        private async void RenameContext_Click(object sender, RoutedEventArgs e)
        {
            if (GetContextItem(sender) is not ExplorerItem item || item == rootExplorerItem) return;
            var newName = await PromptForName("Rename", item.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;
            try
            {
                var newPath = Path.Combine(Path.GetDirectoryName(item.FullPath), newName);
                if (File.Exists(newPath) || Directory.Exists(newPath)) throw new IOException($"'{newName}' already exists.");
                if (item.Type == ExplorerItem.ExplorerItemType.Folder)
                    Directory.Move(item.FullPath, newPath);
                else
                    File.Move(item.FullPath, newPath);
                favoritesService.RenamePath(item.FullPath, newPath); // a favorite keeps pointing at it
                if (item.Type != ExplorerItem.ExplorerItemType.Folder) AutoBackup.MoveBackup(item.FullPath, newPath);
                FollowRename(item.FullPath, newPath, item.Type == ExplorerItem.ExplorerItemType.Folder);
                // Recent files follow too, open or not, in every window's list.
                foreach (var window in openWindows.ToList())
                {
                    window.recentFiles.RenamePath(item.FullPath, newPath);
                    window.RefreshFileLists();
                }
                Log($"Rename: {item.FullPath} -> {newPath}");
            }
            catch (Exception ex)
            {
                await ShowErrorDialog(Locale.GetString("CouldntRename"), ex.Message);
            }
        }

        private async void DeleteContext_Click(object sender, RoutedEventArgs e)
        {
            if (GetContextItem(sender) is not ExplorerItem item || item == rootExplorerItem) return;
            var isFolder = item.Type == ExplorerItem.ExplorerItemType.Folder;
            var confirm = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = Locale.GetString(isFolder ? "DeleteFolderTitle" : "DeleteFileTitle"),
                Content = Locale.Format("MoveToRecycleBin", item.Name),
                PrimaryButtonText = Locale.GetString("Delete"),
                CloseButtonText = Locale.GetString("Cancel"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
            try
            {
                // Recycle Bin, not a permanent delete. Microsoft.VisualBasic.FileIO.FileSystem is the
                // simplest way to get that from a plain .NET app — despite the namespace, it's just a
                // small framework-provided assembly with no relation to VB the language.
                if (isFolder)
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(item.FullPath,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                else
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(item.FullPath,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                trashService.Record(item.FullPath);
                if (TrashPanel.Visibility == Visibility.Visible) RefreshTrashNavList();
                favoritesService.Remove(item.FullPath);
                if (FavoritesPanel.Visibility == Visibility.Visible) RefreshFavoritesNavList();
                Log($"Delete: {item.FullPath}");
            }
            catch (Exception ex)
            {
                await ShowErrorDialog(Locale.GetString("CouldntDelete"), ex.Message);
            }
        }

        private void RevealContext_Click(object sender, RoutedEventArgs e)
        {
            if (GetContextItem(sender) is not ExplorerItem item) return;
            // /select opens Explorer with the item highlighted — the simple well-known equivalent of
            // the original's Common.OpenFileLocation (Windows Shell OpenFolderAndSelectItems API) for
            // a single path.
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{item.FullPath}\"");
        }

        // --- Folder tree root context menu ---
        // The TreeView's own background flyout (right-click on empty space below the tree, not any
        // particular row) — ported from the original's separate TreeViewContextFlyout (FolderPage.xaml
        // 's OnTreeViewContextFlyoutOpening hid it the same way when there's nothing open yet).
        // Without this there'd be no way to create a file or paste into the *top level* of an opened
        // folder — every other New File/New Folder/Paste is scoped to whatever row it was opened on.

        private void RootContextFlyout_Opening(object sender, object e)
        {
            if (rootExplorerItem == null && sender is MenuFlyout flyout) flyout.Hide();
        }

        private async void NewFileRootContext_Click(object sender, RoutedEventArgs e)
        {
            if (rootExplorerItem != null) await CreateNewFile(rootExplorerItem);
        }

        private async void NewFolderRootContext_Click(object sender, RoutedEventArgs e)
        {
            if (rootExplorerItem != null) await CreateNewFolder(rootExplorerItem);
        }

        private async void PasteRootContext_Click(object sender, RoutedEventArgs e)
        {
            if (rootExplorerItem != null) await PasteIntoFolder(rootExplorerItem.FullPath);
        }

        // --- Folder tree clipboard & drag-drop ---
        // Reimplemented, not a literal port: the original (Typedown\Services\FileOperation.cs /
        // Clipboard.cs) went through System.Windows.Clipboard plus a hand-rolled "Preferred
        // DropEffect" byte blob to tell Move from Copy — the WPF-era way of doing what
        // Windows.ApplicationModel.DataTransfer.DataPackage does natively via its own
        // RequestedOperation property (Copy/Move/None), so there's no separate marker format to write
        // and parse here. The actual on-disk copy/move/delete still goes through the same shell
        // engine as the original's raw SHFileOperation calls, just via Microsoft.VisualBasic.FileIO
        // .FileSystem (already this project's convention — see DeleteContext_Click above) instead of
        // P/Invoking SHFileOperation directly: same Explorer-native conflict/overwrite prompts and
        // Recycle Bin support, less interop code. UIOption.AllDialogs is what surfaces those prompts;
        // without it a same-name conflict would throw instead of asking.
        //
        // Drag-and-drop between rows is per-TreeViewItem (CanDrag/DragStarting/AllowDrop/DragOver/
        // Drop set directly on each TreeViewItem in FolderItemTemplate/FileItemTemplate in the XAML),
        // matching the original's structure — the TreeView's own CanDragItems/AllowDrop stay off, so
        // its built-in same-list reorder never kicks in; only dropping onto a Folder row is accepted,
        // same restriction as the original's OnItemDragOver/OnFolderItemDrop.

        private async void CutContext_Click(object sender, RoutedEventArgs e)
        {
            if (GetContextItem(sender) is not ExplorerItem item || item == rootExplorerItem) return;
            await SetClipboardItem(item, DataPackageOperation.Move);
            Log($"Cut: {item.FullPath}");
        }

        private async void CopyContext_Click(object sender, RoutedEventArgs e)
        {
            if (GetContextItem(sender) is not ExplorerItem item || item == rootExplorerItem) return;
            await SetClipboardItem(item, DataPackageOperation.Copy);
            Log($"Copy: {item.FullPath}");
        }

        private async void PasteContext_Click(object sender, RoutedEventArgs e)
        {
            if (GetContextItem(sender) is not ExplorerItem item || item.Type != ExplorerItem.ExplorerItemType.Folder) return;
            await PasteIntoFolder(item.FullPath);
        }

        private void CopyAsPathContext_Click(object sender, RoutedEventArgs e)
        {
            if (GetContextItem(sender) is not ExplorerItem item) return;
            var dataPackage = new DataPackage();
            dataPackage.SetText(item.FullPath);
            Clipboard.SetContent(dataPackage);
        }

        private static async Task SetClipboardItem(ExplorerItem item, DataPackageOperation operation)
        {
            var storageItem = await GetStorageItem(item);
            var dataPackage = new DataPackage { RequestedOperation = operation };
            dataPackage.SetStorageItems(new[] { storageItem });
            Clipboard.SetContent(dataPackage);
        }

        // Copied files on the clipboard. GetStorageItemsAsync works for Caret's own cut/copy but throws
        // DV_E_FORMATETC for files copied in File Explorer (see Win32ClipboardFiles), so that case falls
        // back to reading the Win32 file list directly.
        // Also covers File Explorer's own Copy button, where the WinRT view comes back with no formats
        // at all (its data object carries AsyncFlag/elevation attributes WinRT doesn't enumerate) — so
        // the Win32 list is tried whenever WinRT doesn't offer files, not only when its read throws.
        private async Task<(string[] paths, bool isMove)> GetClipboardFiles(DataPackageView dataView)
        {
            if (Offers(dataView, StandardDataFormats.StorageItems))
            {
                try
                {
                    var items = await dataView.GetStorageItemsAsync();
                    return (items.Select(i => i.Path).ToArray(), dataView.RequestedOperation == DataPackageOperation.Move);
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    Log($"Clipboard files: WinRT read failed ({ex.HResult:X8}), using Win32 file list");
                }
            }
            if (Win32ClipboardFiles.TryGet(WindowNative.GetWindowHandle(this), out var paths, out var isMove, out var failure))
                return (paths, isMove);
            if (failure != null) Log($"Clipboard files: Win32 read failed: {failure}");
            return (Array.Empty<string>(), false);
        }

        // Clipboard.GetContent() can return an empty view for a moment right after another app wrote
        // to the clipboard while Caret was in the background (seen repeatedly: a screenshot or text
        // copied just before switching to Caret and pressing Ctrl+V pasted nothing, while the same
        // Ctrl+V seconds later worked). A few short re-reads cover that window.
        private static async Task<DataPackageView> GetClipboardContent()
        {
            var dataView = Clipboard.GetContent();
            for (var attempt = 0; attempt < 4 && FormatCount(dataView) == 0; attempt++)
            {
                await Task.Delay(150);
                dataView = Clipboard.GetContent();
            }
            return dataView;
        }

        // File Explorer's Copy puts a data object on the clipboard that WinRT can't enumerate: reading
        // AvailableFormats/Contains on it throws OutOfMemoryException (WinRT's mapping of the object's
        // E_OUTOFMEMORY, not real memory pressure). These treat that as "this format isn't offered",
        // so the paste falls through to the Win32 file list (GetClipboardFiles) instead of failing.
        private static int FormatCount(DataPackageView dataView)
        {
            try { return dataView.AvailableFormats.Count; }
            catch (Exception ex) when (ex is OutOfMemoryException or System.Runtime.InteropServices.COMException) { return -1; }
        }

        private static bool Offers(DataPackageView dataView, string format)
        {
            try { return dataView.Contains(format); }
            catch (Exception ex) when (ex is OutOfMemoryException or System.Runtime.InteropServices.COMException) { return false; }
        }

        private async Task PasteIntoFolder(string targetFolder)
        {
            try
            {
                var dataView = await GetClipboardContent();
                var (paths, isMove) = await GetClipboardFiles(dataView);
                if (paths.Length == 0) return;
                foreach (var path in paths)
                    CopyOrMove(path, Path.Combine(targetFolder, Path.GetFileName(path.TrimEnd('\\'))), Directory.Exists(path), isMove);
                // Matches Explorer's own cut/paste behavior: a successful move consumes the clipboard,
                // so a second Ctrl+V doesn't silently try to move the same (now-gone) source again.
                if (isMove) Clipboard.Clear();
                Log($"Paste: {paths.Length} item(s) into {targetFolder}");
            }
            catch (Exception ex)
            {
                await ShowErrorDialog(Locale.GetString("CouldntPaste"), ex.Message);
            }
        }

        private static async Task<IStorageItem> GetStorageItem(ExplorerItem item)
        {
            if (item.Type == ExplorerItem.ExplorerItemType.Folder)
                return await StorageFolder.GetFolderFromPathAsync(item.FullPath);
            return await StorageFile.GetFileFromPathAsync(item.FullPath);
        }

        // Shared by Paste (above) and Drop (below) — same shell-backed copy/move either way,
        // regardless of whether the item came from this app's own clipboard or a live drag.
        private static void CopyOrMove(string sourcePath, string destPath, bool isFolder, bool isMove)
        {
            if (isMove)
            {
                if (isFolder) Microsoft.VisualBasic.FileIO.FileSystem.MoveDirectory(sourcePath, destPath, Microsoft.VisualBasic.FileIO.UIOption.AllDialogs);
                else Microsoft.VisualBasic.FileIO.FileSystem.MoveFile(sourcePath, destPath, Microsoft.VisualBasic.FileIO.UIOption.AllDialogs);
            }
            else
            {
                if (isFolder) Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(sourcePath, destPath, Microsoft.VisualBasic.FileIO.UIOption.AllDialogs);
                else Microsoft.VisualBasic.FileIO.FileSystem.CopyFile(sourcePath, destPath, Microsoft.VisualBasic.FileIO.UIOption.AllDialogs);
            }
        }

        private async void FolderTreeItem_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            if ((sender as FrameworkElement)?.DataContext is not ExplorerItem item) return;
            var deferral = args.GetDeferral();
            try
            {
                var storageItem = await GetStorageItem(item);
                args.Data.SetStorageItems(new[] { storageItem });
                args.Data.RequestedOperation = DataPackageOperation.Move;
            }
            catch (Exception ex)
            {
                Log($"DragStarting EXCEPTION: {ex}");
                args.Cancel = true;
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void FolderTreeItem_DragOver(object sender, DragEventArgs e)
        {
            // Only a Folder row accepts a drop — dropping a file onto another file (or a folder onto
            // itself/an unrelated file row) isn't a meaningful "move into", same restriction as the
            // original's OnItemDragOver.
            if ((sender as FrameworkElement)?.DataContext is ExplorerItem target &&
                target.Type == ExplorerItem.ExplorerItemType.Folder &&
                e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Move;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }

        private async void FolderTreeItem_Drop(object sender, DragEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ExplorerItem target || target.Type != ExplorerItem.ExplorerItemType.Folder) return;
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            try
            {
                var items = await e.DataView.GetStorageItemsAsync();
                foreach (var storageItem in items)
                    CopyOrMove(storageItem.Path, Path.Combine(target.FullPath, storageItem.Name), storageItem.IsOfType(StorageItemTypes.Folder), isMove: true);
                Log($"Drop: moved {items.Count} item(s) into {target.FullPath}");
            }
            catch (Exception ex)
            {
                Log($"Drop EXCEPTION: {ex}");
                await ShowErrorDialog(Locale.GetString("CouldntMove"), ex.Message);
            }
        }

        private async Task<string> PromptForName(string title, string startingText)
        {
            TextInputDialog.Title = title;
            TextInputDialog.XamlRoot = Content.XamlRoot;
            TextInputBox.Text = startingText;
            TextInputBox.SelectAll();
            var result = await TextInputDialog.ShowAsync();
            return result == ContentDialogResult.Primary ? TextInputBox.Text.Trim() : null;
        }

        private async Task ShowErrorDialog(string title, string message)
        {
            var dialog = new ContentDialog { XamlRoot = Content.XamlRoot, Title = title, Content = message, CloseButtonText = Locale.GetString("OK") };
            await dialog.ShowAsync();
        }
    }
}
