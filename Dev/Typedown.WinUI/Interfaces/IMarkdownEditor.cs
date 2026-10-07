using System;
using System.ComponentModel;

namespace Typedown.WinUI.Interfaces
{
    // Ported from Typedown.Core\Interfaces\IMarkdownEditor.cs.
    // GetDummyRectangle/MoveDummyRectangle (Windows.UI.Xaml.Shapes.Rectangle overlay positioning) are
    // deferred to milestone #4 (editor container + floating controls) and will come back as
    // Microsoft.UI.Xaml.Shapes.Rectangle. This is just enough surface for the message transport
    // (milestone #2) to compile and round-trip.
    public interface IMarkdownEditor : IDisposable, INotifyPropertyChanged
    {
        bool PostMessage(string name, object arg);

        bool IsEditorLoadFailed { get; }

        bool IsEditorLoaded { get; }
    }
}
