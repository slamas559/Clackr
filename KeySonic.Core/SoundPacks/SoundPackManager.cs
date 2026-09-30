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

    public void DiscoverPacks(params string[] packsRootFolders)
    {
        string? activeFolder = ActivePack?.FolderPath;
        string[] existingRoots = packsRootFolders.Where(Directory.Exists).ToArray();
        if (existingRoots.Length == 0)
        {
            throw new DirectoryNotFoundException("No sound packs folders were found.");
        }

        var packs = new List<SoundPack>();
        var discoveredFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in existingRoots.SelectMany(Directory.EnumerateDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (!discoveredFolders.Add(Path.GetFullPath(dir)) ||
                !Directory.EnumerateFiles(dir).Any(AudioFileExtensions.IsSupported)) continue;

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
        if (activeFolder != null)
        {
            var activeReplacement = InstalledPacks.FirstOrDefault(pack =>
                string.Equals(pack.FolderPath, activeFolder, StringComparison.OrdinalIgnoreCase));
            if (activeReplacement != null)
            {
                Activate(activeReplacement);
            }
        }
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
