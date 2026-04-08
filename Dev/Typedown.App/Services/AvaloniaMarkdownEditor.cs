using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using Newtonsoft.Json;
using Typedown.Core.Interfaces;

namespace Typedown.App.Services;

/// <summary>
/// Avalonia implementation of IMarkdownEditor using NativeWebView.
/// Bridges Core ViewModel PostMessage calls to the JS editor via WebView.
/// </summary>
public class AvaloniaMarkdownEditor : IMarkdownEditor
{
    private NativeWebView? _webView;
    private bool _isLoaded;
    private bool _isFailed;

    public bool IsEditorLoaded
    {
        get => _isLoaded;
        private set
        {
            if (_isLoaded != value)
            {
                _isLoaded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEditorLoaded)));
            }
        }
    }

    public bool IsEditorLoadFailed
    {
        get => _isFailed;
        private set
        {
            if (_isFailed != value)
            {
                _isFailed = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEditorLoadFailed)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Attach this editor service to a WebView control in the UI.
    /// </summary>
    public void AttachWebView(NativeWebView webView)
    {
        _webView = webView;
    }

    public bool PostMessage(string name, object? arg)
    {
        if (_webView == null) return false;

        try
        {
            var json = arg != null
                ? JsonConvert.SerializeObject(new { name, arg }, Typedown.Core.Config.EditorJsonSerializerSettings)
                : JsonConvert.SerializeObject(new { name }, Typedown.Core.Config.EditorJsonSerializerSettings);

            var escaped = json.Replace("\\", "\\\\").Replace("'", "\\'");
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    await _webView.InvokeScript($"window.postMessageFromNative && window.postMessageFromNative('{escaped}')");
                }
                catch
                {
                    // Ignore script execution errors
                }
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void SetEditorLoaded(bool loaded) => IsEditorLoaded = loaded;
    public void SetEditorFailed(bool failed) => IsEditorLoadFailed = failed;

    public void Dispose()
    {
        _webView = null;
    }
}
