using System;
using Microsoft.Win32;

namespace KeySonic.Core.Settings;

/// <summary>
/// Registers/unregisters KeySonic in the current user's startup entries via the
/// standard HKCU Run key. This is the same mechanism most lightweight Windows
/// utilities use - no installer or scheduled task needed, and it doesn't require
/// admin rights since it's per-user, not machine-wide.
/// </summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "KeySonic";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string;
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;
                key.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Registry writes can legitimately fail (policy restrictions, etc.) - the
            // toggle should reflect reality, not crash the app.
        }
    }
}
