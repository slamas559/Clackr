using System;

namespace KeySonic.Core.Keyboard;

public readonly struct KeyEventData
{
    public KeyCode Key { get; }
    public bool IsRepeat { get; }
    public DateTime TimestampUtc { get; }

    public KeyEventData(KeyCode key, bool isRepeat, DateTime timestampUtc)
    {
        Key = key;
        IsRepeat = isRepeat;
        TimestampUtc = timestampUtc;
    }
}
