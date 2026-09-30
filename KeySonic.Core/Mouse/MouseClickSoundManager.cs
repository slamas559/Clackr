using System;
using System.Collections.Generic;
using System.IO;
using KeySonic.Core.Audio;
using System.Linq;

namespace KeySonic.Core.Mouse;

public sealed record MouseClickSoundFile(string RelativePath, string DisplayName, string FullPath);

public sealed class MouseClickSoundManager
{
    private List<MouseClickSoundFile> _availableSounds = new();
    private readonly Dictionary<string, CachedSound> _soundCache = new(StringComparer.OrdinalIgnoreCase);
    private CachedSound? _leftSound;
    private CachedSound? _rightSound;
    private string? _leftSoundPath;
    private string? _rightSoundPath;

    public string FolderPath { get; private set; } = string.Empty;
    public IReadOnlyList<MouseClickSoundFile> AvailableSounds => _availableSounds;
    public string? LeftSoundPath => _leftSoundPath;
    public string? RightSoundPath => _rightSoundPath;

    public void DiscoverSounds(string folderPath)
    {
        string? leftPath = _leftSoundPath;
        string? rightPath = _rightSoundPath;
        FolderPath = folderPath;
        Directory.CreateDirectory(FolderPath);
        _soundCache.Clear();
        _availableSounds = Directory.EnumerateFiles(FolderPath, "*", SearchOption.AllDirectories)
            .Where(IsSupportedAudioFile)
            .Select(path =>
            {
                string relativePath = Path.GetRelativePath(FolderPath, path);
                return new MouseClickSoundFile(relativePath, relativePath, path);
            })
            .OrderBy(sound => sound.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _leftSound = null;
        _rightSound = null;

        if (leftPath != null && !TrySetSound(MouseButton.Left, leftPath, out _))
        {
            ClearSound(MouseButton.Left);
        }

        if (rightPath != null && !TrySetSound(MouseButton.Right, rightPath, out _))
        {
            ClearSound(MouseButton.Right);
        }
    }

    public bool TrySetSound(MouseButton button, string? relativePath, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            ClearSound(button);
            return true;
        }

        var soundFile = _availableSounds.FirstOrDefault(sound =>
            string.Equals(sound.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
        if (soundFile == null)
        {
            error = "The selected sound file is no longer in the mouse sounds folder.";
            return false;
        }

        if (!TryLoadSound(relativePath, out var sound, out error) || sound == null)
        {
            return false;
        }

        if (button == MouseButton.Left)
        {
            _leftSound = sound;
            _leftSoundPath = soundFile.RelativePath;
        }
        else
        {
            _rightSound = sound;
            _rightSoundPath = soundFile.RelativePath;
        }

        return true;
    }

    public bool TryLoadSound(string? relativePath, out CachedSound? sound, out string? error)
    {
        sound = null;
        error = null;
        if (string.IsNullOrWhiteSpace(relativePath)) return false;

        var soundFile = _availableSounds.FirstOrDefault(file =>
            string.Equals(file.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
        if (soundFile == null)
        {
            error = "The selected sound file is no longer in the mouse sounds folder.";
            return false;
        }

        if (_soundCache.TryGetValue(soundFile.RelativePath, out sound)) return true;

        try
        {
            sound = new CachedSound(soundFile.FullPath, SoundBank.EngineFormat);
            _soundCache[soundFile.RelativePath] = sound;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Couldn't load '{soundFile.DisplayName}': {ex.Message}";
            return false;
        }
    }

    public CachedSound? GetSound(MouseButton button) => button == MouseButton.Left ? _leftSound : _rightSound;

    public void ClearSound(MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            _leftSound = null;
            _leftSoundPath = null;
        }
        else
        {
            _rightSound = null;
            _rightSoundPath = null;
        }
    }

    private static bool IsSupportedAudioFile(string path)
        => AudioFileExtensions.IsSupported(path);
}