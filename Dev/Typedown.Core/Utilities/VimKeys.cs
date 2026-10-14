using System.Linq;
using Windows.System;

namespace Typedown.Core.Utilities
{
    /// <summary>
    /// Settings > Editor > Vim keys: the Ctrl shortcuts the application leaves to the editor page while Vim needs them -
    /// in source mode's normal and visual mode (block visual, redo, half pages, jumps, increment, scrolling) and in
    /// reading mode (scrolling). Insert mode keeps the application's shortcuts. The page reports the state
    /// ("VimState"); the lists are the page's (Typedown.Editor services/vim.ts) and must stay the same.
    /// </summary>
    public static class VimKeys
    {
        private const VirtualKey LeftBracket = (VirtualKey)219; // VK_OEM_4, "[" on US layouts

        private static readonly VirtualKey[] Normal = { VirtualKey.V, VirtualKey.R, VirtualKey.D, VirtualKey.U, VirtualKey.O, VirtualKey.A, VirtualKey.X, VirtualKey.E, VirtualKey.Y, VirtualKey.B, VirtualKey.F, LeftBracket };
        private static readonly VirtualKey[] Reading = { VirtualKey.D, VirtualKey.U, VirtualKey.E, VirtualKey.Y, VirtualKey.F, VirtualKey.B };

        public static bool EditorWants(string vimState, VirtualKeyModifiers modifiers, VirtualKey key)
        {
            if (modifiers != VirtualKeyModifiers.Control) return false;
            return vimState switch
            {
                "normal" or "visual" => Normal.Contains(key),
                "reading" => Reading.Contains(key),
                "insert" or "replace" => key == LeftBracket,
                _ => false,
            };
        }
    }
}
