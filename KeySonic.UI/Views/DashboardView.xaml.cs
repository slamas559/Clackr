using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using KeySonic.Core.Keyboard;
using KeySonic.Core.Settings;

namespace KeySonic.UI.Views;

public partial class DashboardView : System.Windows.Controls.UserControl
{
    /// <summary>Raised when the user clicks "Change" on the Active Sound card.</summary>
    public event Action? ChangeSoundRequested;
    public event Action? MouseSoundsRequested;

    /// <summary>Raised when the user toggles Keyboard Sounds, so the tray menu checkbox can stay in sync.</summary>
    public event Action<bool>? EnabledChanged;

    private App AppInstance => (App)System.Windows.Application.Current;
    private bool _suppressToggleEvent;
    private bool _refreshingProfiles;
    private static readonly JsonSerializerOptions ProfileJsonOptions = new() { WriteIndented = true };

    public DashboardView()
    {
        InitializeComponent();
        SyncEnabledState(true);
        Loaded += (_, _) =>
        {
            RefreshProfiles();
            RefreshControlCenter();
        };
    }

    public void RefreshControlCenter()
    {
        var app = AppInstance;
        SyncEnabledState(app.AudioEngine.Enabled);
        SyncMouseSoundsState(app.Settings.MouseSoundsEnabled);
        RefreshActivePackDisplay();
        MouseSoundSummaryText.Text = $"Left: {GetSoundName(app.Settings.LeftMouseClickSoundPath)} · " +
                                    $"Right: {GetSoundName(app.Settings.RightMouseClickSoundPath)}";
        FirstProfileSetupPanel.Visibility = app.Settings.SoundProfiles.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static string GetSoundName(string? relativePath) => string.IsNullOrWhiteSpace(relativePath)
        ? "no sound"
        : Path.GetFileNameWithoutExtension(relativePath);

    public void SyncMouseSoundsState(bool isEnabled)
    {
        MouseSoundsToggle.IsChecked = isEnabled;
        MouseSoundSummaryText.Opacity = isEnabled ? 1 : 0.65;
    }

    private void RefreshProfiles(string? selectedName = null)
    {
        var app = AppInstance;
        selectedName ??= app.Settings.ActiveSoundProfileName;
        _refreshingProfiles = true;
        ProfileCombo.ItemsSource = app.Settings.SoundProfiles.ToList();
        ProfileCombo.SelectedItem = app.Settings.SoundProfiles.FirstOrDefault(profile =>
            string.Equals(profile.Name, selectedName, StringComparison.OrdinalIgnoreCase));
        _refreshingProfiles = false;

        ProfileStatusText.Text = ProfileCombo.SelectedItem is SoundProfile activeProfile
            ? $"Active profile: {activeProfile.Name}"
            : app.Settings.SoundProfiles.Count == 0
                ? "Save this keyboard and mouse setup as your first profile."
                : "Choose a saved keyboard and mouse setup.";
    }

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshingProfiles || ProfileCombo.SelectedItem is not SoundProfile profile) return;

        var app = AppInstance;
        if (!app.ApplySoundProfile(profile, out var warning))
        {
            MessageBox.Show(warning, "KeySonic - Profile", MessageBoxButton.OK, MessageBoxImage.Warning);
            RefreshProfiles();
            return;
        }

