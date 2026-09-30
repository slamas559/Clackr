using System.Collections.Generic;

namespace KeySonic.Core.Settings;

public sealed class AppSettings
{
    public bool StartMinimized { get; set; } = false;
    public bool AutoSwitchProfilesEnabled { get; set; }
    public double WindowWidth { get; set; } = 1060;
    public double WindowHeight { get; set; } = 720;
    public float MasterVolume { get; set; } = 0.7f;
    public bool KeyboardSoundsEnabled { get; set; } = true;
    public bool MouseSoundsEnabled { get; set; } = true;
    public float LeftMouseClickVolume { get; set; } = 0.7f;
    public float RightMouseClickVolume { get; set; } = 0.7f;
    public string? LeftMouseClickSoundPath { get; set; }
    public string? RightMouseClickSoundPath { get; set; }
    public List<SoundProfile> SoundProfiles { get; set; } = new();

    /// <summary>Folder name of the last active sound pack, so it's remembered across launches. Null = use the first installed pack.</summary>
    public string? ActivePackFolderName { get; set; } = null;
    public string? ActiveSoundProfileName { get; set; }
}
