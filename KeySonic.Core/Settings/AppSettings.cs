namespace KeySonic.Core.Settings;

public sealed class AppSettings
{
    public bool StartMinimized { get; set; } = false;
    public float MasterVolume { get; set; } = 0.7f;
    public bool KeyboardSoundsEnabled { get; set; } = true;

    /// <summary>Folder name of the last active sound pack, so it's remembered across launches. Null = use the first installed pack.</summary>
    public string? ActivePackFolderName { get; set; } = null;
}
