using System;
using System.Windows;
using System.Windows.Controls;
using KeySonic.Core.Keyboard;

namespace KeySonic.UI.Views;

public partial class DashboardView : System.Windows.Controls.UserControl
{
    /// <summary>Raised when the user clicks "Change" on the Active Sound card.</summary>
    public event Action? ChangeSoundRequested;

    /// <summary>Raised when the user toggles Keyboard Sounds, so the tray menu checkbox can stay in sync.</summary>
    public event Action<bool>? EnabledChanged;

    private App AppInstance => (App)System.Windows.Application.Current;
    private bool _suppressToggleEvent;

    public DashboardView()
    {
        InitializeComponent();
        SyncEnabledState(true);
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
}
