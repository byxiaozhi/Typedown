using System;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Typedown.App.Services;
using Typedown.Core.Enums;
using Typedown.Core.Interfaces;
using Typedown.Core.Services;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;

namespace Typedown.App.Views;

public partial class MainWindow : Window
{
    private NativeWebView? _webView;
    private readonly AppViewModel _appVm;
    private readonly FileViewModel _fileVm;
    private readonly EditorViewModel _editorVm;
    private readonly FormatViewModel _formatVm;
    private readonly ParagraphViewModel _paragraphVm;
    private readonly UIViewModel _uiVm;
    private readonly SettingsViewModel _settingsVm;
    private readonly RemoteInvoke _remoteInvoke;
    private readonly EventCenter _eventCenter;
    private readonly AvaloniaMarkdownEditor _markdownEditor;
    private readonly CompositeDisposable _disposables = new();
    private bool _isCloseable;
    private bool _isClosing;

    public MainWindow()
    {
        InitializeComponent();

        var sp = App.Services;
        _appVm = sp.GetRequiredService<AppViewModel>();
        _fileVm = sp.GetRequiredService<FileViewModel>();
        _editorVm = sp.GetRequiredService<EditorViewModel>();
        _formatVm = sp.GetRequiredService<FormatViewModel>();
        _paragraphVm = sp.GetRequiredService<ParagraphViewModel>();
        _uiVm = sp.GetRequiredService<UIViewModel>();
        _settingsVm = sp.GetRequiredService<SettingsViewModel>();
        _remoteInvoke = sp.GetRequiredService<RemoteInvoke>();
        _eventCenter = sp.GetRequiredService<EventCenter>();
        _markdownEditor = sp.GetRequiredService<AvaloniaMarkdownEditor>();

        SetupBindings();
        WireMenuCommands();
    }

    // ═══════════ Reactive Bindings ═══════════

