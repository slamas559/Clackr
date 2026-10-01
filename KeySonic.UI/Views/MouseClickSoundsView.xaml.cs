using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using KeySonic.Core.Audio;
using KeySonic.Core.Mouse;
using KeySonic.Core.Settings;

namespace KeySonic.UI.Views;

public partial class MouseClickSoundsView : UserControl
{
    private sealed record SoundChoice(string DisplayName, string? RelativePath)
    {
        public override string ToString() => DisplayName;
    }

    private App AppInstance => (App)Application.Current;
    private readonly List<SoundChoice> _choices = new();
    private readonly DispatcherTimer _flashTimer;
    private bool _updatingControls;
    private MouseButton? _flashingButton;
    private bool _sortDescending;

    public MouseClickSoundsView()
    {
        InitializeComponent();
        _flashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
        _flashTimer.Tick += FlashTimer_Tick;
        Loaded += (_, _) => RefreshLibrary();
        AppInstance.MouseHook.ButtonDown += MouseHook_ButtonDown;
    }

    public void RefreshLibrary()
    {
        var app = AppInstance;
        app.MouseClickSounds.DiscoverSounds(app.MouseClicksFolderPath);
        LibraryFolderText.Text = app.MouseClicksFolderPath;

        _choices.Clear();
        _choices.Add(new SoundChoice("No sound", null));
        foreach (var file in app.MouseClickSounds.AvailableSounds)
        {
            _choices.Add(new SoundChoice(file.DisplayName, file.RelativePath));
        }

        _updatingControls = true;
        LeftSoundCombo.ItemsSource = _choices.ToArray();
        RightSoundCombo.ItemsSource = _choices.ToArray();
        LeftSoundCombo.SelectedItem = FindChoice(app.Settings.LeftMouseClickSoundPath);
        RightSoundCombo.SelectedItem = FindChoice(app.Settings.RightMouseClickSoundPath);
        RefreshSoundCards();
        MouseSoundsToggle.IsChecked = app.Settings.MouseSoundsEnabled;
        LeftVolumeSlider.Value = app.Settings.LeftMouseClickVolume * 100;
        RightVolumeSlider.Value = app.Settings.RightMouseClickVolume * 100;
        _updatingControls = false;

        LeftVolumeText.Text = $"{(int)LeftVolumeSlider.Value}%";
        RightVolumeText.Text = $"{(int)RightVolumeSlider.Value}%";
    }

