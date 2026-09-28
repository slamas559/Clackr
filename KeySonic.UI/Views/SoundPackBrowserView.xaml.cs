using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeySonic.Core.SoundPacks;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;

namespace KeySonic.UI.Views;

public partial class SoundPackBrowserView : System.Windows.Controls.UserControl
{
    /// <summary>Raised when a pack is activated, so MainWindow can refresh the dashboard and navigate back.</summary>
    public event Action? PackActivated;

    private App AppInstance => (App)System.Windows.Application.Current;
    private bool _sortDescending;

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

        if (!string.IsNullOrWhiteSpace(pack.Metadata.Author))
        {
            var author = new TextBlock
            {
                Text = pack.Metadata.Author,
                Style = (Style)FindResource("SubText"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(author, 2);
            cardContent.Children.Add(author);
        }

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
            Height = 174,
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
}
