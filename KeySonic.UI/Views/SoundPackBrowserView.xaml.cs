using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media;
using KeySonic.Core.Audio;
using KeySonic.Core.SoundPacks;
using Microsoft.Win32;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;

namespace KeySonic.UI.Views;

public partial class SoundPackBrowserView : System.Windows.Controls.UserControl
{
    /// <summary>Raised when a pack is activated, so MainWindow can refresh the dashboard and navigate back.</summary>
    public event Action? PackActivated;

    private App AppInstance => (App)System.Windows.Application.Current;
    private bool _sortDescending;
    private bool _isImporting;

    public SoundPackBrowserView()
    {
        InitializeComponent();
    }

    public void RefreshCards()
    {
        var installedPacks = AppInstance.PackManager.InstalledPacks;
        var searchText = SearchBox.Text.Trim();
        var matchingPacks = installedPacks
            .Where(pack => MatchesSearch(pack, searchText));
        matchingPacks = _sortDescending
            ? matchingPacks.OrderByDescending(pack => pack.Metadata.Name, StringComparer.OrdinalIgnoreCase)
            : matchingPacks.OrderBy(pack => pack.Metadata.Name, StringComparer.OrdinalIgnoreCase);

        var packs = matchingPacks.ToList();
        CardsPanel.Children.Clear();
        foreach (var pack in packs)
        {
            CardsPanel.Children.Add(BuildCard(pack));
        }

        PackCountText.Text = packs.Count == installedPacks.Count
            ? $"{packs.Count} packs"
            : $"{packs.Count} of {installedPacks.Count} packs";
        EmptyStateText.Visibility = packs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        SearchBorder.BorderBrush = (Brush)FindResource("AccentBrush");
        SearchBorder.Background = (Brush)FindResource("SurfaceRaisedBrush");
    }