    private void RefreshSoundCards()
    {
        if (SoundLibraryList == null) return;

        string search = LibrarySearchBox.Text.Trim();
        string format = (LibraryFormatFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All formats";
        var sounds = AppInstance.MouseClickSounds.AvailableSounds
            .Where(sound => string.IsNullOrWhiteSpace(search) ||
                            sound.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Where(sound => format == "All formats" ||
                            Path.GetExtension(sound.RelativePath).Equals($".{format}", StringComparison.OrdinalIgnoreCase));
        sounds = _sortDescending
            ? sounds.OrderByDescending(sound => sound.DisplayName, StringComparer.OrdinalIgnoreCase)
            : sounds.OrderBy(sound => sound.DisplayName, StringComparer.OrdinalIgnoreCase);
        var visibleSounds = sounds.ToArray();
        SoundLibraryList.ItemsSource = visibleSounds;
        int totalSounds = AppInstance.MouseClickSounds.AvailableSounds.Count;
        SoundLibraryCountText.Text = visibleSounds.Length == totalSounds
            ? $"{totalSounds} sounds"
            : $"{visibleSounds.Length} of {totalSounds} sounds";
        SoundLibraryEmptyText.Text = totalSounds == 0
            ? "No WAV or MP3 sounds found. Add files to the library to get started."
            : "No sounds match this search and format.";
        SoundLibraryEmptyText.Visibility = visibleSounds.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LibrarySearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshSoundCards();

    private void LibraryFormatFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshSoundCards();

    private void LibrarySortButton_Click(object sender, RoutedEventArgs e)
    {
        _sortDescending = !_sortDescending;
        LibrarySortButton.Content = _sortDescending ? "Z-A" : "A-Z";
        RefreshSoundCards();
    }

    private SoundChoice FindChoice(string? relativePath)
    {
        foreach (var choice in _choices)
        {
            if (string.Equals(choice.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase))
            {
                return choice;
            }
        }

        return _choices[0];
    }

    private void MouseHook_ButtonDown(object? sender, MouseButtonEventArgs e)
    {
        Dispatcher.BeginInvoke(() => FlashMouseButton(e.Button));
    }

    private void FlashMouseButton(MouseButton button)
    {
        if (_flashingButton != null)
        {
            var previousTarget = _flashingButton == MouseButton.Left ? LeftMouseButtonRegion : RightMouseButtonRegion;
            previousTarget.Background = (Brush)FindResource("SurfaceBrush");
            previousTarget.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            previousTarget.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }

        _flashingButton = button;
        bool left = button == MouseButton.Left;
        var target = left ? LeftMouseButtonRegion : RightMouseButtonRegion;
        var fill = left ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("SuccessBrush");
        target.Background = fill;
        ((ScaleTransform)target.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1, 0.94, TimeSpan.FromMilliseconds(65)) { AutoReverse = true });
        ((ScaleTransform)target.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(1, 0.94, TimeSpan.FromMilliseconds(65)) { AutoReverse = true });
        MouseActivityText.Text = left ? "Left button pressed" : "Right button pressed";
        _flashTimer.Stop();
        _flashTimer.Start();
    }

    private void FlashTimer_Tick(object? sender, EventArgs e)
    {
        _flashTimer.Stop();
        if (_flashingButton == null) return;

        var target = _flashingButton == MouseButton.Left ? LeftMouseButtonRegion : RightMouseButtonRegion;
        target.Background = (Brush)FindResource("SurfaceBrush");
        target.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        target.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _flashingButton = null;
    }

    private void MouseSoundsToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingControls) return;
        AppInstance.Settings.MouseSoundsEnabled = MouseSoundsToggle.IsChecked == true;
        SettingsStore.Save(AppInstance.Settings);
    }

    private void LeftSoundCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ChangeSound(MouseButton.Left, LeftSoundCombo.SelectedItem as SoundChoice);

    private void RightSoundCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ChangeSound(MouseButton.Right, RightSoundCombo.SelectedItem as SoundChoice);

    private void ChangeSound(MouseButton button, SoundChoice? choice)
    {
        if (_updatingControls || choice == null) return;
        var app = AppInstance;
        if (!app.MouseClickSounds.TrySetSound(button, choice.RelativePath, out var error))
        {
            MessageBox.Show(error, "Clackr - Mouse sound", MessageBoxButton.OK, MessageBoxImage.Warning);
            RefreshLibrary();
            return;
        }

        if (button == MouseButton.Left)
        {
            app.Settings.LeftMouseClickSoundPath = choice.RelativePath;
        }
        else
        {
            app.Settings.RightMouseClickSoundPath = choice.RelativePath;
        }
        SettingsStore.Save(app.Settings);
    }

