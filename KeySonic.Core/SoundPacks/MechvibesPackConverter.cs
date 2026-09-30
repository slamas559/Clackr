using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using KeySonic.Core.Keyboard;
using NAudio.Vorbis;
using NAudio.Wave;

namespace KeySonic.Core.SoundPacks;

public sealed record PackConversionResult(string PackName, int ConvertedSounds, int SkippedSounds);

public static class MechvibesPackConverter
{
    public static PackConversionResult Convert(string inputFolder, string outputFolder)
    {
        if (!Directory.Exists(inputFolder))
        {
            throw new DirectoryNotFoundException($"Input folder not found: {inputFolder}");
        }

        string configPath = Path.Combine(inputFolder, "config.json");
        if (!File.Exists(configPath))
        {
            throw new InvalidDataException($"No config.json found in: {inputFolder}");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        var root = document.RootElement;
        string packName = root.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString() ?? Path.GetFileName(inputFolder)
            : Path.GetFileName(inputFolder);
        string? sharedFileName = root.TryGetProperty("sound", out var soundElement)
            ? soundElement.GetString()
            : null;
        string? sharedSoundPath = string.IsNullOrWhiteSpace(sharedFileName)
            ? null
            : Path.GetFullPath(Path.Combine(inputFolder, sharedFileName));

        if (!root.TryGetProperty("defines", out var defines) || defines.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("config.json has no 'defines' object in a supported Mechvibes format.");
        }

        Directory.CreateDirectory(outputFolder);
        int defaultIndex = 1;
        int converted = 0;
        int skipped = 0;

        foreach (var definition in defines.EnumerateObject())
        {
            if (!int.TryParse(definition.Name, out int keyCode))
            {
                skipped++;
                continue;
            }

            string category = keyCode switch
            {
                32 => "space",
                13 => "enter",
                8 => "backspace",
                _ => "default"
            };
            string outputName = category == "default" ? $"default_{defaultIndex++:000}.wav" : $"{category}.wav";
            string outputPath = Path.Combine(outputFolder, outputName);

            try
            {
                if (definition.Value.ValueKind == JsonValueKind.Array)
                {
                    if (sharedSoundPath == null || !File.Exists(sharedSoundPath))
                    {
                        skipped++;
                        continue;
                    }

                    int[] timing = definition.Value.EnumerateArray().Select(value => value.GetInt32()).ToArray();
                    if (timing.Length < 2 || timing[0] < 0 || timing[1] <= 0)
                    {
                        skipped++;
                        continue;
                    }

                    ExtractSlice(sharedSoundPath, timing[0], timing[1], outputPath);
                    converted++;
                }
                else if (definition.Value.ValueKind == JsonValueKind.String)
                {
                    string? sourceName = definition.Value.GetString();
                    if (string.IsNullOrWhiteSpace(sourceName))
                    {
                        skipped++;
                        continue;
                    }

                    string sourcePath = Path.GetFullPath(Path.Combine(inputFolder, sourceName));
                    if (!sourcePath.StartsWith(Path.GetFullPath(inputFolder) + Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase) || !File.Exists(sourcePath))
                    {
                        skipped++;
                        continue;
                    }

                    ConvertWholeFile(sourcePath, outputPath);
                    converted++;
                }
                else
                {
                    skipped++;
                }
            }
            catch
            {
                skipped++;
                if (File.Exists(outputPath)) File.Delete(outputPath);
            }
        }

        if (converted == 0)
        {
            throw new InvalidDataException("No usable sounds were converted from this Mechvibes pack.");
        }

        File.WriteAllText(Path.Combine(outputFolder, "pack.json"), JsonSerializer.Serialize(
            new SoundPackMetadata
            {
                Name = packName,
                Description = "Converted from a Mechvibes sound pack.",
                Author = "Mechvibes community (converted)",
                Version = "1.0.0"
            }, new JsonSerializerOptions { WriteIndented = true }));

        return new PackConversionResult(packName, converted, skipped);
    }

    private static void ExtractSlice(string sourcePath, int offsetMilliseconds, int durationMilliseconds, string outputPath)
    {
        using var reader = new VorbisWaveReader(sourcePath);
        reader.CurrentTime = TimeSpan.FromMilliseconds(offsetMilliseconds);
        TimeSpan endTime = TimeSpan.FromMilliseconds((long)offsetMilliseconds + durationMilliseconds);
        using var writer = new WaveFileWriter(outputPath, reader.WaveFormat);
        var buffer = new byte[Math.Max(reader.WaveFormat.AverageBytesPerSecond / 4, 4096)];

        while (reader.CurrentTime < endTime)
        {
            int bytesRead = reader.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0) break;
            writer.Write(buffer, 0, bytesRead);
        }
    }

    private static void ConvertWholeFile(string sourcePath, string outputPath)
    {
        if (Path.GetExtension(sourcePath).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new VorbisWaveReader(sourcePath);
            WaveFileWriter.CreateWaveFile(outputPath, reader);
            return;
        }

        using var audioReader = new AudioFileReader(sourcePath);
        WaveFileWriter.CreateWaveFile16(outputPath, audioReader);
    }
}