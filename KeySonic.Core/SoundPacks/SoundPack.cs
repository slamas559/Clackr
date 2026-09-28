using System;
using System.IO;
using System.Text.Json;
using KeySonic.Core.Audio;

namespace KeySonic.Core.SoundPacks;

/// <summary>
/// One folder under the packs root. Metadata is read immediately (cheap - it's a few
/// bytes of JSON) so the browser can list every installed pack instantly. The actual
/// audio (SoundBank) is only decoded the first time the pack is activated or previewed -
/// no point decoding every installed pack's audio just to show a list of names.
/// </summary>
public sealed class SoundPack
{
    public SoundPackMetadata Metadata { get; }
    public string FolderPath { get; }
    public SoundBank? Bank { get; private set; }
    public bool IsLoaded => Bank != null;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private SoundPack(string folderPath, SoundPackMetadata metadata)
    {
        FolderPath = folderPath;
        Metadata = metadata;
    }

    public static SoundPack FromFolder(string folderPath)
    {
        var manifestPath = Path.Combine(folderPath, "pack.json");
        SoundPackMetadata metadata;

        if (File.Exists(manifestPath))
        {
            try
            {
                var json = File.ReadAllText(manifestPath);
                metadata = JsonSerializer.Deserialize<SoundPackMetadata>(json, JsonOptions)
                           ?? new SoundPackMetadata { Name = Path.GetFileName(folderPath) };
            }
            catch (Exception)
            {
                // A malformed manifest must never break the whole pack list.
                metadata = new SoundPackMetadata
                {
                    Name = Path.GetFileName(folderPath),
                    Description = "Manifest could not be read"
                };
            }
        }
        else
        {
            // No manifest required - any folder of .wav files just works, named after the folder.
            // This keeps the system friendly for someone who just drops files in without writing JSON.
            metadata = new SoundPackMetadata
            {
                Name = Path.GetFileName(folderPath),
                Description = "Custom sounds"
            };
        }

        return new SoundPack(folderPath, metadata);
    }

    /// <summary>Decodes this pack's audio into memory if it hasn't been already. Safe to call repeatedly.</summary>
    public SoundBank EnsureLoaded()
    {
        if (Bank == null)
        {
            var bank = new SoundBank();
            bank.LoadFromFolder(FolderPath);
            Bank = bank;
        }
        return Bank;
    }
}
