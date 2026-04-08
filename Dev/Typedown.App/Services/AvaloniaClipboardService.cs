using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Typedown.App.Services;

/// <summary>
/// Avalonia 12 clipboard implementation for Typedown.Core.Interfaces.IClipboard.
/// Accesses the native clipboard via TopLevel.Clipboard using dynamic dispatch
/// to avoid naming conflicts with Typedown.Core.Interfaces.IClipboard.
/// </summary>
public class AvaloniaClipboardService : Typedown.Core.Interfaces.IClipboard
{
    private static dynamic? GetNativeClipboard()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow?.Clipboard;
        return null;
    }

    public bool ContainsText(Typedown.Core.Interfaces.TextDataFormat format)
    {
        try
        {
            dynamic? cb = GetNativeClipboard();
            if (cb == null) return false;
            string? text = ((Task<string?>)cb.GetTextAsync()).GetAwaiter().GetResult();
            return !string.IsNullOrEmpty(text);
        }
        catch { return false; }
    }

    public async Task<string> GetTextAsync(Typedown.Core.Interfaces.TextDataFormat format)
    {
        try
        {
            dynamic? cb = GetNativeClipboard();
            if (cb == null) return string.Empty;
            return (string?)await cb.GetTextAsync() ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    public void SetText(string text, Typedown.Core.Interfaces.TextDataFormat format)
    {
        try
        {
            dynamic? cb = GetNativeClipboard();
            if (cb != null) _ = (Task)cb.SetTextAsync(text);
        }
        catch { }
    }

    public void SetText(string text)
    {
        try
        {
            dynamic? cb = GetNativeClipboard();
            if (cb != null) _ = (Task)cb.SetTextAsync(text);
        }
        catch { }
    }

    public Task<StringCollection> GetFileDropListAsync()
        => Task.FromResult(new StringCollection());

    public Task SetFileDropListAsync(StringCollection fileDropList)
        => Task.CompletedTask;

    public Task<Typedown.Core.Interfaces.IClipboardImage> GetImageAsync()
        => Task.FromResult<Typedown.Core.Interfaces.IClipboardImage>(null!);
}
