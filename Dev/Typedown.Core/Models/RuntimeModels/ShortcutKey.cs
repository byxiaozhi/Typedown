using Windows.System;

namespace Typedown.Core.Models
{
    public record ShortcutKey(VirtualKeyModifiers Modifiers, VirtualKey Key)
    {
        public override string ToString()
        {
            return base.ToString();
        }
    }
}
