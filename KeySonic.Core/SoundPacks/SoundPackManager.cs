using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KeySonic.Core.Audio;

namespace KeySonic.Core.SoundPacks;

public sealed class SoundPackManager
{
    public IReadOnlyList<SoundPack> InstalledPacks { get; private set; } = Array.Empty<SoundPack>();
    public SoundPack? ActivePack { get; private set; }

    /// <summary>Fires whenever a different pack becomes active, so UI can refresh without polling.</summary>
    public event Action<SoundPack>? ActivePackChanged;

    public void DiscoverPacks(string packsRootFolder)
    {
        if (!Directory.Exists(packsRootFolder))
            throw new DirectoryNotFoundException($"Sound packs folder not found: {packsRootFolder}");

        var packs = new List<SoundPack>();
        foreach (var dir in Directory.EnumerateDirectories(packsRootFolder).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            bool hasWav = Directory.EnumerateFiles(dir, "*.wav").Any();
            if (!hasWav) continue; // skip empty/unrelated folders rather than showing a broken pack

            try
            {
                packs.Add(SoundPack.FromFolder(dir));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SoundPackManager] Skipped '{dir}': {ex.Message}");
            }
        }

        InstalledPacks = packs;
    }

    /// <summary>Loads (if needed) and switches to the given pack. The pack must be one returned by DiscoverPacks.</summary>
    public SoundBank Activate(SoundPack pack)
    {
        if (!InstalledPacks.Contains(pack))
            throw new ArgumentException("This pack isn't part of the currently discovered set.", nameof(pack));

        var bank = pack.EnsureLoaded();
        ActivePack = pack;
        ActivePackChanged?.Invoke(pack);
        return bank;
    }
}
