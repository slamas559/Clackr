using KeySonic.Core.SoundPacks;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: dotnet run --project KeySonic.PackConverter -- \"<mechvibes-pack-folder>\" \"<output-pack-folder>\"");
    return 1;
}

try
{
    var result = MechvibesPackConverter.Convert(args[0], args[1]);
    Console.WriteLine($"Converted '{result.PackName}': {result.ConvertedSounds} sounds, {result.SkippedSounds} skipped.");
    Console.WriteLine($"Output: {args[1]}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Pack conversion failed: {ex.Message}");
    return 1;
}
