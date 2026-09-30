using System;

namespace KeySonic.Core.Mouse;

public enum MouseButton
{
    Left,
    Right
}

public sealed class MouseButtonEventArgs : EventArgs
{
    public MouseButton Button { get; }

    public MouseButtonEventArgs(MouseButton button)
    {
        Button = button;
    }
}