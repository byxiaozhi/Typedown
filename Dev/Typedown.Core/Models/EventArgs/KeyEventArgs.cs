using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Typedown.Core.Enums;

namespace Typedown.Core.Models
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
