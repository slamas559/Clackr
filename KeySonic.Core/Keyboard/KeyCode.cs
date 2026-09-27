namespace KeySonic.Core.Keyboard;

/// <summary>
/// Normalized representation of a keyboard key. Everything outside this file
/// deals only in KeyCode, never in raw Windows virtual-key codes.
/// </summary>
public enum KeyCode
{
    Unknown = 0,

    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,

    Space, Enter, Backspace, Tab, Escape, CapsLock,

    LeftShift, RightShift, LeftCtrl, RightCtrl, LeftAlt, RightAlt,
    LeftWindows, RightWindows,

    ArrowUp, ArrowDown, ArrowLeft, ArrowRight,

    Comma, Period, Slash, Semicolon, Quote, LeftBracket, RightBracket,
    Backslash, Minus, Equals, Grave,

    Delete, Insert, Home, End, PageUp, PageDown,

    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12
}