    private void LeftVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LeftVolumeText != null) LeftVolumeText.Text = $"{(int)LeftVolumeSlider.Value}%";
        if (_updatingControls || AppInstance.Settings == null) return;
        AppInstance.Settings.LeftMouseClickVolume = (float)(LeftVolumeSlider.Value / 100);
    }

    private void RightVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RightVolumeText != null) RightVolumeText.Text = $"{(int)RightVolumeSlider.Value}%";
        if (_updatingControls || AppInstance.Settings == null) return;
        AppInstance.Settings.RightMouseClickVolume = (float)(RightVolumeSlider.Value / 100);
    }

    private void VolumeSlider_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        SettingsStore.Save(AppInstance.Settings);
    }

    private void VolumeSlider_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        SettingsStore.Save(AppInstance.Settings);
    }

    private void PreviewLeft_Click(object sender, RoutedEventArgs e) => Preview(MouseButton.Left);

    private void PreviewRight_Click(object sender, RoutedEventArgs e) => Preview(MouseButton.Right);

    private void Preview(MouseButton button)
    {
        var sound = AppInstance.MouseClickSounds.GetSound(button);
        if (sound == null)
        {
            MessageBox.Show("Choose a sound file for this button first.", "Clackr - Mouse sound",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        float volume = button == MouseButton.Left
            ? AppInstance.Settings.LeftMouseClickVolume
            : AppInstance.Settings.RightMouseClickVolume;
        AppInstance.AudioEngine.PlayPreview(sound, volume);
    }

    private void RefreshLibrary_Click(object sender, RoutedEventArgs e) => RefreshLibrary();

    private async void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        string supportedExtensions = string.Join(";", AudioFileExtensions.Supported.Select(extension => $"*{extension}"));
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Add mouse click sounds",
            Filter = $"Supported audio ({supportedExtensions})|{supportedExtensions}|All files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        string destinationFolder = AppInstance.MouseClicksFolderPath;
        var sourcePaths = dialog.FileNames;
        var failures = new List<string>();
        MouseImportBusyStatusText.Text = sourcePaths.Length == 1
            ? "Adding sound to your library..."
            : $"Adding {sourcePaths.Length} sounds to your library...";
        MouseImportBusyOverlay.Visibility = Visibility.Visible;
        int added;
        try
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            (added, failures) = await Task.Run(() =>
            {
                Directory.CreateDirectory(destinationFolder);
                int addedFiles = 0;
                var failedFiles = new List<string>();
                foreach (string sourcePath in sourcePaths)
                {
                    string destinationPath = GetUniqueDestinationPath(destinationFolder, Path.GetFileName(sourcePath));
                    try
                    {
                        File.Copy(sourcePath, destinationPath);
                        addedFiles++;
                    }
                    catch (Exception ex)
                    {
                        failedFiles.Add($"{Path.GetFileName(sourcePath)}: {ex.Message}");
                    }
                }

                return (addedFiles, failedFiles);
            });
            RefreshLibrary();
            SoundLibraryCountText.Text = $"{added} added - {AppInstance.MouseClickSounds.AvailableSounds.Count} sounds in library";
        }
        finally
        {
            MouseImportBusyOverlay.Visibility = Visibility.Collapsed;
        }
        if (failures.Count > 0)
        {
            MessageBox.Show(string.Join(Environment.NewLine, failures), "Clackr - Couldn't add some sounds",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string GetUniqueDestinationPath(string folderPath, string fileName)
    {
        string destinationPath = Path.Combine(folderPath, fileName);
        if (!File.Exists(destinationPath)) return destinationPath;

        string name = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        int suffix = 2;
        do
        {
            destinationPath = Path.Combine(folderPath, $"{name} ({suffix++}){extension}");
        } while (File.Exists(destinationPath));

        return destinationPath;
    }

    private void LibraryPreview_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MouseClickSoundFile file }) return;
        var app = AppInstance;
        if (!app.MouseClickSounds.TryLoadSound(file.RelativePath, out var sound, out var error) || sound == null)
        {
            MessageBox.Show(error, "Clackr - Mouse sound", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        app.AudioEngine.PlayPreview(sound, app.Settings.MasterVolume);
    }

    private void LibraryAssignLeft_Click(object sender, RoutedEventArgs e) =>
        AssignLibrarySound(MouseButton.Left, (sender as Button)?.Tag as MouseClickSoundFile);

    private void LibraryAssignRight_Click(object sender, RoutedEventArgs e) =>
        AssignLibrarySound(MouseButton.Right, (sender as Button)?.Tag as MouseClickSoundFile);

    private void AssignLibrarySound(MouseButton button, MouseClickSoundFile? file)
    {
        if (file == null) return;
        var app = AppInstance;
        if (!app.MouseClickSounds.TrySetSound(button, file.RelativePath, out var error))
        {
            MessageBox.Show(error, "Clackr - Mouse sound", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _updatingControls = true;
        if (button == MouseButton.Left)
        {
            app.Settings.LeftMouseClickSoundPath = file.RelativePath;
            LeftSoundCombo.SelectedItem = FindChoice(file.RelativePath);
        }
        else
        {
            app.Settings.RightMouseClickSoundPath = file.RelativePath;
            RightSoundCombo.SelectedItem = FindChoice(file.RelativePath);
        }
        _updatingControls = false;
        SettingsStore.Save(app.Settings);
    }

    private void OpenLibraryFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AppInstance.MouseClicksFolderPath);
        Process.Start(new ProcessStartInfo(AppInstance.MouseClicksFolderPath) { UseShellExecute = true });
    }
}