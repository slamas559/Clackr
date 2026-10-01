namespace KeySonic.Core.Settings;

public sealed class SoundProfile
{
    public string Name { get; set; } = "";
    public string TargetProcessName { get; set; } = "";
    public string? ActivePackFolderName { get; set; }
    public float MasterVolume { get; set; } = 0.7f;
    public bool KeyboardSoundsEnabled { get; set; } = true;
    public bool MouseSoundsEnabled { get; set; } = true;
    public float LeftMouseClickVolume { get; set; } = 0.7f;
    public float RightMouseClickVolume { get; set; } = 0.7f;
    public string? LeftMouseClickSoundPath { get; set; }
    public string? RightMouseClickSoundPath { get; set; }

    public override string ToString() => Name;
}