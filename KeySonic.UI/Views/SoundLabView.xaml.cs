using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using KeySonic.Core.Keyboard;

namespace KeySonic.UI.Views;

public partial class SoundLabView : UserControl
{
    private App AppInstance => (App)Application.Current;
    private ToggleButton? _selectedKeyButton;
    private readonly Dictionary<KeyCode, ToggleButton> _keyButtons = new();

    public SoundLabView()
    {
        InitializeComponent();
        BuildKeyboard();
    }

    public void RefreshFromCurrentState()
    {
        var pack = AppInstance.PackManager.ActivePack;
        if (pack == null)
        {
            ActivePackText.Text = "No active pack";
            VariationCountText.Text = string.Empty;
            return;
        }

        ActivePackText.Text = pack.Metadata.Name;
        int count = pack.Bank?.LoadedDefaultSoundCount ?? 0;
        VariationCountText.Text = count == 1 ? "1 default variation" : $"{count} default variations";
    }

    public void FlashPhysicalKeyPress(KeyCode key)
    {
        if (!_keyButtons.TryGetValue(key, out var button)) return;

        button.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            From = 1,
            To = 0.45,
            Duration = TimeSpan.FromMilliseconds(70),
            AutoReverse = true
        });
    }

    private void BuildKeyboard()
    {
        for (int column = 0; column < 15; column++)
        {
            KeyboardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (int row = 0; row < 5; row++)
        {
            KeyboardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        AddKey("`", KeyCode.Grave, 0, 0);
        AddKey("1", KeyCode.D1, 0, 1);
        AddKey("2", KeyCode.D2, 0, 2);
        AddKey("3", KeyCode.D3, 0, 3);
        AddKey("4", KeyCode.D4, 0, 4);
        AddKey("5", KeyCode.D5, 0, 5);
        AddKey("6", KeyCode.D6, 0, 6);
        AddKey("7", KeyCode.D7, 0, 7);
        AddKey("8", KeyCode.D8, 0, 8);
        AddKey("9", KeyCode.D9, 0, 9);
        AddKey("0", KeyCode.D0, 0, 10);
        AddKey("-", KeyCode.Minus, 0, 11);
        AddKey("=", KeyCode.Equals, 0, 12);
        AddKey("Backspace", KeyCode.Backspace, 0, 13, 2);

        AddKey("Tab", KeyCode.Tab, 1, 0, 2);
        AddKey("Q", KeyCode.Q, 1, 2);
        AddKey("W", KeyCode.W, 1, 3);
        AddKey("E", KeyCode.E, 1, 4);
        AddKey("R", KeyCode.R, 1, 5);
        AddKey("T", KeyCode.T, 1, 6);
        AddKey("Y", KeyCode.Y, 1, 7);
        AddKey("U", KeyCode.U, 1, 8);
        AddKey("I", KeyCode.I, 1, 9);
        AddKey("O", KeyCode.O, 1, 10);
        AddKey("P", KeyCode.P, 1, 11);
        AddKey("[", KeyCode.LeftBracket, 1, 12);
        AddKey("]", KeyCode.RightBracket, 1, 13);
        AddKey("\\", KeyCode.Backslash, 1, 14);

        AddKey("Caps", KeyCode.CapsLock, 2, 0, 2);
        AddKey("A", KeyCode.A, 2, 2);
        AddKey("S", KeyCode.S, 2, 3);
        AddKey("D", KeyCode.D, 2, 4);
        AddKey("F", KeyCode.F, 2, 5);
        AddKey("G", KeyCode.G, 2, 6);
        AddKey("H", KeyCode.H, 2, 7);
        AddKey("J", KeyCode.J, 2, 8);
        AddKey("K", KeyCode.K, 2, 9);
        AddKey("L", KeyCode.L, 2, 10);
        AddKey(";", KeyCode.Semicolon, 2, 11);
        AddKey("'", KeyCode.Quote, 2, 12);
        AddKey("Enter", KeyCode.Enter, 2, 13, 2);

        AddKey("Shift", KeyCode.LeftShift, 3, 0, 2);
        AddKey("Z", KeyCode.Z, 3, 2);
        AddKey("X", KeyCode.X, 3, 3);
        AddKey("C", KeyCode.C, 3, 4);
        AddKey("V", KeyCode.V, 3, 5);
        AddKey("B", KeyCode.B, 3, 6);
        AddKey("N", KeyCode.N, 3, 7);
        AddKey("M", KeyCode.M, 3, 8);
        AddKey(",", KeyCode.Comma, 3, 9);
        AddKey(".", KeyCode.Period, 3, 10);
        AddKey("/", KeyCode.Slash, 3, 11);
        AddKey("Shift", KeyCode.RightShift, 3, 12, 3);

        AddKey("Ctrl", KeyCode.LeftCtrl, 4, 0, 2);
        AddKey("Win", KeyCode.LeftWindows, 4, 2);
        AddKey("Alt", KeyCode.LeftAlt, 4, 3, 2);
        AddKey("Space", KeyCode.Space, 4, 5, 5);
        AddKey("Alt", KeyCode.RightAlt, 4, 10, 2);
        AddKey("Ctrl", KeyCode.RightCtrl, 4, 12, 3);
    }

    private void AddKey(string label, KeyCode key, int row, int column, int columnSpan = 1)
    {
        var button = new ToggleButton
        {
            Content = label,
            Style = (Style)FindResource("KeyboardKeyButton")
        };
        _keyButtons[key] = button;
        button.Click += (_, _) => PreviewKey(button, key);
        Grid.SetRow(button, row);
        Grid.SetColumn(button, column);
        Grid.SetColumnSpan(button, columnSpan);
        KeyboardGrid.Children.Add(button);
    }

    private void PreviewKey(ToggleButton button, KeyCode key)
    {
        if (_selectedKeyButton != null && _selectedKeyButton != button)
        {
            _selectedKeyButton.IsChecked = false;
        }

        button.IsChecked = true;
        _selectedKeyButton = button;
        SelectedKeyText.Text = (string)button.Content;

        var pack = AppInstance.PackManager.ActivePack;
        if (pack == null) return;

        var sound = pack.EnsureLoaded().PickSound(key);
        AppInstance.AudioEngine.PlayPreview(sound);
    }
}