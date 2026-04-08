using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;

namespace Typedown.App.Services;

/// <summary>
/// Cross-platform IFileOperation implementation using System.IO.
/// </summary>
public class FileOperationService : IFileOperation
{
    public bool Delete(StringCollection files)
    {
        try
        {
            foreach (string? f in files)
            {
                if (f == null) continue;
                if (File.Exists(f)) File.Delete(f);
                else if (Directory.Exists(f)) Directory.Delete(f, true);
            }
            return true;
        }
        catch { return false; }
    }

    public bool Copy(StringCollection files, string to)
    {
        try
        {
            Directory.CreateDirectory(to);
            foreach (string? f in files)
            {
                if (f == null) continue;
                var dest = Path.Combine(to, Path.GetFileName(f));
                if (File.Exists(f)) File.Copy(f, dest, true);
                else if (Directory.Exists(f)) CopyDirectory(f, dest);
            }
            return true;
        }
        catch { return false; }
    }

    public bool Move(StringCollection files, string to)
    {
        try
        {
            Directory.CreateDirectory(to);
            foreach (string? f in files)
            {
                if (f == null) continue;
                var dest = Path.Combine(to, Path.GetFileName(f));
                if (File.Exists(f)) File.Move(f, dest, true);
                else if (Directory.Exists(f)) Directory.Move(f, dest);
            }
            return true;
        }
        catch { return false; }
    }

    public bool Rename(string from, string to)
    {
        try
        {
            if (File.Exists(from))
                File.Move(from, to);
            else if (Directory.Exists(from))
                Directory.Move(from, to);
            else
                return false;
            return true;
        }
        catch { return false; }
    }

    public Task CutToClipboardAsync(StringCollection files) => Task.CompletedTask;
    public Task CopyToClipboardAsync(StringCollection files) => Task.CompletedTask;
    public bool IsPasteEnabled => false;
    public void PasteFromClipboard(string to) { }

    public bool IsFilenameValid(string sourceFolder, string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
            && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
            && !File.Exists(Path.Combine(sourceFolder, fileName));
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }
}
