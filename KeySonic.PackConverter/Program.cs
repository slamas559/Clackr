using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NAudio.Vorbis;
using NAudio.Wave;

// KeySonic Mechvibes pack converter
//
// Mechvibes packs from mechvibes.com come in two shapes, both described by a
// config.json:
//   1. "Sprite" packs (the common case): one shared audio file (usually sound.ogg)
//      plus "defines": { "keycode": [offsetMs, durationMs], ... } - each key is a
//      time-sliced region of that one file, like a sprite sheet for audio.
//   2. "Multi-file" packs: "defines": { "keycode": "somefile.ogg", ... } - a
//      separate audio file per key, no slicing needed.
//
// This tool reads either shape and writes out individual .wav files plus a
// pack.json in KeySonic's own format (one folder = one pack, flat pool of
// default-key variations plus optional space/enter/backspace files), because
// KeySonic's audio engine plays whole preloaded files rather than slicing a
// shared buffer at runtime.
//
// Usage:
//   dotnet run --project KeySonic.PackConverter -- "<mechvibes-pack-folder>" "<output-pack-folder>"
//
// Note: every distinct key sound from the source pack gets dropped into one
// randomized "default" pool (except space/enter/backspace, which are kept
// dedicated) - this is a deliberate simplification to match KeySonic's current
// flat sound-pack format, not a bug. A full per-key mapping is a bigger future
// upgrade to the pack system itself.

if (args.Length < 2)
{
    Console.WriteLine("Usage: dotnet run --project KeySonic.PackConverter -- \"<mechvibes-pack-folder>\" \"<output-pack-folder>\"");
    return 1;
}

var inputFolder = args[0];
var outputFolder = args[1];

if (!Directory.Exists(inputFolder))
{
    Console.Error.WriteLine($"Input folder not found: {inputFolder}");
    return 1;
}

var configPath = Path.Combine(inputFolder, "config.json");
if (!File.Exists(configPath))
{
    Console.Error.WriteLine($"No config.json found in: {inputFolder}");
    Console.Error.WriteLine("This doesn't look like a Mechvibes pack folder.");
    return 1;
}

Directory.CreateDirectory(outputFolder);

using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
var root = doc.RootElement;

string packName = root.TryGetProperty("name", out var nameEl) ? (nameEl.GetString() ?? "Converted Pack") : "Converted Pack";
string? sharedSoundFile = root.TryGetProperty("sound", out var soundEl) ? soundEl.GetString() : null;
string? sharedSoundPath = string.IsNullOrEmpty(sharedSoundFile) ? null : Path.Combine(inputFolder, sharedSoundFile);

if (!root.TryGetProperty("defines", out var defines) || defines.ValueKind != JsonValueKind.Object)
{
    Console.Error.WriteLine("config.json has no 'defines' object - not a recognized Mechvibes pack format.");
    return 1;
}

Console.WriteLine($"Converting '{packName}'...");

int defaultIndex = 1;
int converted = 0;
int skipped = 0;

foreach (var prop in defines.EnumerateObject())
{
    if (!int.TryParse(prop.Name, out int keycode))
    {
        skipped++;
        continue;
    }

    string category = keycode switch
    {
        32 => "space",
        13 => "enter",
        8 => "backspace",
        _ => "default"
    };

    string outFileName = category == "default" ? $"default_{defaultIndex++:000}.wav" : $"{category}.wav";
    string outPath = Path.Combine(outputFolder, outFileName);

    try
    {
        if (prop.Value.ValueKind == JsonValueKind.Array)
        {
            // Sprite mode: [offsetMs, durationMs] into the shared audio file.
            if (sharedSoundPath == null || !File.Exists(sharedSoundPath))
            {
                Console.Error.WriteLine($"  Key {prop.Name}: shared sound file not found, skipped.");
                skipped++;
                continue;
            }

            var timing = prop.Value.EnumerateArray().Select(x => x.GetInt32()).ToArray();
            if (timing.Length < 2)
            {
                skipped++;
                continue;
            }

            ExtractSlice(sharedSoundPath, timing[0], timing[1], outPath);
            converted++;
        }
        else if (prop.Value.ValueKind == JsonValueKind.String)
        {
            // Multi-file mode: a separate audio file per key.
            var fileName = prop.Value.GetString();
            if (string.IsNullOrEmpty(fileName))
            {
                skipped++;
                continue;
            }

            var srcPath = Path.Combine(inputFolder, fileName);
            if (!File.Exists(srcPath))
            {
                Console.Error.WriteLine($"  Key {prop.Name}: '{fileName}' not found, skipped.");
                skipped++;
                continue;
            }

            ConvertWholeFile(srcPath, outPath);
            converted++;
        }
        else
        {
            skipped++;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  Key {prop.Name}: {ex.Message}");
        skipped++;
    }
}

var escapedName = packName.Replace("\"", "'");
var packJson =
    "{\n" +
    $"  \"name\": \"{escapedName}\",\n" +
    "  \"description\": \"Converted from a Mechvibes sound pack.\",\n" +
    "  \"author\": \"Mechvibes community (converted)\",\n" +
    "  \"version\": \"1.0.0\"\n" +
    "}\n";
File.WriteAllText(Path.Combine(outputFolder, "pack.json"), packJson);

Console.WriteLine($"Done. {converted} sounds converted, {skipped} skipped.");
Console.WriteLine($"Output: {outputFolder}");
Console.WriteLine("Copy that folder into your KeySonic packs\\ folder (or it may already be there) and restart KeySonic.");
return 0;

// ===== Helpers =====

static void ExtractSlice(string sourceOggPath, int offsetMs, int durationMs, string outputWavPath)
{
    using var reader = new VorbisWaveReader(sourceOggPath);
    reader.CurrentTime = TimeSpan.FromMilliseconds(offsetMs);
    var endTime = TimeSpan.FromMilliseconds(offsetMs + durationMs);

    using var writer = new WaveFileWriter(outputWavPath, reader.WaveFormat);
    var buffer = new byte[reader.WaveFormat.AverageBytesPerSecond / 4];

    while (reader.CurrentTime < endTime)
    {
        int bytesRead = reader.Read(buffer, 0, buffer.Length);
        if (bytesRead == 0) break;
        writer.Write(buffer, 0, bytesRead);
    }
}

static void ConvertWholeFile(string sourcePath, string outputWavPath)
{
    if (Path.GetExtension(sourcePath).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
    {
        using var reader = new VorbisWaveReader(sourcePath);
        WaveFileWriter.CreateWaveFile(outputWavPath, reader);
    }
    else
    {
        using var reader = new AudioFileReader(sourcePath);
        WaveFileWriter.CreateWaveFile16(outputWavPath, reader);
    }
}
