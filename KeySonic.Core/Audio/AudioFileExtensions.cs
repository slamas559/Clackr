using System;
using System.Collections.Generic;
using System.IO;

namespace KeySonic.Core.Audio;

public static class AudioFileExtensions
{
    public static IReadOnlyList<string> Supported { get; } = Array.AsReadOnly(new[]
    {
        ".wav", ".mp3", ".ogg", ".aiff", ".aif"
    });

    public static bool IsSupported(string path)
    {
        string extension = Path.GetExtension(path);
        foreach (string supportedExtension in Supported)
        {
            if (extension.Equals(supportedExtension, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}