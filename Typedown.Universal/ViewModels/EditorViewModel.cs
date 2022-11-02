using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Models;
using Typedown.Universal.Services;
using Typedown.Universal.Utilities;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.ViewModels
{
    public sealed partial class EditorViewModel : INotifyPropertyChanged
    {
        public IServiceProvider ServiceProvider { get; }

        public AppViewModel AppViewModel => ServiceProvider.GetService<AppViewModel>();
        public FileViewModel FileViewModel => ServiceProvider.GetService<FileViewModel>();
        public FloatViewModel FloatViewModel => ServiceProvider.GetService<FloatViewModel>();
        public FormatViewModel FormatViewModel => ServiceProvider.GetService<FormatViewModel>();
        public SettingsViewModel Settings => ServiceProvider.GetService<SettingsViewModel>();
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

        public Command<Unit> UndoCommand { get; } = new();
        public Command<Unit> RedoCommand { get; } = new();
        public Command<string> CutCommand { get; } = new();
        public Command<string> PasteCommand { get; } = new();
        public Command<string> CopyCommand { get; } = new();
        public Command<Unit> DeleteSelectionCommand { get; } = new();
        public Command<Unit> SelectAllCommand { get; } = new();
        public Command<string> FindCommand { get; } = new();

        public IMarkdownEditor MarkdownEditor => ServiceProvider.GetService<IMarkdownEditor>();
        public IClipboard Clipboard => ServiceProvider.GetService<IClipboard>();
        public AutoBackup AutoBackup => ServiceProvider.GetService<AutoBackup>();

        public EditorViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            EventCenter.GetObservable<EditorEventArgs>("MarkdownChange").Subscribe(x => OnMarkdownChange(x.Args));
            EventCenter.GetObservable<EditorEventArgs>("FileLoaded").Subscribe(x => OnFileLoaded(x.Args));
            EventCenter.GetObservable<EditorEventArgs>("CursorChange").Subscribe(x => OnCursorChange(x.Args));
            EventCenter.GetObservable<EditorEventArgs>("SelectionChange").Subscribe(x => OnSelectionChange(x.Args));
            EventCenter.GetObservable<EditorEventArgs>("CodeMirrorSelectionChange").Subscribe(x => OnCodeMirrorSelectionChange(x.Args));
            EventCenter.GetObservable<EditorEventArgs>("StateChange").Subscribe(x => OnStateChange(x.Args));
            RemoteInvoke.Handle("GetSettings", GetSettings);
            RemoteInvoke.Handle<JToken>("SetClipboard", OnSetClipboard);
            Settings.WhenPropertyChanged(nameof(Settings.AutoSave)).Subscribe(_ => Settings_AutoSaveChanged(Settings.AutoSave));
            this.WhenPropertyChanged(nameof(SearchValue)).Subscribe(_ => SearchValueChanged());
            this.WhenPropertyChanged(nameof(Saved)).Subscribe(_ => SavedOrAutoSavedSuccChanged());
            this.WhenPropertyChanged(nameof(AutoSavedSucc)).Subscribe(_ => SavedOrAutoSavedSuccChanged());
            UndoCommand.OnExecute.Subscribe(_ => Undo());
            RedoCommand.OnExecute.Subscribe(_ => Redo());
            FindCommand.OnExecute.Subscribe(x => Find(x));
            PasteCommand.OnExecute.Subscribe(x => Paste(x));
            CutCommand.OnExecute.Subscribe(x => Cut(x));
            CopyCommand.OnExecute.Subscribe(x => Copy(x));
            DeleteSelectionCommand.OnExecute.Subscribe(_ => DeleteSelection());
            SelectAllCommand.OnExecute.Subscribe(_ => SelectAll());
        }

        public async Task<object> GetSettings()
        {
            if (FirstStart)
            {
                FirstStart = false;
                await FileViewModel.LoadStartUpMarkdown();
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
            MarkdownEditor?.PostMessage("Search", new
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

        public void Undo()
        {
            var state = History.Undo();
            if (state == null)
            {
                return;
            }
            OnMarkdownChange(state.Text);
            MarkdownEditor?.PostMessage("SetMarkdown", new
            {
                text = state.Text,
                cursor = state.Cursor
            });
        }

        public void Redo()
        {
            var state = History.Redo();
            if (state == null)
            {
                return;
            }
            OnMarkdownChange(state.Text);
            MarkdownEditor?.PostMessage("SetMarkdown", new
            {
                text = state.Text,
                cursor = state.Cursor
            });
        }

        public void Cut(string type)
        {
            MarkdownEditor?.PostMessage("Cut", new { type });
        }

        public void Paste(string type)
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
                MarkdownEditor?.PostMessage("Paste", new { type, text, html });
                return;
            }
            var files = Clipboard.GetFileDropList();
            if (files != null && files.Count == 1)
            {
                if (FileExtension.Image.Where(files[0].ToLower().EndsWith).Any())
                {
                    MarkdownEditor?.PostMessage("InsertImage", new { src = files[0] });
                }
                return;
            }
        }

        public void Copy(string type)
        {
            MarkdownEditor?.PostMessage("Copy", new { type });
        }

        public void OnSetClipboard(JToken arg)
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
        }

        public void DeleteSelection()
        {
            MarkdownEditor?.PostMessage("DeleteSelection", null);
        }

        public void SelectAll()
        {
            MarkdownEditor?.PostMessage("SelectAll", null);
        }

        public void Find(string action)
        {
            var appViewModel = ServiceProvider.GetService<AppViewModel>();
            if (appViewModel.FloatViewModel.SearchOpen == 0)
            {
                appViewModel.FloatViewModel.SearchOpen = FloatViewModel.SearchOpenType.Search;
                appViewModel.EditorViewModel.OnSearch();
            }
            else
            {
                MarkdownEditor?.PostMessage("Find", new { action });
            }
        }

        public void SearchValueChanged()
        {
            var floatViewModel = ServiceProvider.GetService<FloatViewModel>();
            if (floatViewModel.SearchOpen > 0)
                OnSearch();
        }

        public void SavedOrAutoSavedSuccChanged()
        {
            var fileViewModel = ServiceProvider.GetService<FileViewModel>();
            DisplaySaved = Saved || (Settings.AutoSave && fileViewModel.FilePath != null && AutoSavedSucc);
            if (Saved)
                AutoBackup.DeleteBackup(fileViewModel.FilePath);
        }

        public void Settings_AutoSaveChanged(bool autoSave)
        {
            DisplaySaved = Saved || autoSave;
        }

        public void JumpByIndex(int index)
        {
            try
            {
                if (ContentState["toc"] != null)
                {
                    MarkdownEditor?.PostMessage("ScrollTo", new { slug = ContentState["toc"][index]["slug"].ToString() });
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
            }
        }
    }
}
