using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Typedown.Core.Interfaces;

namespace Typedown.App.Services;

/// <summary>
/// Avalonia implementation of IFilePickerService using Avalonia's StorageProvider API.
/// </summary>
public class AvaloniaFilePickerService : IFilePickerService
{
    public async Task<string?> PickOpenFileAsync(string[]? fileTypes, string? title = null)
    {
        var window = GetMainWindow();
        if (window == null) return null;

        var filters = BuildFilters(fileTypes);
        var result = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title ?? "Open File",
            AllowMultiple = false,
            FileTypeFilter = filters,
        });

        return result.FirstOrDefault()?.Path.LocalPath;
    }

    public async Task<string?> PickSaveFileAsync(string? defaultName, string[]? fileTypes, string? title = null)
    {
        var window = GetMainWindow();
        if (window == null) return null;

        var filters = BuildFilters(fileTypes);
        var result = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title ?? "Save File",
            SuggestedFileName = defaultName,
            FileTypeChoices = filters,
        });

        return result?.Path.LocalPath;
    }

    public async Task<string?> PickFolderAsync(string? title = null)
    {
        var window = GetMainWindow();
        if (window == null) return null;

        var result = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title ?? "Open Folder",
            AllowMultiple = false,
        });

        return result.FirstOrDefault()?.Path.LocalPath;
    }

    private static List<FilePickerFileType>? BuildFilters(string[]? extensions)
    {
        if (extensions == null || extensions.Length == 0)
            return null;

        return new List<FilePickerFileType>
        {
            new("Supported Files")
            {
                Patterns = extensions.Select(e => e.StartsWith('.') ? $"*{e}" : $"*.{e}").ToList()
            }
        };
    }

    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}
