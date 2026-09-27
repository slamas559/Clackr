using System.Collections.Generic;

namespace KeySonic.Core.Keyboard;

/// <summary>
/// The single place in the whole application that knows about raw Windows
/// virtual-key (VK_*) codes. Everything else consumes KeyCode.
/// </summary>
public static class VirtualKeyMapper
{
    private static readonly Dictionary<int, KeyCode> Map = new()
    {
        [0x41] = KeyCode.A, [0x42] = KeyCode.B, [0x43] = KeyCode.C, [0x44] = KeyCode.D,
        [0x45] = KeyCode.E, [0x46] = KeyCode.F, [0x47] = KeyCode.G, [0x48] = KeyCode.H,
        [0x49] = KeyCode.I, [0x4A] = KeyCode.J, [0x4B] = KeyCode.K, [0x4C] = KeyCode.L,
        [0x4D] = KeyCode.M, [0x4E] = KeyCode.N, [0x4F] = KeyCode.O, [0x50] = KeyCode.P,
        [0x51] = KeyCode.Q, [0x52] = KeyCode.R, [0x53] = KeyCode.S, [0x54] = KeyCode.T,
        [0x55] = KeyCode.U, [0x56] = KeyCode.V, [0x57] = KeyCode.W, [0x58] = KeyCode.X,
        [0x59] = KeyCode.Y, [0x5A] = KeyCode.Z,

        [0x30] = KeyCode.D0, [0x31] = KeyCode.D1, [0x32] = KeyCode.D2, [0x33] = KeyCode.D3,
        [0x34] = KeyCode.D4, [0x35] = KeyCode.D5, [0x36] = KeyCode.D6, [0x37] = KeyCode.D7,
        [0x38] = KeyCode.D8, [0x39] = KeyCode.D9,

        [0x20] = KeyCode.Space,
        [0x0D] = KeyCode.Enter,
        [0x08] = KeyCode.Backspace,
        [0x09] = KeyCode.Tab,
        [0x1B] = KeyCode.Escape,
        [0x14] = KeyCode.CapsLock,

        [0xA0] = KeyCode.LeftShift, [0xA1] = KeyCode.RightShift,
        [0xA2] = KeyCode.LeftCtrl, [0xA3] = KeyCode.RightCtrl,
        [0xA4] = KeyCode.LeftAlt, [0xA5] = KeyCode.RightAlt,
        [0x5B] = KeyCode.LeftWindows, [0x5C] = KeyCode.RightWindows,

        [0x26] = KeyCode.ArrowUp, [0x28] = KeyCode.ArrowDown,
        [0x25] = KeyCode.ArrowLeft, [0x27] = KeyCode.ArrowRight,

        [0xBC] = KeyCode.Comma, [0xBE] = KeyCode.Period, [0xBF] = KeyCode.Slash,
        [0xBA] = KeyCode.Semicolon, [0xDE] = KeyCode.Quote,
        [0xDB] = KeyCode.LeftBracket, [0xDD] = KeyCode.RightBracket,
        [0xDC] = KeyCode.Backslash, [0xBD] = KeyCode.Minus, [0xBB] = KeyCode.Equals,
        [0xC0] = KeyCode.Grave,

        [0x2E] = KeyCode.Delete, [0x2D] = KeyCode.Insert,
        [0x24] = KeyCode.Home, [0x23] = KeyCode.End,
        [0x21] = KeyCode.PageUp, [0x22] = KeyCode.PageDown,

        [0x70] = KeyCode.F1, [0x71] = KeyCode.F2, [0x72] = KeyCode.F3, [0x73] = KeyCode.F4,
        [0x74] = KeyCode.F5, [0x75] = KeyCode.F6, [0x76] = KeyCode.F7, [0x77] = KeyCode.F8,
        [0x78] = KeyCode.F9, [0x79] = KeyCode.F10, [0x7A] = KeyCode.F11, [0x7B] = KeyCode.F12,
    };

    public static KeyCode ToKeyCode(int virtualKeyCode)
        => Map.TryGetValue(virtualKeyCode, out var key) ? key : KeyCode.Unknown;
}
