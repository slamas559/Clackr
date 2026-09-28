using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using KeySonic.Core.Audio;
using KeySonic.Core.Keyboard;
using KeySonic.Core.Settings;
using KeySonic.Core.SoundPacks;
using Application = System.Windows.Application;

namespace KeySonic.UI;

public partial class App : Application
{
    public SoundPackManager PackManager { get; private set; } = null!;
    public AudioEngine AudioEngine { get; private set; } = null!;
    public GlobalKeyboardHook KeyboardHook { get; private set; } = null!;
    public AppSettings Settings { get; private set; } = null!;
    public string PacksFolderPath { get; private set; } = "";

    private NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogCrash("AppDomain.UnhandledException", args.ExceptionObject as Exception);

        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash("DispatcherUnhandledException", args.Exception);
            args.Handled = true; // keep the app alive long enough to show the message box below
            System.Windows.MessageBox.Show(
                $"KeySonic crashed:\n\n{args.Exception}",
                "KeySonic - Unhandled error", MessageBoxButton.OK, MessageBoxImage.Error);
        };
    }

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "crash-log.txt");
            File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{source}]\n{ex}\n\n");
        }
        catch { /* logging must never itself throw */ }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Settings = SettingsStore.Load();

        PackManager = new SoundPackManager();
        PacksFolderPath = Path.Combine(AppContext.BaseDirectory, "packs");

        try
        {
            PackManager.DiscoverPacks(PacksFolderPath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Couldn't read the sound packs folder:\n{PacksFolderPath}\n\n{ex.Message}",
                "KeySonic - No sound packs found",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        if (PackManager.InstalledPacks.Count == 0)
        {
            System.Windows.MessageBox.Show(
                $"No sound packs found in:\n{PacksFolderPath}\n\n" +
                "Each pack is its own subfolder containing .wav files. Add at least one and restart.",
                "KeySonic - No sound packs found",
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
                "KeySonic - Pack load failed",
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

        _mainWindow = new MainWindow();
        _mainWindow.ApplySettingsOnLoad(Settings.MasterVolume, Settings.KeyboardSoundsEnabled);
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

    private void SetupTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application, // TODO: replace with a real KeySonic.ico before publishing
            Text = "KeySonic",
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

        var openItem = new ToolStripMenuItem("Open KeySonic");
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
        Settings.MasterVolume = AudioEngine.MasterVolume;
        Settings.KeyboardSoundsEnabled = AudioEngine.Enabled;
        SettingsStore.Save(Settings);

        _trayIcon?.Dispose();
        KeyboardHook?.Dispose();
        AudioEngine?.Dispose();
        Shutdown();
    }
}
