using System;

namespace Typedown.Core.Enums
{
    /// <summary>
    /// Cross-platform virtual key codes. Maps 1:1 with Windows.System.VirtualKey values
    /// for serialization compatibility, but no longer depends on WinRT.
    /// </summary>
    public enum VirtualKey
    {
        None = 0,
        Back = 8,
        Tab = 9,
        Enter = 13,
        Shift = 16,
        Control = 17,
        Menu = 18,    // Alt
        Pause = 19,
        Capital = 20,
        Escape = 27,
        Space = 32,
        PageUp = 33,
        PageDown = 34,
        End = 35,
        Home = 36,
        Left = 37,
        Up = 38,
        Right = 39,
        Down = 40,
        Insert = 45,
        Delete = 46,
        Number0 = 48,
        Number1 = 49,
        Number2 = 50,
        Number3 = 51,
        Number4 = 52,
        Number5 = 53,
        Number6 = 54,
        Number7 = 55,
        Number8 = 56,
        Number9 = 57,
        A = 65, B = 66, C = 67, D = 68, E = 69, F = 70, G = 71,
        H = 72, I = 73, J = 74, K = 75, L = 76, M = 77, N = 78,
        O = 79, P = 80, Q = 81, R = 82, S = 83, T = 84, U = 85,
        V = 86, W = 87, X = 88, Y = 89, Z = 90,
        F1 = 112, F2 = 113, F3 = 114, F4 = 115, F5 = 116, F6 = 117,
        F7 = 118, F8 = 119, F9 = 120, F10 = 121, F11 = 122, F12 = 123,
    }

    /// <summary>
    /// Cross-platform keyboard modifier flags.
    /// Maps 1:1 with Windows.System.VirtualKeyModifiers.
    /// </summary>
    [Flags]
    public enum VirtualKeyModifiers
    {
        None = 0,
        Control = 1,
        Menu = 2,      // Alt
        Shift = 4,
    }
}