    private void SearchBox_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        SearchBorder.BorderBrush = (Brush)FindResource("BorderBrush2");
        SearchBorder.Background = (Brush)FindResource("SurfaceBrush");
    }

    private static bool MatchesSearch(SoundPack pack, string searchText)
    {
        if (string.IsNullOrEmpty(searchText)) return true;

        return pack.Metadata.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
               || pack.Metadata.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true
               || pack.Metadata.Author?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true;
    }

    private Border BuildCard(SoundPack pack)
    {
        bool isActive = ReferenceEquals(pack, AppInstance.PackManager.ActivePack);

        var cardContent = new Grid();
        cardContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        cardContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titleRow = new Grid();
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleRow.Children.Add(new TextBlock
        {
            Text = pack.Metadata.Name,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxHeight = 40,
            Margin = new Thickness(0, 0, 8, 0)
        });
        if (isActive)
        {
            var activeLabel = new TextBlock
            {
                Text = "ACTIVE",
                Foreground = (Brush)FindResource("SuccessBrush"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(activeLabel, 1);
            titleRow.Children.Add(activeLabel);
        }
        Grid.SetRow(titleRow, 0);
        cardContent.Children.Add(titleRow);

        var description = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(pack.Metadata.Description) ? "Custom sounds" : pack.Metadata.Description,
            Style = (Style)FindResource("SubText"),
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxHeight = 38,
            Margin = new Thickness(0, 7, 0, 4)
        };
        Grid.SetRow(description, 1);
        cardContent.Children.Add(description);

        int soundCount = Directory.Exists(pack.FolderPath)
            ? Directory.EnumerateFiles(pack.FolderPath, "*.wav", SearchOption.TopDirectoryOnly).Count()
            : 0;
        var packDetails = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        packDetails.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(pack.Metadata.Author) ? "Creator not listed" : $"By {pack.Metadata.Author}",
            Style = (Style)FindResource("SubText"),
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = pack.Metadata.Author
        });
        packDetails.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(pack.Metadata.License)
                ? "License not stated · verify before redistribution"
                : $"License: {pack.Metadata.License}",
            Style = (Style)FindResource("SubText"),
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = string.IsNullOrWhiteSpace(pack.Metadata.License)
                ? "The pack does not declare a license. Check the source terms before sharing or including it in a release."
                : pack.Metadata.License
        });
        packDetails.Children.Add(new TextBlock
        {
            Text = $"{soundCount} WAV files · v{pack.Metadata.Version}",
            Style = (Style)FindResource("SubText"),
            FontSize = 11
        });
        Grid.SetRow(packDetails, 2);
        cardContent.Children.Add(packDetails);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal };
        var previewButton = new Button
        {
            Content = "Preview",
            Style = (Style)FindResource("GhostButton"),
            Margin = new Thickness(0, 0, 8, 0)
        };
        previewButton.Click += (_, _) => PreviewPack(pack);
        buttonRow.Children.Add(previewButton);

        if (isActive)
        {
            buttonRow.Children.Add(new TextBlock
            {
                Text = "In use",
                Foreground = (Brush)FindResource("SuccessBrush"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 4, 0)
            });
        }
        else
        {
            var useButton = new Button { Content = "Use", Style = (Style)FindResource("PrimaryButton") };
            useButton.Click += (_, _) => ActivatePack(pack);
            buttonRow.Children.Add(useButton);
        }

        Grid.SetRow(buttonRow, 3);
        cardContent.Children.Add(buttonRow);

        return new Border
        {
            Style = (Style)FindResource("Card"),
            Width = 310,
            Height = 194,
            Margin = new Thickness(0, 0, 12, 12),
            Child = cardContent
        };
    }

    private void PreviewPack(SoundPack pack)
    {
        // Loading for preview does NOT activate the pack - previewing must never
        // switch what's actually playing while you type.
        var bank = pack.EnsureLoaded();
        var sound = bank.PickSound(KeySonic.Core.Keyboard.KeyCode.A);
        AppInstance.AudioEngine.PlayPreview(sound);
    }

    private void ActivatePack(SoundPack pack)
    {
        AppInstance.PackManager.Activate(pack);
        PackActivated?.Invoke();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        RefreshCards();
    }

    private void SortButton_Click(object sender, RoutedEventArgs e)
    {
        _sortDescending = !_sortDescending;
        SortButton.Content = _sortDescending ? "Z-A" : "A-Z";
        RefreshCards();
    }

    private async void ImportMechvibes_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select an unzipped Mechvibes sound pack" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        string sourceFolder = dialog.FolderName;
        string folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(sourceFolder));
        await ImportPackAsync(folderName, "Converting Mechvibes sounds...", stagingFolder =>
        {
            var result = MechvibesPackConverter.Convert(sourceFolder, stagingFolder);
            return $"{result.ConvertedSounds} sounds converted; {result.SkippedSounds} skipped.";
        });
    }

    private async void ImportAudioFiles_Click(object sender, RoutedEventArgs e)
    {
        string extensions = string.Join(";", AudioFileExtensions.Supported.Select(extension => $"*{extension}"));
        var dialog = new OpenFileDialog
        {
            Title = "Select sound files for a new keyboard pack",
            Filter = $"Supported audio ({extensions})|{extensions}|All files (*.*)|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        string suggestedName = dialog.FileNames.Length == 1
            ? Path.GetFileNameWithoutExtension(dialog.FileNames[0])
            : "Custom sounds";
        var nameDialog = new PackNameDialog(suggestedName) { Owner = Window.GetWindow(this) };
        if (nameDialog.ShowDialog() != true) return;

        await ImportPackAsync(nameDialog.PackName, "Building your sound pack...", stagingFolder =>
        {
            var result = CustomSoundPackImporter.ImportAudioFiles(dialog.FileNames, stagingFolder, nameDialog.PackName);
            string summary = $"{result.ImportedFileCount} audio files imported.";
            if (result.FailedFiles.Count > 0)
            {
                string failedFiles = string.Join(", ", result.FailedFiles.Take(5));
                summary += $" {result.FailedFiles.Count} skipped: {failedFiles}" +
                           (result.FailedFiles.Count > 5 ? ", ..." : ".");
            }
            return summary;
        });
    }

    private async Task ImportPackAsync(string requestedName, string busyMessage, Func<string, string> createPack)
    {
        if (_isImporting) return;

        string folderName = SanitizeFolderName(requestedName);
        if (folderName.Length == 0)
        {
            MessageBox.Show("The pack name must contain at least one valid character.", "Clackr - Import pack",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var app = AppInstance;
        if (app.PackManager.InstalledPacks.Any(pack => string.Equals(
                Path.GetFileName(pack.FolderPath), folderName, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"A sound pack named '{folderName}' already exists. Rename or remove it before importing another with that folder name.",
                "Clackr - Import pack", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Directory.CreateDirectory(app.UserPacksFolderPath);
        string destinationFolder = Path.Combine(app.UserPacksFolderPath, folderName);
        if (Directory.Exists(destinationFolder))
        {
            MessageBox.Show($"The destination folder already exists:\n{destinationFolder}", "Clackr - Import pack",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string stagingFolder = Path.Combine(app.UserPacksFolderPath, $".import-{Guid.NewGuid():N}");
        _isImporting = true;
        ImportMechvibesButton.IsEnabled = false;
        ImportAudioButton.IsEnabled = false;
        ImportBusyStatusText.Text = busyMessage;
        ImportBusyOverlay.Visibility = Visibility.Visible;
        try
        {
            await Dispatcher.Yield(DispatcherPriority.Render);
            string importSummary = await Task.Run(() => createPack(stagingFolder));
            Directory.Move(stagingFolder, destinationFolder);
            app.PackManager.DiscoverPacks(app.PacksFolderPath, app.UserPacksFolderPath);
            RefreshCards();
            MessageBox.Show($"'{folderName}' was added to your sound packs.\n{importSummary}", "Clackr - Import complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            if (Directory.Exists(stagingFolder))
            {
                try { Directory.Delete(stagingFolder, recursive: true); }
                catch (IOException) { }
            }
            MessageBox.Show($"Couldn't import the sound pack:\n{ex.Message}", "Clackr - Import failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            ImportBusyOverlay.Visibility = Visibility.Collapsed;
            ImportMechvibesButton.IsEnabled = true;
            ImportAudioButton.IsEnabled = true;
            _isImporting = false;
        }
    }

    private static string SanitizeFolderName(string name)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string sanitized = new(name.Trim().Where(character => !invalidCharacters.Contains(character)).ToArray());
        return sanitized.Trim().TrimEnd('.');
    }
}
