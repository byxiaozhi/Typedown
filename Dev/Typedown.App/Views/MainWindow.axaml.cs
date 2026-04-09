using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Typedown.App.Services;
using Typedown.Core.Enums;
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

    // Sidebar state
    private bool _showOutline;
    private TreeView? _outlineTree;
    private TreeView? _fileExplorer;

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

        // Grab references
        _fileExplorer = this.FindControl<TreeView>("FileExplorer");

        // Create outline TreeView for the 大纲 tab
        _outlineTree = new TreeView
        {
            Background = Avalonia.Media.Brushes.Transparent,
            FontSize = 13,
            Padding = new Thickness(4, 8),
        };
        _outlineTree.DoubleTapped += OnOutlineItemDoubleTapped;

        SetupBindings();
        WireMenuCommands();
    }

    // ═══════════ Reactive Bindings ═══════════

    private void SetupBindings()
    {
        // Window title
        _disposables.Add(_uiVm.WhenPropertyChanged(nameof(UIViewModel.MainWindowTitle))
            .Cast<string>().StartWith(_uiVm.MainWindowTitle)
            .Subscribe(t => Dispatcher.UIThread.Post(() => Title = t ?? "Typedown")));

        // Theme switching + sync to editor
        _disposables.Add(_settingsVm.WhenPropertyChanged(nameof(SettingsViewModel.AppTheme))
            .Cast<AppTheme>().StartWith(_settingsVm.AppTheme)
            .Subscribe(theme => Dispatcher.UIThread.Post(() =>
            {
                SetTheme(theme);
                SyncEditorTheme(theme);
            })));
    }

    // ═══════════ Menu → Vditor Commands ═══════════

    private void WireMenuCommands()
    {
        // File (via Core ViewModels)
        Bind("MenuNewFile", () => _fileVm.NewFileCommand.Execute(Unit.Default));
        Bind("MenuOpenFile", () => _fileVm.OpenFileCommand.Execute(null));
        Bind("MenuOpenFolder", () => _fileVm.OpenFolderCommand.Execute(null));
        Bind("MenuSave", () => _fileVm.SaveCommand.Execute(Unit.Default));
        Bind("MenuSaveAs", () => _fileVm.SaveAsCommand.Execute(Unit.Default));
        Bind("MenuExit", () => Close());

        // Edit → Vditor
        BindVditor("MenuUndo", "Undo");
        BindVditor("MenuRedo", "Redo");
        BindVditor("MenuCut", "Cut");
        BindVditor("MenuCopy", "Copy");
        BindVditor("MenuPaste", "Paste");
        BindVditor("MenuSelectAll", "SelectAll");
        BindVditor("MenuFind", "Find", new { action = "open" });

        // Paragraph → Vditor
        BindVditor("MenuH1", "Heading", new { level = 1 });
        BindVditor("MenuH2", "Heading", new { level = 2 });
        BindVditor("MenuH3", "Heading", new { level = 3 });
        BindVditor("MenuH4", "Heading", new { level = 4 });
        BindVditor("MenuH5", "Heading", new { level = 5 });
        BindVditor("MenuH6", "Heading", new { level = 6 });
        BindVditor("MenuTable", "InsertTable", new { rows = 3, columns = 3 });
        BindVditor("MenuCodeBlock", "InsertBlock", new { type = "code-block" });
        BindVditor("MenuQuote", "InsertBlock", new { type = "blockquote" });
        BindVditor("MenuMathBlock", "InsertBlock", new { type = "math-block" });
        BindVditor("MenuHorizontalRule", "InsertBlock", new { type = "hr" });
        BindVditor("MenuOrderedList", "InsertBlock", new { type = "ordered-list" });
        BindVditor("MenuBulletList", "InsertBlock", new { type = "bullet-list" });
        BindVditor("MenuTaskList", "InsertBlock", new { type = "task-list" });

        // Format → Vditor toolbar actions
        BindVditor("MenuBold", "ToolbarAction", new { action = "bold" });
        BindVditor("MenuItalic", "ToolbarAction", new { action = "italic" });
        BindVditor("MenuUnderline", "ToolbarAction", new { action = "underline" });
        BindVditor("MenuStrikethrough", "ToolbarAction", new { action = "strike" });
        BindVditor("MenuInlineCode", "ToolbarAction", new { action = "inline-code" });
        BindVditor("MenuInlineMath", "ToolbarAction", new { action = "inline-math" });
        BindVditor("MenuHyperlink", "ToolbarAction", new { action = "link" });
        BindVditor("MenuImage", "ToolbarAction", new { action = "image" });
        BindVditor("MenuClearFormat", "ToolbarAction", new { action = "clear" });

        // View
        BindVditor("MenuSourceCode", "SwitchMode", new { mode = "sv" });
        Bind("MenuSidebarToggle", () => ToggleSidebar());

        // Settings
        Bind("MenuSettings", () => { /* TODO: Open settings page */ });
    }

    private void BindVditor(string menuName, string command, object? data = null)
    {
        Bind(menuName, () => _markdownEditor?.PostMessage(command, data));
    }

    private void Bind(string menuName, Action action)
    {
        var control = SafeFindControl<Control>(menuName);
        if (control is MenuItem mi)
            mi.Click += (_, _) => action();
        else if (control is Button btn)
            btn.Click += (_, _) => action();
    }

    private T? SafeFindControl<T>(string name) where T : Control
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
                catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[JS→C#] {ex.Message}"); }
            });
        };

        _webView.NavigationCompleted += (_, args) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (args.IsSuccess)
                {
                    _markdownEditor.SetEditorLoaded(true);
                    SyncEditorTheme(_settingsVm.AppTheme);
                }
                else _markdownEditor.SetEditorFailed(true);
            });
        };

        LoadEditorPage();

        // Force Mica through by clearing opaque backgrounds in FluentTheme window template
        Console.WriteLine($"[Typedown] ActualTransparencyLevel = {ActualTransparencyLevel}");
        ClearTemplateBackgrounds(this);
    }

    /// <summary>
    /// Central JS→C# message router.
    /// Handles UI-specific events directly, then forwards the rest to Core services.
    /// </summary>
    private void HandleEditorMessage(string json)
    {
        try
        {
            var doc = JObject.Parse(json);
            var name = doc["type"]?.ToString() ?? doc["name"]?.ToString();
            var data = doc["data"] ?? doc["arg"];
            if (string.IsNullOrEmpty(name)) return;

            // ★ Handle UI events directly (word count, outline, etc.)
            switch (name)
            {
                case "StateChange":
                    OnStateChange(data);
                    break;
                case "WordCountUpdate":
                    OnWordCountUpdate(data);
                    break;
                case "TocUpdate":
                    OnTocUpdate(data);
                    break;
                case "contentChanged":
                    OnContentChanged(data);
                    break;
            }

            // Forward to Core RemoteInvoke
            try { _ = _remoteInvoke.Invoke(name, data); }
            catch { /* not a registered handler */ }

            // Also emit to EventCenter (for Core ViewModel subscribers)
            _eventCenter.EmitEvent(name, new Typedown.Core.Models.EditorEventArgs(name, data));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"HandleEditorMessage: {ex.Message}");
        }
    }

    // ═══════════ JS Event Handlers ═══════════

    private void OnWordCountUpdate(JToken? data)
    {
        if (data == null) return;
        var wc = data["WordCount"];
        if (wc == null) return;
        var words = (int)(wc["Word"] ?? 0);
        var el = this.FindControl<TextBlock>("StatusWordCount");
        if (el != null) el.Text = $"{words} 字";
    }

    private void OnTocUpdate(JToken? data)
    {
        if (data == null) return;
        var toc = data["Toc"] as JArray;
        if (toc != null) UpdateOutlineTree(toc);
    }

    private void OnStateChange(JToken? data)
    {
        if (data == null) return;
        var state = data["state"];
        if (state == null) return;

        // Word count (backward-compat)
        var wc = state["WordCount"];
        if (wc != null)
        {
            var words = (int)(wc["Word"] ?? 0);
            var el = this.FindControl<TextBlock>("StatusWordCount");
            if (el != null) el.Text = $"{words} 字";
        }

        // Outline / TOC (backward-compat)
        var toc = state["Toc"] as JArray;
        if (toc != null) UpdateOutlineTree(toc);
    }

    private void OnContentChanged(JToken? data)
    {
        // Could update save state indicator etc.
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

    // ═══════════ Mica Transparency Fix ═══════════

    /// <summary>
    /// Walk top-level visual tree (3 levels) and clear opaque backgrounds
    /// that FluentTheme injects, so the Mica backdrop shines through.
    /// </summary>
    private void ClearTemplateBackgrounds(Avalonia.Visual root)
    {
        foreach (var child in Avalonia.VisualTree.VisualExtensions.GetVisualChildren(root))
        {
            // Template panels (LayoutRoot, BackgroundLayer)
            if (child is Avalonia.Controls.Panel panel
                && panel.Background != null
                && panel.Background != Avalonia.Media.Brushes.Transparent)
            {
                panel.Background = Avalonia.Media.Brushes.Transparent;
            }

            // Only recurse into template layers, not our content
            if (child is Avalonia.Visual v)
            {
                foreach (var grandchild in Avalonia.VisualTree.VisualExtensions.GetVisualChildren(v))
                {
                    if (grandchild is Avalonia.Controls.Panel p2
                        && p2.Background != null
                        && p2.Background != Avalonia.Media.Brushes.Transparent)
                    {
                        p2.Background = Avalonia.Media.Brushes.Transparent;
                    }
                }
            }
        }
    }

    // ═══════════ Theme ═══════════

    private void SetTheme(AppTheme theme)
    {
        RequestedThemeVariant = theme switch
        {
            AppTheme.Light => Avalonia.Styling.ThemeVariant.Light,
            AppTheme.Dark => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Default,
        };
    }

    private void SyncEditorTheme(AppTheme theme)
    {
        var isDark = theme == AppTheme.Dark;
        if (theme == AppTheme.Default)
            isDark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        _markdownEditor?.PostMessage("SetTheme", new { theme = isDark ? "dark" : "classic" });
    }

    // ═══════════ Sidebar Toggle ═══════════

    private void OnToggleSidebar(object? sender, RoutedEventArgs e) => ToggleSidebar();

    private void OnToggleSourceMode(object? sender, RoutedEventArgs e)
    {
        _markdownEditor?.PostMessage("SwitchSourceCodeMode", null);
    }

    private void ToggleSidebar()
    {
        var grid = this.FindControl<Grid>("MainGrid");
        if (grid == null) return;

        var col = grid.ColumnDefinitions[0]; // SidebarColumn
        bool isCollapsed = col.Width == new GridLength(0);
        col.Width = isCollapsed ? new GridLength(240) : new GridLength(0);

        var splitter = this.FindControl<GridSplitter>("SidebarSplitter");
        if (splitter != null) splitter.IsVisible = isCollapsed;
    }

    // ═══════════ Sidebar Tabs ═══════════

    private void OnTabFilesClick(object? sender, TappedEventArgs e)
    {
        _showOutline = false;
        RefreshSidebarContent();
    }

    private void OnTabOutlineClick(object? sender, TappedEventArgs e)
    {
        _showOutline = true;
        RefreshSidebarContent();
    }

    private void RefreshSidebarContent()
    {
        // Update visual state of tabs
        var tabFiles = this.FindControl<Border>("TabFiles");
        var tabOutline = this.FindControl<Border>("TabOutline");
        if (tabFiles != null)
        {
            tabFiles.Classes.Clear();
            tabFiles.Classes.Add(_showOutline ? "tabInactive" : "tabActive");
            if (tabFiles.Child is TextBlock tbf) tbf.Opacity = _showOutline ? 0.5 : 0.9;
        }
        if (tabOutline != null)
        {
            tabOutline.Classes.Clear();
            tabOutline.Classes.Add(_showOutline ? "tabActive" : "tabInactive");
            if (tabOutline.Child is TextBlock tbo) tbo.Opacity = _showOutline ? 0.9 : 0.5;
        }

        // Swap content in the sidebar panel
        var panel = this.FindControl<DockPanel>("SidebarPanel");
        if (panel == null) return;

        // Keep the tab header (child 0), remove everything else
        while (panel.Children.Count > 1)
            panel.Children.RemoveAt(panel.Children.Count - 1);

        if (_showOutline && _outlineTree != null)
        {
            // Detach from previous parent if needed
            if (_outlineTree.Parent != null)
                ((Panel)_outlineTree.Parent).Children.Remove(_outlineTree);
            panel.Children.Add(_outlineTree);
        }
        else if (_fileExplorer != null)
        {
            if (_fileExplorer.Parent != null)
                ((Panel)_fileExplorer.Parent).Children.Remove(_fileExplorer);
            panel.Children.Add(_fileExplorer);
        }
    }

    // ═══════════ Outline Tree ═══════════

    private void UpdateOutlineTree(JArray toc)
    {
        if (_outlineTree == null) return;
        _outlineTree.Items.Clear();

        foreach (var item in toc)
        {
            var level = (int)(item["Lvl"] ?? 1);
            var content = item["Content"]?.ToString() ?? "";
            var slug = item["Slug"]?.ToString() ?? "";

            _outlineTree.Items.Add(new TreeViewItem
            {
                Header = content,
                Tag = slug,
                Padding = new Thickness(4 + (level - 1) * 16, 3, 4, 3),
                FontSize = level <= 2 ? 13 : 12,
                Opacity = level == 1 ? 1.0 : level == 2 ? 0.85 : 0.7,
            });
        }
    }

    private void OnOutlineItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_outlineTree?.SelectedItem is TreeViewItem tvi && tvi.Tag is string slug)
            _markdownEditor?.PostMessage("ScrollTo", new { slug });
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