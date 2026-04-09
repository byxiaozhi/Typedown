using System;
using System.ComponentModel;

namespace Typedown.Core.Interfaces
{
    /// <summary>
    /// Cross-platform interface for the Markdown editor control.
    /// Platform implementations will wrap WebView-based Vditor editor.
    /// </summary>
    public interface IMarkdownEditor : IDisposable, INotifyPropertyChanged
    {
        bool PostMessage(string name, object arg);

        Avalonia.Controls.Control GetDummyRectangle(Avalonia.Rect rect);

        bool IsEditorLoadFailed { get; }

        bool IsEditorLoaded { get; }
    }
}
