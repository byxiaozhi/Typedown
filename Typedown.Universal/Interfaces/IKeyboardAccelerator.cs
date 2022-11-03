using System;
using Typedown.Universal.Models;
using Windows.System;

namespace Typedown.Universal.Interfaces
{
    public interface IKeyboardAccelerator
    {
        IDisposable Register(ShortcutKey key, EventHandler<KeyEventArgs> handler);

        string GetShortcutKeyText(ShortcutKey key);

        string GetVirtualKeyNameText(VirtualKey key);
    }
}
