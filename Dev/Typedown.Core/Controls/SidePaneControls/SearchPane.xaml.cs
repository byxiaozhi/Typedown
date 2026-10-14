using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls
{
    public class SearchResultItem
    {
        public string FullPath { get; set; }
        public string FileName { get; set; }
        public string RelativePath { get; set; }
        public string Snippet { get; set; }
    }

    /// <summary>Full-text search across the Markdown files of the open folder (upstream #33).</summary>
    public sealed partial class SearchPane : UserControl, INotifyPropertyChanged
    {
        public event EventHandler Close;

        public event PropertyChangedEventHandler PropertyChanged;

        public AppViewModel ViewModel => DataContext as AppViewModel;

        public ObservableCollection<SearchResultItem> Results { get; } = new();

        public string Status { get; set; } = "";

        private CancellationTokenSource searchCts;

        private const int MaxResults = 200;
        private const long MaxFileBytes = 8 * 1024 * 1024;

        public SearchPane()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ = Dispatcher.RunIdleAsync(() => SearchTextBox.Focus(FocusState.Programmatic));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            searchCts?.Cancel();
        }

        private void OnSearchTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchTextBox.Text) && Results.Count == 0)
                Close?.Invoke(this, EventArgs.Empty);
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close?.Invoke(this, EventArgs.Empty);
        }

        private string SearchRoot
        {
            get
            {
                var file = ViewModel?.FileViewModel;
                if (file == null) return null;
                if (!string.IsNullOrEmpty(file.WorkFolder) && Directory.Exists(file.WorkFolder)) return file.WorkFolder;
                if (!string.IsNullOrEmpty(file.FilePath)) return Path.GetDirectoryName(file.FilePath);
                return null;
            }
        }

        private async void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            searchCts?.Cancel();
            var query = sender.Text?.Trim();
            Results.Clear();
            if (string.IsNullOrEmpty(query))
            {
                Status = "";
                return;
            }
            var root = SearchRoot;
            if (root == null)
            {
                Status = Locale.GetString("FolderPane.NoFolderOpen");
                return;
            }
            var cts = searchCts = new CancellationTokenSource();
            try
            {
                await Task.Delay(300, cts.Token); // debounce typing
                Status = Locale.GetString("FolderSearch.Searching");
                var progress = new Progress<SearchResultItem>(item => { if (!cts.IsCancellationRequested && Results.Count < MaxResults) Results.Add(item); });
                var (files, hits) = await Task.Run(() => SearchFolder(root, query, progress, cts.Token), cts.Token);
                if (cts.IsCancellationRequested) return;
                Status = string.Format(Locale.GetString("FolderSearch.ResultSummary"), hits, files) + (hits >= MaxResults ? " (max)" : "");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
        }

        private static (int files, int hits) SearchFolder(string root, string query, IProgress<SearchResultItem> progress, CancellationToken token)
        {
            var nameMatchLabel = Locale.GetString("FolderSearch.NameMatch");
            var files = 0;
            var hits = 0;
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0 && hits < MaxResults)
            {
                token.ThrowIfCancellationRequested();
                var dir = pending.Pop();
                IEnumerable<FileSystemInfo> entries;
                try { entries = new DirectoryInfo(dir).EnumerateFileSystemInfos(); }
                catch { continue; }
                foreach (var entry in entries)
                {
                    token.ThrowIfCancellationRequested();
                    if (entry.Attributes.HasFlag(FileAttributes.Hidden) || entry.Attributes.HasFlag(FileAttributes.System)) continue;
                    if (entry is DirectoryInfo)
                    {
                        if (!entry.Name.StartsWith(".") && entry.Name != "node_modules") pending.Push(entry.FullName);
                        continue;
                    }
                    if (!FileTypeHelper.IsEditableTextFile(entry.Name) || ((FileInfo)entry).Length > MaxFileBytes) continue;
                    files++;
                    string snippet = null;
                    try
                    {
                        foreach (var line in File.ReadLines(entry.FullName))
                        {
                            var index = line.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                            if (index < 0) continue;
                            var start = Math.Max(0, index - 40);
                            snippet = (start > 0 ? "…" : "") + line.Substring(start, Math.Min(line.Length - start, 120)).Trim();
                            break;
                        }
                    }
                    catch { continue; }
                    // A file whose name contains the query is a hit too (shown once: content snippet wins if both match).
                    if (snippet == null && entry.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        snippet = nameMatchLabel;
                    if (snippet == null) continue;
                    hits++;
                    var relative = entry.FullName.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? entry.FullName.Substring(root.Length).TrimStart('\\', '/') : entry.FullName;
                    progress.Report(new SearchResultItem { FullPath = entry.FullName, FileName = entry.Name, RelativePath = relative, Snippet = snippet });
                    if (hits >= MaxResults) break;
                }
            }
            return (files, hits);
        }

        private async void OnResultClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not SearchResultItem item || ViewModel == null) return;
            var query = SearchTextBox.Text?.Trim();
            ViewModel.FileViewModel.OpenFileCommand.Execute(item.FullPath);
            if (string.IsNullOrEmpty(query)) return;
            // Let the document load, then open the in-document find with the same query.
            var editor = ViewModel.EditorViewModel;
            await Task.Delay(400);
            editor.SearchValue = query;
            editor.FindCommand.Execute("search");
        }
    }
}
