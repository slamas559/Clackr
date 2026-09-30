using System;
using System.Collections.Generic;
using System.Linq;
using KeySonic.Core.Keyboard;
using NAudio.Wave;

namespace KeySonic.Core.Audio;

/// <summary>
/// Phase-1 stand-in for the future sound-pack system. Just a folder of wav files
/// loaded once at startup: everything in the folder is treated as one interchangeable
/// pool of "default key" sounds, except for a few special keys with their own files
/// if present (space.wav, enter.wav, backspace.wav). No JSON manifest yet - that's
/// Phase 6 territory. This exists purely to prove the hook -> audio pipeline works.
/// </summary>
public sealed class SoundBank
{
    private readonly List<CachedSound> _defaultVariations = new();
    private readonly Dictionary<KeyCode, List<CachedSound>> _specialSounds = new();
    private readonly Dictionary<KeyCode, int> _lastVariationIndex = new();
    private readonly Random _random = new();

    public static readonly WaveFormat EngineFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    public int LoadedDefaultSoundCount => _defaultVariations.Count;

    public void LoadFromFolder(string folderPath)
    {
        if (!System.IO.Directory.Exists(folderPath))
            throw new System.IO.DirectoryNotFoundException($"Sound folder not found: {folderPath}");

        foreach (var file in System.IO.Directory.EnumerateFiles(folderPath))
        {
            if (!AudioFileExtensions.IsSupported(file)) continue;
            var name = System.IO.Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
            try
            {
                var sound = new CachedSound(file, EngineFormat);

                switch (name)
                {
                    case "space": AddSpecial(KeyCode.Space, sound); break;
                    case "enter": AddSpecial(KeyCode.Enter, sound); break;
                    case "backspace": AddSpecial(KeyCode.Backspace, sound); break;
                    default: _defaultVariations.Add(sound); break;
                }
            }
            catch (Exception ex)
            {
                // A single bad/corrupt file must never take down the whole sound bank.
                Console.Error.WriteLine($"[SoundBank] Failed to load '{file}': {ex.Message}");
            }
        }

        if (_defaultVariations.Count == 0)
            throw new InvalidOperationException($"No usable default key sounds loaded from {folderPath}");
    }

    private void AddSpecial(KeyCode key, CachedSound sound)
    {
        if (!_specialSounds.TryGetValue(key, out var list))
        {
            list = new List<CachedSound>();
            _specialSounds[key] = list;
        }
        list.Add(sound);
    }

    /// <summary>
    /// Picks a sound for the given key: its dedicated special sound if one is loaded,
    /// otherwise a random pick from the default pool that avoids repeating the exact
    /// same sample twice in a row (when more than one variation exists).
    /// </summary>
    public CachedSound PickSound(KeyCode key)
    {
        var pool = _specialSounds.TryGetValue(key, out var special) ? special : _defaultVariations;

        if (pool.Count == 1) return pool[0];

        int lastIndex = _lastVariationIndex.GetValueOrDefault(key, -1);
        int index;
        do
        {
            index = _random.Next(pool.Count);
        } while (index == lastIndex);

        _lastVariationIndex[key] = index;
        return pool[index];
    }
}
