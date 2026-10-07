using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Typedown.WinUI.Models;
using Typedown.WinUI.Services;
using Typedown.WinUI.Services.Conversion;
using Typedown.WinUI.Utilities;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Typedown.WinUI
{
    // New since the fork: the Convert to Markdown page. Word, Excel, PowerPoint, PDF and CSV files —
    // picked, dropped, or a whole folder — are converted in-process (Services/Conversion) and written
    // as .md files, each with its size and an estimate of how many AI tokens it takes. Nothing is ever
    // overwritten: an existing "report.md" makes the new one "report (2).md".
    public sealed partial class MainWindow
    {
        private readonly ObservableCollection<ConversionItem> conversions = new();
        private bool convertPageReady;
        private bool convertOptionsUpdating;
        private bool converting;

        private static readonly string[] LegacyOfficeExtensions = { ".doc", ".xls", ".ppt", ".dot", ".xlt", ".pot" };
        private static readonly string[] EmailExtensions = { ".msg", ".eml" };

        private void SetConvertPageVisible(bool visible)
        {
            if (visible && !convertPageReady) InitializeConvertPage();
            ConvertPage.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        // File Explorer's "Convert to Markdown" (Services/ExplorerCommandServer.cs): the files and folders
        // are converted on the Convert page of the window in use, as if dropped there.
        public static void ConvertFromShell(IReadOnlyList<string> paths)
        {
            var target = lastActiveWindow ?? openWindows.LastOrDefault();
            if (target == null)
            {
                Program.StartupConvertPaths = paths.ToList();
                new MainWindow().Activate();
                return;
            }
            target.BringToFront();
            target.ShowConvertPage("Convert");
            _ = target.ConvertPathsAsync(paths);
        }

        private void InitializeConvertPage()
        {
            convertPageReady = true;
            ConvertResultsList.ItemsSource = conversions;
            convertOptionsUpdating = true;
            ConvertImagesToggle.IsOn = settings.ConvertExtractImages;
            ConvertRedactToggle.IsOn = settings.ConvertEmailRedact;
            ConvertExplorerMenuToggle.IsOn = settings.ExplorerMenu;
            // An administrator turned it off: the switch says so and can't turn it back on.
            if (Config.PolicyDisablesExplorerMenu)
            {
                ConvertExplorerMenuToggle.IsOn = false;
                ConvertExplorerMenuToggle.IsEnabled = false;
            }
            UpdateConvertOutputChoice();
            convertOptionsUpdating = false;
        }

        // Opening a note (from the page's Open button, the folder tree, Recent…) returns to the editor.
        private void SetUpConvertPage() =>
            eventCenter.GetObservable<EditorEventArgs>("FileLoaded").Subscribe(_ =>
            {
                // The editor's own first load (the note the window starts on) is not the user opening one.
                if (convertPageHeldAtStartup)
                {
                    convertPageHeldAtStartup = false;
                    return;
                }
                if (ConvertPage.Visibility == Visibility.Visible) CloseConvertPage();
            });

        // A window started by File Explorer's "Convert to Markdown" opens on the Convert page and keeps it
        // through the editor's first FileLoaded.
        private bool convertPageHeldAtStartup;

        private void CloseConvertPage()
        {
            SetConvertPageVisible(false);
            if (SelectedNavTag is "Convert" or "Emails") SelectNav("Home");
            // Back to writing. Without this, focus falls to the next control (the status bar's word
            // count button), which also pops up its tooltip.
            EditorView.Focus(FocusState.Programmatic);
        }

        private void HomeConvertButton_Click(object sender, RoutedEventArgs e) => ShowConvertPage("Convert");

        // The Home card and File menu entry for emails go straight to picking them.
        private async void HomeEmailsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowConvertPage("Emails");
            await ChooseEmailsAsync();
        }

        private void ShowConvertPage(string tag)
        {
            if (!SelectNav(tag)) SetConvertPageVisible(true);
        }

        private void ConvertBack_Click(object sender, RoutedEventArgs e) => CloseConvertPage();

        // --- Options ---

        private void UpdateConvertOutputChoice()
        {
            var folder = settings.ConvertOutputFolder;
            var useFolder = !string.IsNullOrEmpty(folder) && Directory.Exists(folder);
            ConvertOutputFolderItem.Content = useFolder ? Locale.Format("ConvertSaveToFolderPath", folder) : Locale.GetString("ConvertSaveToFolder");
            ConvertOutputComboBox.SelectedIndex = useFolder ? 1 : 0;
        }

        private async void ConvertOutputComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (convertOptionsUpdating) return;
            if (ConvertOutputComboBox.SelectedIndex == 0)
            {
                settings.ConvertOutputFolder = "";
                return;
            }
            var picked = await Win32FolderPicker.PickFolderAsync(WindowNative.GetWindowHandle(this));
            if (!string.IsNullOrEmpty(picked)) settings.ConvertOutputFolder = picked;
            convertOptionsUpdating = true;
            UpdateConvertOutputChoice();
            convertOptionsUpdating = false;
        }

        private void ConvertImagesToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!convertOptionsUpdating) settings.ConvertExtractImages = ConvertImagesToggle.IsOn;
        }

        private void ConvertExplorerMenuToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!convertOptionsUpdating) settings.ExplorerMenu = ConvertExplorerMenuToggle.IsOn;
        }

        private void ConvertRedactToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!convertOptionsUpdating) settings.ConvertEmailRedact = ConvertRedactToggle.IsOn;
        }

        // --- Input ---

        private async void ConvertChooseFiles_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            foreach (var ext in DocumentConverter.SupportedExtensions.Concat(LegacyOfficeExtensions)) picker.FileTypeFilter.Add(ext);
            var files = await picker.PickMultipleFilesAsync();
            if (files?.Count > 0) await ConvertPathsAsync(files.Select(f => f.Path));
        }

        // Outlook mail: drag messages from Outlook to a folder (or save them as .msg), then pick them here.
        private async void ConvertChooseEmails_Click(object sender, RoutedEventArgs e) => await ChooseEmailsAsync();

        private async Task ChooseEmailsAsync()
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            foreach (var ext in EmailExtensions) picker.FileTypeFilter.Add(ext);
            var files = await picker.PickMultipleFilesAsync();
            if (files?.Count > 0) await ConvertPathsAsync(files.Select(f => f.Path));
        }

        private async void ConvertChooseFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = await Win32FolderPicker.PickFolderAsync(WindowNative.GetWindowHandle(this));
            if (!string.IsNullOrEmpty(folder)) await ConvertPathsAsync(new[] { folder });
        }

        private void ConvertPage_DragOver(object sender, DragEventArgs e)
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = Locale.GetString("ConvertDropCaption");
        }

        private async void ConvertPage_Drop(object sender, DragEventArgs e)
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            var deferral = e.GetDeferral();
            List<string> paths;
            try { paths = (await e.DataView.GetStorageItemsAsync()).Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList(); }
            finally { deferral.Complete(); }
            await ConvertPathsAsync(paths);
        }

        // --- Conversion ---

        private async Task ConvertPathsAsync(IEnumerable<string> inputs)
        {
            // (source file, folder it was found under — for mirroring subfolders into an output folder)
            var jobs = new List<(string File, string Root)>();
            foreach (var input in inputs)
            {
                if (Directory.Exists(input))
                {
                    IEnumerable<string> found;
                    try
                    {
                        // On a worker thread: a big dropped folder must not freeze the window
                        found = await Task.Run(() => Directory.EnumerateFiles(input, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
                            .Where(f => DocumentConverter.IsSupported(f) || LegacyOfficeExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                            .Where(f => !Path.GetFileName(f).StartsWith("~$"))
                            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList());
                    }
                    catch (Exception ex)
                    {
                        Log($"Convert: couldn't list {input}: {ex.Message}");
                        continue;
                    }
                    jobs.AddRange(found.Select(f => (f, input)));
                }
                else if (File.Exists(input)) jobs.Add((input, Path.GetDirectoryName(input)));
            }
            if (jobs.Count == 0) return;

            // The newest batch goes on top, in the order the files were given.
            var items = jobs.Select((j, index) =>
            {
                var (badge, color) = ConversionItem.BadgeFor(Path.GetExtension(j.File));
                var item = new ConversionItem
                {
                    SourcePath = j.File,
                    Name = Path.GetFileName(j.File),
                    Badge = badge,
                    BadgeBrush = new SolidColorBrush(color),
                    Detail = Locale.GetString("ConvertWaiting"),
                };
                conversions.Insert(index, item);
                return (Item: item, j.Root);
            }).ToList();
            ConvertSummaryPanel.Visibility = Visibility.Visible;
            UpdateConvertSummary();

            // One batch at a time; later drops queue behind it.
            while (converting) await Task.Delay(200);
            converting = true;
            try
            {
                foreach (var (item, root) in items)
                {
                    await ConvertOneAsync(item, root);
                    UpdateConvertSummary();
                }
            }
            finally { converting = false; }
        }

        private async Task ConvertOneAsync(ConversionItem item, string root)
        {
            var source = item.SourcePath;
            var extension = Path.GetExtension(source).ToLowerInvariant();
            if (LegacyOfficeExtensions.Contains(extension))
            {
                Fail(item, Locale.GetString("ConvertLegacyFormat"));
                return;
            }
            if (!DocumentConverter.IsSupported(source))
            {
                Fail(item, Locale.GetString("ConvertUnsupported"));
                return;
            }

            item.IsConverting = true;
            item.Detail = Locale.GetString("ConvertConverting");
            try
            {
                item.SourceBytes = new FileInfo(source).Length;
                var outputPath = OutputPathFor(source, root);
                var baseName = Path.GetFileNameWithoutExtension(outputPath);
                var options = new ConversionOptions
                {
                    SlideHeadingFormat = Locale.GetString("ConvertSlideHeading"),
                    SlideHeadingUntitledFormat = Locale.GetString("ConvertSlideHeadingUntitled"),
                    NotesLabel = Locale.GetString("ConvertNotesLabel"),
                    EmailRedact = settings.ConvertEmailRedact,
                };
                if (settings.ConvertExtractImages)
                {
                    options.ImageDirectory = Path.Combine(Path.GetDirectoryName(outputPath), baseName + "_images");
                    options.ImageLinkPrefix = baseName + "_images";
                }
                var result = await DocumentConverter.ConvertAsync(source, options);
                if (string.IsNullOrWhiteSpace(result.Markdown))
                {
                    Fail(item, result.Warnings.Contains(ConversionWarning.PdfHasNoText)
                        ? Locale.GetString("ConvertPdfNoText")
                        : Locale.GetString("ConvertNoContent"));
                    return;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                await File.WriteAllTextAsync(outputPath, result.Markdown, new UTF8Encoding(false));

                item.OutputPath = outputPath;
                item.Markdown = result.Markdown;
                item.MarkdownBytes = Encoding.UTF8.GetByteCount(result.Markdown);
                item.Tokens = DocumentConverter.EstimateTokens(result.Markdown);
                item.Succeeded = true;
                var detail = Locale.Format("ConvertResultDetail", FormatSize(item.SourceBytes), FormatSize(item.MarkdownBytes), item.Tokens.ToString("N0"));
                // Rounded down: 99.6% must not read "100% smaller".
                var saved = Math.Floor(100.0 * (item.SourceBytes - item.MarkdownBytes) / item.SourceBytes);
                if (saved >= 1)
                    detail += " · " + Locale.Format("ConvertSmaller", saved);
                if (EmailExtensions.Contains(extension) && options.EmailRedact)
                    detail += " · " + Locale.GetString("ConvertPersonalDataMasked");
                if (result.Warnings.Contains(ConversionWarning.SkippedUnsupportedImages))
                    detail += " · " + Locale.GetString("ConvertSkippedImages");
                item.Detail = detail;
                item.ActionsVisibility = Visibility.Visible;
                Log($"Convert: {source} -> {outputPath} ({item.SourceBytes} -> {item.MarkdownBytes} bytes, ~{item.Tokens} tokens)");
            }
            catch (Exception ex)
            {
                Log($"Convert: failed {source}: {ex}");
                Fail(item, FriendlyConversionError(ex));
            }
            finally
            {
                item.IsConverting = false;
            }
        }

        private static void Fail(ConversionItem item, string message)
        {
            item.Failed = true;
            item.IsConverting = false;
            item.Detail = message;
        }

        private static string FriendlyConversionError(Exception ex) => ex switch
        {
            UnauthorizedAccessException => Locale.GetString("ConvertNoWriteAccess"),
            FileNotFoundException or DirectoryNotFoundException => Locale.GetString("ConvertFileMissing"),
            IOException io when io.HResult == unchecked((int)0x80070020) => Locale.GetString("ConvertFileLocked"),
            DocumentFormat.OpenXml.Packaging.OpenXmlPackageException or System.IO.InvalidDataException or FileFormatException
                => Locale.GetString("ConvertDamagedOrProtected"),
            _ when ex.GetType().FullName?.StartsWith("UglyToad.PdfPig") == true => Locale.GetString("ConvertDamagedOrProtected"),
            _ when ex.GetType().FullName is string name && (name.StartsWith("OpenMcdf") || name.StartsWith("MimeKit")) => Locale.GetString("ConvertDamagedOrProtected"),
            _ => Locale.Format("ConvertFailed", ex.Message),
        };

        // Next to the original, or in the chosen folder (mirroring subfolders of a dropped folder).
        // Never overwrites: "report.md" → "report (2).md".
        private string OutputPathFor(string source, string root)
        {
            var folder = settings.ConvertOutputFolder;
            string directory;
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                var relative = Path.GetRelativePath(root, Path.GetDirectoryName(source));
                directory = relative == "." || relative.StartsWith("..") ? folder : Path.Combine(folder, relative);
            }
            else directory = Path.GetDirectoryName(source);
            var name = Path.GetFileNameWithoutExtension(source);
            bool Taken(string candidate) => File.Exists(candidate) || Directory.Exists(Path.Combine(directory, Path.GetFileNameWithoutExtension(candidate) + "_images"));
            var path = Path.Combine(directory, name + ".md");
            if (!Taken(path)) return path;
            // "report.docx" and "report.pdf" side by side → "report.md" and "report (PDF).md".
            path = Path.Combine(directory, $"{name} ({Path.GetExtension(source).TrimStart('.').ToUpperInvariant()}).md");
            for (var n = 2; Taken(path); n++)
                path = Path.Combine(directory, $"{name} ({n}).md");
            return path;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024 * 1024)
                return (bytes / 1024.0).ToString(bytes < 10 * 1024 ? "0.#" : "N0") + " " + Locale.GetString("UnitKB");
            return (bytes / (1024.0 * 1024)).ToString("0.#") + " " + Locale.GetString("UnitMB");
        }

        private void UpdateConvertSummary()
        {
            var done = conversions.Where(c => c.Succeeded).ToList();
            var failed = conversions.Count(c => c.Failed);
            var pending = conversions.Count(c => !c.Succeeded && !c.Failed);
            ConvertSummaryTitle.Text = pending > 0
                ? Locale.Format("ConvertSummaryWorking", conversions.Count - pending, conversions.Count)
                : Locale.Format(done.Count == 1 || (done.Count == 0 && Locale.CurrentLang == "fr") ? "ConvertSummaryOne" : "ConvertSummaryMany", done.Count);
            var detail = done.Count == 0 ? "" : Locale.Format("ConvertSummaryDetail",
                FormatSize(done.Sum(c => c.SourceBytes)), FormatSize(done.Sum(c => c.MarkdownBytes)), done.Sum(c => c.Tokens).ToString("N0"));
            if (failed > 0) detail = (detail + " " + Locale.Format("ConvertSummaryFailed", failed)).Trim();
            ConvertSummaryDetail.Text = detail;
            ConvertSummaryDetail.Visibility = detail.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            ConvertCopyAllButton.IsEnabled = done.Count > 0;
        }

        // --- Results ---

        // Everything converted so far as one Markdown text — ready to paste into an AI assistant.
        private async void ConvertCopyAll_Click(object sender, RoutedEventArgs e)
        {
            var done = conversions.Where(c => c.Succeeded).Reverse().ToList();
            if (done.Count == 0) return;
            var text = done.Count == 1
                ? done[0].Markdown
                : string.Join("\n\n---\n\n", done.Select(c => $"<!-- {c.Name} -->\n\n{c.Markdown.TrimEnd()}")) + "\n";
            CopyTextToClipboard(text);
            ConvertCopyAllText.Text = Locale.GetString("ConvertCopied");
            await Task.Delay(1800);
            ConvertCopyAllText.Text = Locale.GetString("ConvertCopyAll");
        }

        private void ConvertCopyResult_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is ConversionItem item && item.Succeeded) CopyTextToClipboard(item.Markdown);
        }

        private static void CopyTextToClipboard(string text)
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
            Clipboard.Flush();
        }

        private async void ConvertOpenResult_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is ConversionItem item && File.Exists(item.OutputPath))
                await OpenRecentFile(item.OutputPath);
        }

        private void ConvertRevealResult_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is ConversionItem item && File.Exists(item.OutputPath))
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{item.OutputPath}\"");
        }

        private void ConvertClear_Click(object sender, RoutedEventArgs e)
        {
            if (converting) return;
            conversions.Clear();
            ConvertSummaryPanel.Visibility = Visibility.Collapsed;
        }
    }
}
