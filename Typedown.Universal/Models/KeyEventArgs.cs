using System;
using Windows.System;

namespace Typedown.Universal.Models
{
    public class KeyEventArgs : EventArgs
    {
        public bool Handled { get; set; }

        public VirtualKey Key { get; }

        public VirtualKeyModifiers Modifiers { get; }

        public KeyEventArgs(VirtualKey key, VirtualKeyModifiers modifiers)
        {
            Key = key;
            Modifiers = modifiers;
        }
    }
}