    private void SetupBindings()
    {
        // Window title (matches original: "FileName - Typedown")
        _disposables.Add(_uiVm.WhenPropertyChanged(nameof(UIViewModel.MainWindowTitle))
            .Cast<string>().StartWith(_uiVm.MainWindowTitle)
            .Subscribe(t => Dispatcher.UIThread.Post(() => Title = t ?? "Typedown")));

        // Save status — show * prefix in title (original behavior)
        _disposables.Add(_editorVm.WhenPropertyChanged(nameof(EditorViewModel.Saved))
            .Cast<bool>().StartWith(_editorVm.Saved)
            .Subscribe(_ => { })); // Title already managed by UIViewModel

        // Theme switching
        _disposables.Add(_settingsVm.WhenPropertyChanged(nameof(SettingsViewModel.AppTheme))
            .Cast<AppTheme>().StartWith(_settingsVm.AppTheme)
            .Subscribe(theme => Dispatcher.UIThread.Post(() => SetTheme(theme))));

        // Word count in bottom bar (matches original: "114 字")
        _disposables.Add(_eventCenter.GetObservable<Typedown.Core.Models.EditorEventArgs>("StateChange")
            .Subscribe(args =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        var wordCount = (int)(args.Args?["wordCount"] ?? 0);
                        SetStatus("StatusWordCount", $"{wordCount} 字");
                    }
                    catch { }
                });
            }));
    }

    // ═══════════ Menu Command Wiring ═══════════

    private void WireMenuCommands()
    {
        // File
        Bind("MenuNewFile", () => _fileVm.NewFileCommand.Execute(Unit.Default));
        Bind("MenuOpenFile", () => _fileVm.OpenFileCommand.Execute(null));
        Bind("MenuOpenFolder", () => _fileVm.OpenFolderCommand.Execute(null));
        Bind("MenuSave", () => _fileVm.SaveCommand.Execute(Unit.Default));
        Bind("MenuSaveAs", () => _fileVm.SaveAsCommand.Execute(Unit.Default));
        Bind("MenuExit", () => Close());

        // Edit
        Bind("MenuUndo", () => _editorVm.UndoCommand.Execute(Unit.Default));
        Bind("MenuRedo", () => _editorVm.RedoCommand.Execute(Unit.Default));
        Bind("MenuCut", () => _editorVm.CutCommand.Execute("cut"));
        Bind("MenuCopy", () => _editorVm.CopyCommand.Execute("copy"));
        Bind("MenuPaste", () => _editorVm.PasteCommand.Execute("paste"));
        Bind("MenuSelectAll", () => _editorVm.SelectAllCommand.Execute(Unit.Default));
        Bind("MenuFind", () => _editorVm.FindCommand.Execute(null));

        // Paragraph
        Bind("MenuH1", () => _paragraphVm.UpdateParagraphCommand.Execute("atx-heading 1"));
        Bind("MenuH2", () => _paragraphVm.UpdateParagraphCommand.Execute("atx-heading 2"));
        Bind("MenuH3", () => _paragraphVm.UpdateParagraphCommand.Execute("atx-heading 3"));
        Bind("MenuH4", () => _paragraphVm.UpdateParagraphCommand.Execute("atx-heading 4"));
        Bind("MenuH5", () => _paragraphVm.UpdateParagraphCommand.Execute("atx-heading 5"));
        Bind("MenuH6", () => _paragraphVm.UpdateParagraphCommand.Execute("atx-heading 6"));
        Bind("MenuUpgradeParagraph", () => _paragraphVm.UpdateParagraphCommand.Execute("upgrade heading"));
        Bind("MenuDegradeParagraph", () => _paragraphVm.UpdateParagraphCommand.Execute("degrade heading"));
        Bind("MenuTable", () => _paragraphVm.InsertTableCommand.Execute(Unit.Default));
        Bind("MenuCodeBlock", () => _paragraphVm.InsertParagraphCommand.Execute("pre"));
        Bind("MenuQuote", () => _paragraphVm.InsertParagraphCommand.Execute("block-quote"));
        Bind("MenuMathBlock", () => _paragraphVm.InsertParagraphCommand.Execute("mathblock"));
        Bind("MenuHorizontalRule", () => _paragraphVm.InsertParagraphCommand.Execute("thematic-break"));
        Bind("MenuOrderedList", () => _paragraphVm.InsertParagraphCommand.Execute("order-list"));
        Bind("MenuBulletList", () => _paragraphVm.InsertParagraphCommand.Execute("bullet-list"));
        Bind("MenuTaskList", () => _paragraphVm.InsertParagraphCommand.Execute("task-list"));

        // Format
        Bind("MenuBold", () => _formatVm.SetFormatCommand.Execute("strong"));
        Bind("MenuItalic", () => _formatVm.SetFormatCommand.Execute("em"));
        Bind("MenuUnderline", () => _formatVm.SetFormatCommand.Execute("u"));
        Bind("MenuStrikethrough", () => _formatVm.SetFormatCommand.Execute("del"));
        Bind("MenuInlineCode", () => _formatVm.SetFormatCommand.Execute("inline_code"));
        Bind("MenuInlineMath", () => _formatVm.SetFormatCommand.Execute("inline_math"));
        Bind("MenuHyperlink", () => _formatVm.SetFormatCommand.Execute("a"));
        Bind("MenuImage", () => _formatVm.SetFormatCommand.Execute("image"));
        Bind("MenuClearFormat", () => _formatVm.SetFormatCommand.Execute("clear"));

        // View
        Bind("MenuSourceCode", () => _markdownEditor?.PostMessage("SwitchSourceCodeMode", null));
        Bind("MenuSidebarToggle", () => ToggleSidebar());

        // Settings gear
        Bind("MenuSettings", () => { /* TODO: Open settings page */ });
    }

    private void Bind(string menuName, Action action)
    {
        // Use non-generic approach to avoid type mismatch when name is found as wrong type
        var control = this.GetControl<Control>(menuName);
        if (control is MenuItem menuItem)
            menuItem.Click += (_, _) => action();
        else if (control is Button button)
            button.Click += (_, _) => action();
    }

    private T? GetControl<T>(string name) where T : Control
    {
        try { return this.FindControl<T>(name); }
        catch { return null; }
    }

    // ═══════════ WebView / Editor ═══════════

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _appVm.MainWindow = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

        _webView = this.FindControl<NativeWebView>("EditorWebView");
        if (_webView == null) return;

        _markdownEditor.AttachWebView(_webView);

        _webView.WebMessageReceived += (_, args) =>
        {
            var message = args.Body;
            if (string.IsNullOrEmpty(message)) return;
            Dispatcher.UIThread.Post(() =>
            {
                try { HandleEditorMessage(message); }
                catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"JS error: {ex.Message}"); }
            });
        };

        _webView.NavigationCompleted += (_, args) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (args.IsSuccess) _markdownEditor.SetEditorLoaded(true);
                else _markdownEditor.SetEditorFailed(true);
            });
        };

        LoadEditorPage();
    }

    private void HandleEditorMessage(string json)
    {
        try
        {
            var doc = Newtonsoft.Json.Linq.JObject.Parse(json);
            var name = doc["name"]?.ToString() ?? doc["type"]?.ToString();
            var arg = doc["arg"] ?? doc["data"];
            if (string.IsNullOrEmpty(name)) return;

            try { _ = _remoteInvoke.Invoke(name, arg); return; }
            catch { /* not a registered handler */ }

            _eventCenter.EmitEvent(name, new Typedown.Core.Models.EditorEventArgs(name, arg));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"HandleEditorMessage: {ex.Message}");
        }
    }

    private void SetStatus(string name, string text)
    {
        var el = this.FindControl<TextBlock>(name);
        if (el != null) el.Text = text;
    }

    private void LoadEditorPage()
    {
        if (_webView == null) return;
        var editorDir = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".",
            "Assets", "Editor");
        var htmlPath = Path.Combine(editorDir, "index.html");
        if (File.Exists(htmlPath))
            _webView.Navigate(new Uri($"file:///{htmlPath.Replace('\\', '/')}"));
    }

    // ═══════════ UI Handlers ═══════════

    private void OnToggleSidebar(object? sender, RoutedEventArgs e) => ToggleSidebar();

    private void OnToggleSourceMode(object? sender, RoutedEventArgs e)
    {
        _markdownEditor?.PostMessage("SwitchSourceCodeMode", null);
    }

    private void ToggleSidebar()
    {
        var grid = this.FindControl<Grid>("MainGrid");
        if (grid != null && grid.ColumnDefinitions.Count > 0)
        {
            var col = grid.ColumnDefinitions[0];
            col.Width = col.Width == new GridLength(0)
                ? new GridLength(240) : new GridLength(0);
        }
    }

    private void SetTheme(AppTheme theme)
    {
        RequestedThemeVariant = theme switch
        {
            AppTheme.Light => Avalonia.Styling.ThemeVariant.Light,
            AppTheme.Dark => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Default,
        };
    }

    // ═══════════ Window Lifecycle ═══════════

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (!_isCloseable)
        {
            e.Cancel = true;
            if (!_isClosing)
            {
                _isClosing = true;
                try
                {
                    await _fileVm.AutoSaveFile();
                    if (_editorVm.Saved || await _fileVm.AskToSave())
                    {
                        _isCloseable = true;
                        Close();
                    }
                }
                finally { _isClosing = false; }
            }
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _disposables.Dispose();
        _appVm.Dispose();
        base.OnClosed(e);
    }
}