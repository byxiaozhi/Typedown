using System;
using Windows.Foundation;
using Windows.UI.Xaml.Shapes;

namespace Typedown.Core.Interfaces
{
    public interface IMarkdownEditor : IDisposable
    {
        bool PostMessage(string name, object arg);

        Rectangle GetDummyRectangle(Rect rect);

        Rectangle MoveDummyRectangle(Point offset);
    }
}
