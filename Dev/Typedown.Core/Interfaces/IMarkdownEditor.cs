using System;
using System.ComponentModel;
using Windows.Foundation;
using Windows.UI.Xaml.Shapes;

namespace Typedown.Core.Interfaces
{
    public interface IMarkdownEditor : IDisposable, INotifyPropertyChanged
    {
        bool PostMessage(string name, object arg);

        Rectangle GetDummyRectangle(Rect rect);

        Rectangle MoveDummyRectangle(Point offset);

        bool IsEditorLoadFailed { get; }

        bool IsEditorLoaded { get; }

        /// <summary>The keyboard to the editor page: the control focused, and the page given the keys.</summary>
        void FocusEditor();
    }
}
