using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Models;
using Typedown.Universal.Services;
using Typedown.Universal.Utilities;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.ViewModels
{
    public class EditorViewModel : ObservableObject
    {
        public IServiceProvider ServiceProvider { get; }

        public FileViewModel FileViewModel => ServiceProvider.GetService<FileViewModel>();
        public FloatViewModel FloatViewModel => ServiceProvider.GetService<FloatViewModel>();
        public FormatViewModel FormatViewModel => ServiceProvider.GetService<FormatViewModel>();
        public SettingsViewModel Settings => ServiceProvider.GetService<SettingsViewModel>();
        public AppViewModel ViewModel => ServiceProvider.GetService<AppViewModel>();
        public EventCenter EventCenter => ServiceProvider.GetService<EventCenter>();
        public RemoteInvoke RemoteInvoke => ServiceProvider.GetService<RemoteInvoke>();

        public JToken Selection { get; set; }
        public JToken CodeMirrorSelection { get; set; }
        public JToken ContentState { get; set; }
        public MenuState MenuState { get; set; }
        public ParagraphState ParagraphState { get; set; }
        public ObservableCollection<ListViewItem> TocListViewItems { get; } = new();
        public ContentHistory History { get; } = new();

        public string Markdown { get; set; } = "";
        public int TocListSelectIndex { get; set; }
        public bool Selected { get; set; }
        public string SelectionText { get; set; }
        public bool TextSelected { get; set; }
        public int WordCount { get; set; }
        public int CharacterCount { get; set; }
        public bool Saved { get; set; } = true;
        public bool AutoSavedSucc { get; set; } = true;
        public bool DisplaySaved { get; set; } = true;
        public ulong FileHash { get; set; }
        public ulong CurrentHash { get; set; }
        public string SearchValue { get; set; } = null;
        public bool FirstStart { get; set; } = true;
        public bool FileLoaded { get; set; }

        public Command<Unit> Undo { get; } = new();
        public Command<Unit> Redo { get; } = new();

        public IMarkdownEditor Transport => ServiceProvider.GetService<IMarkdownEditor>();

        public IClipboard Clipboard => ServiceProvider.GetService<IClipboard>();

        public AutoBackup AutoBackup => ServiceProvider.GetService<AutoBackup>();

        public EditorViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            RemoteInvoke.Handle("GetSettings", OnGetSettings);
        }

        public object OnGetSettings(JToken arg)
        {
            if (FirstStart)
            {
                FirstStart = false;
                // await FileViewModel.LoadStartUpMarkdown();
            }
            return new
            {
                focusMode = Settings.FocusMode,
                typewriter = Settings.Typewriter,
                sourceCode = Settings.SourceCode,
                fontSize = Settings.FontSize,
                lineHeight = Settings.LineHeight,
                autoPairBracket = Settings.AutoPairBracket,
                autoPairQuote = Settings.AutoPairQuote,
                trimUnnecessaryCodeBlockEmptyLines = Settings.TrimUnnecessaryCodeBlockEmptyLines,
                preferLooseListItem = Settings.PreferLooseListItem,
                autoPairMarkdownSyntax = Settings.AutoPairMarkdownSyntax,
                editorAreaWidth = Settings.EditorAreaWidth,
                markdown = Markdown
            };
        }

        public void OnSelectionChange(JToken arg)
        {
            Selection = arg["selection"];
            MenuState = arg["menuState"].ToObject<MenuState>();
            SelectionText = arg["selectionText"].ToString();
            ParagraphState = new ParagraphState(MenuState);
            UpdateMuyaSelected();
        }

        public void UpdateMuyaSelected()
        {
            var formatViewModel = ServiceProvider.GetService<FormatViewModel>();
            TextSelected = Selection["start"]["offset"].ToString() != Selection["end"]["offset"].ToString();
            Selected = TextSelected || formatViewModel.FormatState.Image;
        }

        public async void OnMarkdownChange(string markdown)
        {
            Markdown = markdown;
            History.ContentChange(Markdown);
            CurrentHash = Common.SimpleHash(Markdown);
            if (!FileLoaded) await Task.Delay(100);
            Saved = FileHash == CurrentHash;
        }

        public void OnFileLoaded(JToken arg)
        {
            if (!FileLoaded)
            {
                FileLoaded = true;
                var newText = arg["text"].ToString();
                FileHash = Common.SimpleHash(newText);
                OnMarkdownChange(newText);
                if (FloatViewModel.SearchOpen > 0)
                    OnSearch();
            }
        }

        public void OnMarkdownChange(JToken arg)
        {
            OnMarkdownChange(arg["text"].ToString());
        }

        public void OnCursorChange(JToken arg)
        {
            History.CursorChange(arg["cursor"]);
        }

        public void OnCodeMirrorSelectionChange(JToken arg)
        {
            CodeMirrorSelection = arg["cursor"];
            var anchorLine = CodeMirrorSelection["anchor"]["line"].ToObject<int>();
            var headLine = CodeMirrorSelection["head"]["line"].ToObject<int>();
            var anchorCh = CodeMirrorSelection["anchor"]["ch"].ToObject<int>();
            var headCh = CodeMirrorSelection["head"]["ch"].ToObject<int>();
            TextSelected = Selected = anchorLine != headLine || anchorCh != headCh;
            SelectionText = arg["selectionText"].ToString();
        }

        public void OnStateChange(JToken arg)
        {
            ContentState = arg["state"];
            WordCount = ContentState["wordCount"]["word"].ToObject<int>();
            CharacterCount = ContentState["wordCount"]["character"].ToObject<int>();

            var toc = ContentState["toc"];
            var cur = ContentState["cur"];
            Toc2ListViewItems.Convert(this, toc, TocListViewItems);
            if (cur != null && cur.HasValues)
            {
                var arr = toc.Select(x => x["slug"].ToString()).ToList();
                var curslug = cur["slug"].ToString();
                TocListSelectIndex = arr.IndexOf(arr.Where(x => x == curslug).First());
            }
            else
            {
                TocListSelectIndex = -1;
            }

        }

        public void OnSearch()
        {
            if (string.IsNullOrEmpty(SearchValue))
                SearchValue = null;
            Transport?.PostMessage("Search", new
            {
                value = SearchValue,
                opt = new
                {
                    isCaseSensitive = Settings.SearchIsCaseSensitive,
                    isWholeWord = Settings.SearchIsWholeWord,
                    isRegexp = Settings.SearchIsRegexp,
                    selection = Settings.SourceCode ? CodeMirrorSelection : Selection
                }
            });
        }

        public void UndoFun()
        {
            var state = History.Undo();
            if (state == null)
            {
                return;
            }
            OnMarkdownChange(state.Text);
            Transport?.PostMessage("SetMarkdown", new
            {
                text = state.Text,
                cursor = state.Cursor
            });
        }

        public void RedoFun()
        {
            var state = History.Redo();
            if (state == null)
            {
                return;
            }
            OnMarkdownChange(state.Text);
            Transport?.PostMessage("SetMarkdown", new
            {
                text = state.Text,
                cursor = state.Cursor
            });
        }


        public Command<string> Cut { get; } = new();

        private void CutFun(string type)
        {
            Transport?.PostMessage("Cut", new { type });
        }

        public Command<string> Paste { get; } = new();

        private void PasteFun(string type)
        {
            string text = null;
            string html = null;
            if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
            {
                text = Clipboard.GetText(TextDataFormat.UnicodeText);
            }
            if (Clipboard.ContainsText(TextDataFormat.Html))
            {
                html = Clipboard.GetText(TextDataFormat.Html);
            }
            if (text != null || html != null)
            {
                Transport?.PostMessage("Paste", new { type, text, html });
                return;
            }
            var files = Clipboard.GetFileDropList();
            if (files != null && files.Count == 1)
            {
                if (FileExtension.Image.Where(files[0].ToLower().EndsWith).Any())
                {
                    Transport?.PostMessage("InsertImage", new { src = files[0] });
                }
                return;
            }
        }

        public Command<string> Copy { get; } = new();

        private void CopyFun(string type)
        {
            var settings = ServiceProvider.GetService<SettingsViewModel>();
            Transport?.PostMessage("Copy", new { type });
        }

        public object OnSetClipboard(JToken arg)
        {
            var type = arg["type"].ToString();
            var data = arg["data"].ToString();

            if (type == "text/plain")
            {
                Clipboard.SetText(data, TextDataFormat.UnicodeText);
            }
            else if (type == "text/html")
            {
                Clipboard.SetText(data, TextDataFormat.Html);
            }
            return true;
        }

        public Command<Unit> DeleteSelection { get; } = new();

        private void DeleteSelectionFun()
        {
            Transport?.PostMessage("DeleteSelection", null);
        }

        public Command<Unit> SelectAll { get; } = new();

        private void SelectAllFun()
        {
            Transport?.PostMessage("SelectAll", null);
        }

        public Command<string> Find { get; } = new();

        public void FindFun(string action)
        {
            var appViewModel = ServiceProvider.GetService<AppViewModel>();
            if (appViewModel.FloatViewModel.SearchOpen == 0)
            {
                appViewModel.FloatViewModel.SearchOpen = 1;
                appViewModel.EditorViewModel.OnSearch();
            }
            else
            {
                Transport?.PostMessage("Find", new { action });
            }
        }

        private void SearchValueChanged(string searchValue)
        {
            var floatViewModel = ServiceProvider.GetService<FloatViewModel>();
            if (floatViewModel.SearchOpen > 0)
                OnSearch();
        }

        private void SavedOrAutoSavedSuccChanged(Tuple<bool, bool> _)
        {
            var fileViewModel = ServiceProvider.GetService<FileViewModel>();
            DisplaySaved = Saved || (Settings.AutoSave && fileViewModel.FilePath != null && AutoSavedSucc);
            if (Saved)
                AutoBackup.DeleteBackup(fileViewModel.FilePath);
        }

        private void Settings_AutoSaveChanged(bool autoSave)
        {
            DisplaySaved = Saved || autoSave;
        }

        public void JumpByIndex(int index)
        {
            try
            {
                if (ContentState["toc"] != null)
                {
                    Transport?.PostMessage("ScrollTo", new { slug = ContentState["toc"][index]["slug"].ToString() });
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
            }
        }
    }
}
