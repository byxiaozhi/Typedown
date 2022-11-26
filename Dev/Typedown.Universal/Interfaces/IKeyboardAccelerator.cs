using System;
using Typedown.Universal.Models;
using Windows.System;

namespace Typedown.Universal.Interfaces
{
    public interface IKeyboardAccelerator
    {
        IDisposable Register(ShortcutKey key, EventHandler<KeyEventArgs> handler);

        IDisposable RegisterGlobal(EventHandler<KeyEventArgs> handler);

        IObservable<KeyEventArgs> GetObservable();

        string GetShortcutKeyText(ShortcutKey key);

        string GetVirtualKeyNameText(VirtualKey key);
    }
}
