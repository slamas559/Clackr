using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Forms;
using KeySonic.Core.Audio;
using KeySonic.Core.Keyboard;
using KeySonic.Core.Mouse;
using KeySonic.Core.Settings;
using KeySonic.Core.SoundPacks;
using Application = System.Windows.Application;

namespace KeySonic.UI;

public partial class App : Application
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    public SoundPackManager PackManager { get; private set; } = null!;
    public AudioEngine AudioEngine { get; private set; } = null!;
    public GlobalKeyboardHook KeyboardHook { get; private set; } = null!;
    public GlobalMouseHook MouseHook { get; private set; } = null!;
    public MouseClickSoundManager MouseClickSounds { get; private set; } = null!;
    public AppSettings Settings { get; private set; } = null!;
    public string PacksFolderPath { get; private set; } = "";
    public string UserPacksFolderPath { get; private set; } = "";
    public string MouseClicksFolderPath { get; private set; } = "";

    private NotifyIcon? _trayIcon;
    private Icon? _trayIconImage;
    private MainWindow? _mainWindow;
    private DispatcherTimer? _profileSwitchTimer;

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogCrash("AppDomain.UnhandledException", args.ExceptionObject as Exception);

        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash("DispatcherUnhandledException", args.Exception);
            args.Handled = true; // keep the app alive long enough to show the message box below
            System.Windows.MessageBox.Show(
                $"Clackr encountered an error:\n\n{args.Exception}",
                "Clackr - Unhandled error", MessageBoxButton.OK, MessageBoxImage.Error);
        };
    }

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            var logFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeySonic", "Logs");
            Directory.CreateDirectory(logFolder);
            var path = Path.Combine(logFolder, "crash-log.txt");
            File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{source}]\n{ex}\n\n");
        }
        catch { /* logging must never itself throw */ }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Settings = SettingsStore.Load();
        Settings.SoundProfiles ??= new();

        MouseClicksFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeySonic", "mouse-clicks");
        MouseClickSounds = new MouseClickSoundManager();
        try
        {
            MouseClickSounds.DiscoverSounds(MouseClicksFolderPath);
            MouseClickSounds.TrySetSound(MouseButton.Left, Settings.LeftMouseClickSoundPath, out _);
            MouseClickSounds.TrySetSound(MouseButton.Right, Settings.RightMouseClickSoundPath, out _);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MOUSE SOUNDS ERROR] {ex}");
        }

        PackManager = new SoundPackManager();
        PacksFolderPath = Path.Combine(AppContext.BaseDirectory, "packs");
        UserPacksFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeySonic", "packs");
        Directory.CreateDirectory(UserPacksFolderPath);

        try
        {
            PackManager.DiscoverPacks(PacksFolderPath, UserPacksFolderPath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Couldn't read the sound packs folder:\n{PacksFolderPath}\n\n{ex.Message}",
                "Clackr - No sound packs found",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        if (PackManager.InstalledPacks.Count == 0)
        {
            System.Windows.MessageBox.Show(
                $"No sound packs found in:\n{PacksFolderPath}\nor:\n{UserPacksFolderPath}\n\n" +
                "Import a pack from the Sound Packs page to get started.",
                "Clackr - No sound packs found",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // Restore the pack the user had active last time, falling back to the first
        // installed pack if it's gone or this is the first launch.
        var packToActivate = PackManager.InstalledPacks[0];
        if (!string.IsNullOrEmpty(Settings.ActivePackFolderName))
        {
            var remembered = PackManager.InstalledPacks.FirstOrDefault(p =>
                string.Equals(Path.GetFileName(p.FolderPath), Settings.ActivePackFolderName, StringComparison.OrdinalIgnoreCase));
            if (remembered != null) packToActivate = remembered;
        }

        try
        {
            PackManager.Activate(packToActivate);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Couldn't load the sound pack '{packToActivate.Metadata.Name}': {ex.Message}",
                "Clackr - Pack load failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        PackManager.ActivePackChanged += pack =>
        {
            Settings.ActivePackFolderName = Path.GetFileName(pack.FolderPath);
            SettingsStore.Save(Settings);
        };

        AudioEngine = new AudioEngine
        {
            MasterVolume = Settings.MasterVolume,
            Enabled = Settings.KeyboardSoundsEnabled
        };

        KeyboardHook = new GlobalKeyboardHook();
        KeyboardHook.KeyDown += OnGlobalKeyDown;
        KeyboardHook.HookError += (_, ex) =>
            System.Diagnostics.Debug.WriteLine($"[HOOK ERROR] {ex}");
        KeyboardHook.Start();

        MouseHook = new GlobalMouseHook();
        MouseHook.ButtonDown += OnGlobalMouseButtonDown;
        MouseHook.HookError += (_, ex) =>
            System.Diagnostics.Debug.WriteLine($"[MOUSE HOOK ERROR] {ex}");
        MouseHook.Start();

        _mainWindow = new MainWindow();
        _mainWindow.ApplySettingsOnLoad(
            Settings.MasterVolume, Settings.KeyboardSoundsEnabled, Settings.WindowWidth, Settings.WindowHeight);
        _profileSwitchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _profileSwitchTimer.Tick += (_, _) => ApplyForegroundAppProfile();
        _profileSwitchTimer.Start();
        SetupTrayIcon();

        if (!Settings.StartMinimized)
        {
            _mainWindow.Show();
        }
    }

    private void OnGlobalKeyDown(object? sender, KeyEventData e)
    {
        if (e.IsRepeat) return;

        var bank = PackManager.ActivePack?.Bank;
        if (bank == null) return;

        var sound = bank.PickSound(e.Key);
        AudioEngine.Play(sound);
    }

    private void OnGlobalMouseButtonDown(object? sender, MouseButtonEventArgs e)
    {
        if (!Settings.MouseSoundsEnabled) return;

        var sound = MouseClickSounds.GetSound(e.Button);
        if (sound == null) return;

        float volume = e.Button == MouseButton.Left
            ? Settings.LeftMouseClickVolume
            : Settings.RightMouseClickVolume;
        AudioEngine.PlayMouseClick(sound, volume);
    }

    private void ApplyForegroundAppProfile()
    {
        if (!Settings.AutoSwitchProfilesEnabled) return;

        IntPtr foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero) return;
        GetWindowThreadProcessId(foregroundWindow, out uint processId);
        if (processId == 0 || processId == Environment.ProcessId) return;

        try
        {
            using var process = Process.GetProcessById((int)processId);
            string processName = process.ProcessName;
            var profile = Settings.SoundProfiles.FirstOrDefault(candidate =>
                string.Equals(NormalizeProcessName(candidate.TargetProcessName), processName,
                    StringComparison.OrdinalIgnoreCase));
            if (profile == null || string.Equals(Settings.ActiveSoundProfileName, profile.Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (ApplySoundProfile(profile, out var warning))
            {
                _mainWindow?.RefreshActivePackDisplay();
            }
            else if (!string.IsNullOrWhiteSpace(warning))
            {
                Debug.WriteLine($"[PROFILE SWITCH] {warning}");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Debug.WriteLine($"[PROFILE SWITCH] Couldn't inspect foreground process: {ex.Message}");
        }
    }

    private static string NormalizeProcessName(string? processName) =>
        string.IsNullOrWhiteSpace(processName) ? string.Empty : Path.GetFileNameWithoutExtension(processName.Trim());

    public SoundProfile CaptureSoundProfile(string name) => new()
    {
        Name = name,
        ActivePackFolderName = PackManager.ActivePack == null
            ? null
            : Path.GetFileName(PackManager.ActivePack.FolderPath),
        MasterVolume = AudioEngine.MasterVolume,
        KeyboardSoundsEnabled = AudioEngine.Enabled,
        MouseSoundsEnabled = Settings.MouseSoundsEnabled,
        LeftMouseClickVolume = Settings.LeftMouseClickVolume,
        RightMouseClickVolume = Settings.RightMouseClickVolume,
        LeftMouseClickSoundPath = Settings.LeftMouseClickSoundPath,
        RightMouseClickSoundPath = Settings.RightMouseClickSoundPath
    };

    public bool ApplySoundProfile(SoundProfile profile, out string? warning)
    {
        warning = null;
        var pack = PackManager.InstalledPacks.FirstOrDefault(candidate =>
            string.Equals(Path.GetFileName(candidate.FolderPath), profile.ActivePackFolderName,
                StringComparison.OrdinalIgnoreCase));
        if (pack == null)
        {
            warning = $"The keyboard pack for '{profile.Name}' is not installed.";
            return false;
        }

        try
        {
            PackManager.Activate(pack);
        }
        catch (Exception ex)
        {
            warning = $"Couldn't activate '{pack.Metadata.Name}': {ex.Message}";
            return false;
        }

        Settings.ActivePackFolderName = Path.GetFileName(pack.FolderPath);
        Settings.MasterVolume = NormalizeVolume(profile.MasterVolume, 0.7f);
        Settings.KeyboardSoundsEnabled = profile.KeyboardSoundsEnabled;
        Settings.MouseSoundsEnabled = profile.MouseSoundsEnabled;
        Settings.LeftMouseClickVolume = NormalizeVolume(profile.LeftMouseClickVolume, 0.7f);
        Settings.RightMouseClickVolume = NormalizeVolume(profile.RightMouseClickVolume, 0.7f);
        Settings.ActiveSoundProfileName = profile.Name;

        if (!MouseClickSounds.TrySetSound(MouseButton.Left, profile.LeftMouseClickSoundPath, out var leftError))
        {
            MouseClickSounds.ClearSound(MouseButton.Left);
            Settings.LeftMouseClickSoundPath = null;
            warning = leftError;
        }
        else
        {
            Settings.LeftMouseClickSoundPath = profile.LeftMouseClickSoundPath;
        }

        if (!MouseClickSounds.TrySetSound(MouseButton.Right, profile.RightMouseClickSoundPath, out var rightError))
        {
            MouseClickSounds.ClearSound(MouseButton.Right);
            Settings.RightMouseClickSoundPath = null;
            warning = string.Join(Environment.NewLine, new[] { warning, rightError }
                .Where(message => !string.IsNullOrWhiteSpace(message)));
        }
        else
        {
            Settings.RightMouseClickSoundPath = profile.RightMouseClickSoundPath;
        }

        AudioEngine.MasterVolume = Settings.MasterVolume;
        AudioEngine.Enabled = Settings.KeyboardSoundsEnabled;
        SettingsStore.Save(Settings);
        return true;
    }

    private static float NormalizeVolume(float value, float fallback) =>
        float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : fallback;

    private void SetupTrayIcon()
    {
        string? executablePath = Environment.ProcessPath;
        _trayIconImage = executablePath == null
            ? (Icon)SystemIcons.Application.Clone()
            : Icon.ExtractAssociatedIcon(executablePath) ?? (Icon)SystemIcons.Application.Clone();
        _trayIcon = new NotifyIcon
        {
            Icon = _trayIconImage,
            Text = "Clackr",
            Visible = true
        };

        var menu = new ContextMenuStrip();

        var toggleItem = new ToolStripMenuItem("Keyboard Sounds")
        {
            Checked = Settings.KeyboardSoundsEnabled,
            CheckOnClick = true
        };
        toggleItem.CheckedChanged += (_, _) =>
        {
            AudioEngine.Enabled = toggleItem.Checked;
            Settings.KeyboardSoundsEnabled = toggleItem.Checked;
            SettingsStore.Save(Settings);
            _mainWindow?.SyncEnabledState(toggleItem.Checked);
        };
        menu.Items.Add(toggleItem);
        menu.Items.Add(new ToolStripSeparator());

        var openItem = new ToolStripMenuItem("Open Clackr");
        openItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(openItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApplication();
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();

        // Keep the tray checkbox and the window toggle in sync in both directions,
        // and persist the change either way.
        if (_mainWindow != null)
        {
            _mainWindow.EnabledChanged += isEnabled =>
            {
                toggleItem.Checked = isEnabled;
                Settings.KeyboardSoundsEnabled = isEnabled;
                SettingsStore.Save(Settings);
            };
        }
    }

    public void ShowMainWindow()
    {
        if (_mainWindow == null)
        {
            _mainWindow = new MainWindow();
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    public void ExitApplication()
    {
        // Final save captures anything that isn't persisted eagerly (volume, in particular -
        // saving on every slider tick during a drag would be wasteful, so it's saved here instead).
        _mainWindow?.SaveWindowSize();
        Settings.MasterVolume = AudioEngine.MasterVolume;
        Settings.KeyboardSoundsEnabled = AudioEngine.Enabled;
        SettingsStore.Save(Settings);

        _profileSwitchTimer?.Stop();
        _trayIcon?.Dispose();
        _trayIconImage?.Dispose();
        KeyboardHook?.Dispose();
        MouseHook?.Dispose();
        AudioEngine?.Dispose();
        Shutdown();
    }

}
