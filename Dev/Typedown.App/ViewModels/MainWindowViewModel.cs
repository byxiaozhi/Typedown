using System;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;

namespace Typedown.App.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private string _markdownContent = "";
    public string MarkdownContent
    {
        get => _markdownContent;
        set => this.RaiseAndSetIfChanged(ref _markdownContent, value);
    }

    private string _editorMode = "wysiwyg";
    public string EditorMode
    {
        get => _editorMode;
        set => this.RaiseAndSetIfChanged(ref _editorMode, value);
    }

    private string _statusText = "Initializing...";
    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    private int _wordCount;
    public int WordCount
    {
        get => _wordCount;
        set => this.RaiseAndSetIfChanged(ref _wordCount, value);
    }

    private bool _isEditorReady;
    public bool IsEditorReady
    {
        get => _isEditorReady;
        set => this.RaiseAndSetIfChanged(ref _isEditorReady, value);
    }

    // Commands
    public ReactiveCommand<Unit, Unit> SwitchToWysiwygCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToIrCommand { get; }
    public ReactiveCommand<Unit, Unit> InsertSampleCommand { get; }
    public ReactiveCommand<Unit, Unit> GetContentCommand { get; }
    public ReactiveCommand<string, Unit> FormatCommand { get; }

    // Events for the View to handle (sends messages to WebView)
    public event Action<string, object?>? SendToEditor;

    public MainWindowViewModel()
    {
        var isReady = this.WhenAnyValue(x => x.IsEditorReady);

        SwitchToWysiwygCommand = ReactiveCommand.Create(() =>
        {
            EditorMode = "wysiwyg";
            SendToEditor?.Invoke("switchMode", new { mode = "wysiwyg" });
        }, isReady);

        SwitchToIrCommand = ReactiveCommand.Create(() =>
        {
            EditorMode = "ir";
            SendToEditor?.Invoke("switchMode", new { mode = "ir" });
        }, isReady);

        InsertSampleCommand = ReactiveCommand.Create(() =>
        {
            var sample = @"# Typedown PoC

## Avalonia + Vditor Integration Test

This is a **proof of concept** demonstrating:

- ✅ Avalonia 12 with ReactiveUI (MVVM)
- ✅ NativeWebView embedding
- ✅ Vditor WYSIWYG/IR editor
- ✅ Bidirectional C# ↔ JS communication

### Code Example

```csharp
public class HelloWorld
{
    public static void Main() => Console.WriteLine(""Hello, Typedown!"");
}
```

### Table

| Feature | Status |
|---------|--------|
| WYSIWYG | ✅ |
| IR Mode | ✅ |
| Search  | 🔜 |

> This markdown was sent from C# to Vditor via the JS bridge.
";
            SendToEditor?.Invoke("setMarkdown", new { markdown = sample });
        }, isReady);

        GetContentCommand = ReactiveCommand.Create(() =>
        {
            SendToEditor?.Invoke("getMarkdown", null);
        }, isReady);

        FormatCommand = ReactiveCommand.Create<string>(action =>
        {
            SendToEditor?.Invoke("format", new { action });
        }, isReady);
    }

    /// <summary>
    /// Called by the View when a message is received from the JS editor
    /// </summary>
    public void HandleEditorMessage(string type, System.Text.Json.JsonElement data)
    {
        switch (type)
        {
            case "domReady":
                StatusText = "DOM ready, loading Vditor...";
                break;

            case "editorReady":
                IsEditorReady = true;
                var mode = data.TryGetProperty("mode", out var m) ? m.GetString() : "unknown";
                StatusText = $"Editor ready — Mode: {mode}";
                break;

            case "contentChanged":
                if (data.TryGetProperty("markdown", out var md))
                    MarkdownContent = md.GetString() ?? "";
                if (data.TryGetProperty("wordCount", out var wc))
                    WordCount = wc.GetInt32();
                StatusText = $"Word count: {WordCount} | Mode: {EditorMode}";
                break;

            case "selectionChanged":
                var selText = data.TryGetProperty("text", out var st) ? st.GetString() : "";
                if (!string.IsNullOrEmpty(selText))
                    StatusText = $"Selected: \"{(selText!.Length > 50 ? selText[..50] + "..." : selText)}\"";
                break;

            case "markdownContent":
                if (data.TryGetProperty("markdown", out var content))
                {
                    MarkdownContent = content.GetString() ?? "";
                    StatusText = $"Content retrieved ({MarkdownContent.Length} chars)";
                }
                break;

            case "editorFocus":
                break;

            case "editorBlur":
                break;

            case "pong":
                StatusText = "Pong received!";
                break;

            case "setMarkdownDone":
                StatusText = $"Markdown set — Mode: {EditorMode}";
                break;

            default:
                StatusText = $"Unknown message: {type}";
                break;
        }
    }
}