        ProfileNameTextBox.Text = profile.Name;
        TargetProcessNameTextBox.Text = profile.TargetProcessName;
        VolumeSliderControl.Value = app.Settings.MasterVolume * 100;
        SyncEnabledState(app.Settings.KeyboardSoundsEnabled);
        RefreshActivePackDisplay();
        ProfileStatusText.Text = warning == null
            ? $"Active profile: {profile.Name}"
            : $"Active profile: {profile.Name}. {warning}";
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        string name = ProfileNameTextBox.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show("Enter a name for this profile.", "KeySonic - Profile", MessageBoxButton.OK,
                MessageBoxImage.Information);
            ProfileNameTextBox.Focus();
            return;
        }

        var app = AppInstance;
        var profile = app.CaptureSoundProfile(name);
        profile.TargetProcessName = TargetProcessNameTextBox.Text.Trim();
        int existingIndex = app.Settings.SoundProfiles.FindIndex(existing =>
            string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            app.Settings.SoundProfiles[existingIndex] = profile;
        }
        else
        {
            app.Settings.SoundProfiles.Add(profile);
        }

        app.Settings.ActiveSoundProfileName = name;
        SettingsStore.Save(app.Settings);
        RefreshProfiles(name);
        FirstProfileSetupPanel.Visibility = Visibility.Collapsed;
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileCombo.SelectedItem is not SoundProfile profile) return;

        var app = AppInstance;
        app.Settings.SoundProfiles.Remove(profile);
        if (string.Equals(app.Settings.ActiveSoundProfileName, profile.Name, StringComparison.OrdinalIgnoreCase))
        {
            app.Settings.ActiveSoundProfileName = null;
        }

        SettingsStore.Save(app.Settings);
        ProfileNameTextBox.Text = string.Empty;
        TargetProcessNameTextBox.Text = string.Empty;
        RefreshProfiles();
    }

    private void ExportProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileCombo.SelectedItem is not SoundProfile profile)
        {
            MessageBox.Show("Choose a saved profile to export.", "KeySonic - Profile", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export KeySonic profile",
            Filter = "KeySonic profile (*.ksprofile.json)|*.ksprofile.json|JSON file (*.json)|*.json",
            FileName = $"{profile.Name}.ksprofile.json",
            AddExtension = true,
            DefaultExt = ".json"
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        try
        {
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(profile, ProfileJsonOptions));
            ProfileStatusText.Text = $"Exported {profile.Name}. Audio files are referenced, not included.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Couldn't export the profile:\n{ex.Message}", "KeySonic - Profile",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import KeySonic profile",
            Filter = "KeySonic profile (*.ksprofile.json;*.json)|*.ksprofile.json;*.json|All files (*.*)|*.*",
            Multiselect = false
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        try
        {
            var profile = JsonSerializer.Deserialize<SoundProfile>(File.ReadAllText(dialog.FileName));
            if (profile == null || string.IsNullOrWhiteSpace(profile.Name))
            {
                throw new InvalidDataException("The selected file does not contain a named KeySonic profile.");
            }

            profile.Name = profile.Name.Trim();
            profile.TargetProcessName = (profile.TargetProcessName ?? string.Empty).Trim();
            var app = AppInstance;
            int existingIndex = app.Settings.SoundProfiles.FindIndex(existing =>
                string.Equals(existing.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                var replace = MessageBox.Show($"A profile named '{profile.Name}' already exists. Replace it?",
                    "KeySonic - Import profile", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (replace != MessageBoxResult.Yes) return;
                app.Settings.SoundProfiles[existingIndex] = profile;
            }
            else
            {
                app.Settings.SoundProfiles.Add(profile);
            }

            SettingsStore.Save(app.Settings);
            ProfileNameTextBox.Text = profile.Name;
            TargetProcessNameTextBox.Text = profile.TargetProcessName;
            RefreshProfiles(profile.Name);
            bool packInstalled = app.PackManager.InstalledPacks.Any(pack =>
                string.Equals(Path.GetFileName(pack.FolderPath), profile.ActivePackFolderName,
                    StringComparison.OrdinalIgnoreCase));
            ProfileStatusText.Text = packInstalled
                ? $"Imported {profile.Name}. Add its referenced mouse sounds to this device if needed."
                : $"Imported {profile.Name}. Install its referenced keyboard pack and mouse sounds before applying it.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Couldn't import the profile:\n{ex.Message}", "KeySonic - Profile",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void RefreshActivePackDisplay()
    {
        var pack = AppInstance.PackManager.ActivePack;
        if (pack == null) return;

        ActiveSoundNameText.Text = pack.Metadata.Name;

        var count = pack.Bank?.LoadedDefaultSoundCount ?? 0;
        var description = string.IsNullOrWhiteSpace(pack.Metadata.Description) ? "Custom sounds" : pack.Metadata.Description;
        ActiveSoundSubText.Text = count == 1 ? $"{description} \u00b7 1 variation" : $"{description} \u00b7 {count} variations";
    }

    /// <summary>Applies settings loaded from disk once, before the window is first shown.</summary>
    public void SetInitialState(float masterVolume, bool keyboardSoundsEnabled)
    {
        int percent = (int)Math.Round(masterVolume * 100);
        VolumeSliderControl.Value = percent; // fires ValueChanged, which applies it to AudioEngine and updates the % label
        SyncEnabledState(keyboardSoundsEnabled);
    }

    public void SyncEnabledState(bool isEnabled)
    {
        _suppressToggleEvent = true;
        EnabledToggle.IsChecked = isEnabled;
        _suppressToggleEvent = false;
        UpdateStatusVisual(isEnabled);
    }

    private void UpdateStatusVisual(bool isEnabled)
    {
        if (isEnabled)
        {
            StatusDot.Fill = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            StatusText.Text = "Active - listening for keystrokes";
        }
        else
        {
            StatusDot.Fill = (System.Windows.Media.Brush)FindResource("TextMutedBrush");
            StatusText.Text = "Paused - keyboard sounds off";
        }
    }

    private void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        var bank = AppInstance.PackManager.ActivePack?.Bank;
        if (bank == null) return;
        var sound = bank.PickSound(KeyCode.A);
        AppInstance.AudioEngine.PlayPreview(sound);
    }

    private void ChangeSoundButton_Click(object sender, RoutedEventArgs e)
    {
        ChangeSoundRequested?.Invoke();
    }

    private void VolumeSliderControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var normalized = (float)(e.NewValue / 100.0);
        AppInstance.AudioEngine.MasterVolume = normalized;
        VolumeValueText.Text = $"{(int)e.NewValue}%";
    }

    private void EnabledToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvent) return;

        bool isEnabled = EnabledToggle.IsChecked == true;
        AppInstance.AudioEngine.Enabled = isEnabled;
        UpdateStatusVisual(isEnabled);
        EnabledChanged?.Invoke(isEnabled);
    }

    private void MouseSoundsToggle_Changed(object sender, RoutedEventArgs e)
    {
        AppInstance.Settings.MouseSoundsEnabled = MouseSoundsToggle.IsChecked == true;
        SettingsStore.Save(AppInstance.Settings);
        SyncMouseSoundsState(MouseSoundsToggle.IsChecked == true);
    }

    private void MouseSoundsButton_Click(object sender, RoutedEventArgs e) => MouseSoundsRequested?.Invoke();
}
