using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using KeySonic.Core.Audio;
using NAudio.Wave;

namespace KeySonic.Core.SoundPacks;

public sealed record CustomSoundPackImportResult(int ImportedFileCount, IReadOnlyList<string> FailedFiles);

public static class CustomSoundPackImporter
{
    public static CustomSoundPackImportResult ImportAudioFiles(
        IEnumerable<string> sourcePaths,
        string outputFolder,
        string packName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packName);
        Directory.CreateDirectory(outputFolder);

        int imported = 0;
        var failedFiles = new List<string>();
        foreach (string sourcePath in sourcePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!AudioFileExtensions.IsSupported(sourcePath))
            {
                failedFiles.Add(Path.GetFileName(sourcePath));
                continue;
            }

            string outputPath = Path.Combine(outputFolder, $"default_{imported + 1:000}.wav");
            try
            {
                var cachedSound = new CachedSound(sourcePath, SoundBank.EngineFormat);
                WaveFileWriter.CreateWaveFile16(outputPath, new CachedSoundSampleProvider(cachedSound, 1f));
                imported++;
            }
            catch
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
                failedFiles.Add(Path.GetFileName(sourcePath));
            }
        }

        if (imported == 0)
        {
            throw new InvalidDataException("No selected files could be decoded as supported audio.");
        }

        var metadata = new SoundPackMetadata
        {
            Name = packName,
            Description = "Imported custom audio files.",
            Version = "1.0.0"
        };
        File.WriteAllText(Path.Combine(outputFolder, "pack.json"), JsonSerializer.Serialize(metadata,
            new JsonSerializerOptions { WriteIndented = true }));
        return new CustomSoundPackImportResult(imported, failedFiles);
    }
}