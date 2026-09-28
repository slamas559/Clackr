using System;
using System.IO;
using System.Text.Json;

namespace KeySonic.Core.Settings;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string GetSettingsFilePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeySonic");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "settings.json");
    }

    /// <summary>Never throws - a missing or corrupt settings file just means "use defaults".</summary>
    public static AppSettings Load()
    {
        try
        {
            var path = GetSettingsFilePath();
            if (!File.Exists(path)) return new AppSettings();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    /// <summary>Never throws - a failed save (e.g. disk full, permissions) must never crash the app.</summary>
    public static void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(GetSettingsFilePath(), json);
        }
        catch
        {
            // Losing a settings write is far better than crashing a background audio utility.
        }
    }
}
