using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using KeySonic.Core.Keyboard;

namespace KeySonic.UI.Views;

public partial class SoundLabView : UserControl
{
    private App AppInstance => (App)Application.Current;
    private KeyVisual? _selectedKey;
    private readonly Dictionary<KeyCode, KeyVisual> _keys = new();
    private readonly List<KeyVisual> _keyList = new();
    private readonly DispatcherTimer _physicalKeyTimer = new() { Interval = TimeSpan.FromMilliseconds(140) };
    private readonly DispatcherTimer _nextPromptTimer = new() { Interval = TimeSpan.FromMilliseconds(1100) };
    private KeyVisual? _flashingKey;
    private readonly List<bool> _gameCorrectCharacters = new();
    private readonly string[] _gamePrompts =
    {
        "small sounds can make ordinary moments feel more focused.",
        "steady practice turns each new skill into a natural habit.",
        "a quiet room makes every clear note easier to notice."
    };
    private int _gamePromptIndex;
    private string _currentGamePrompt = string.Empty;

    public bool IsTypingGameActive { get; private set; }

    public SoundLabView()
    {
        _rgbBrush = CreateRgbBrush(_rgbShift);
        InitializeComponent();
        BuildKeyboard();
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue) StartRgbAnimation();
            else StopRgbAnimation();
        };
        _physicalKeyTimer.Tick += (_, _) =>
        {
            _physicalKeyTimer.Stop();
            if (_flashingKey != null)
            {
                _flashingKey.SetPressed(false);
                _flashingKey = null;
            }
        };
        _nextPromptTimer.Tick += (_, _) =>
        {
            _nextPromptTimer.Stop();
            if (IsTypingGameActive && _gameCorrectCharacters.Count == _currentGamePrompt.Length)
            {
                StartNextGamePrompt();
            }
        };
    }

    public void FlashPhysicalKeyPress(KeyCode key)
    {
        if (!_keys.TryGetValue(key, out var visual)) return;

        if (_selectedKey != null)
        {
            _selectedKey.SetPressed(false);
            _selectedKey = null;
        }

        if (_flashingKey != null && _flashingKey != visual)
        {
            _flashingKey.SetPressed(false);
        }

        visual.SetPressed(true);
        _flashingKey = visual;
        _physicalKeyTimer.Stop();
        _physicalKeyTimer.Start();
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

    public bool HandleTypingGameKey(KeyCode key)
    {
        if (!IsTypingGameActive) return false;

        if (key == KeyCode.Backspace)
        {
            if (_gameCorrectCharacters.Count > 0)
            {
                _gameCorrectCharacters.RemoveAt(_gameCorrectCharacters.Count - 1);
                UpdateGamePrompt();
            }
            return true;
        }

        char typedCharacter = GetGameCharacter(key);
        if (typedCharacter == '\0' || _gameCorrectCharacters.Count >= _currentGamePrompt.Length) return false;

        int position = _gameCorrectCharacters.Count;
        _gameCorrectCharacters.Add(char.ToLowerInvariant(typedCharacter) == _currentGamePrompt[position]);
        UpdateGamePrompt();
        return true;
    }

    private static char GetGameCharacter(KeyCode key) => key switch
    {
        >= KeyCode.A and <= KeyCode.Z => (char)('a' + (key - KeyCode.A)),
        >= KeyCode.D0 and <= KeyCode.D9 => (char)('0' + (key - KeyCode.D0)),
        KeyCode.Space => ' ',
        KeyCode.Comma => ',',
        KeyCode.Period => '.',
        KeyCode.Minus => '-',
        _ => '\0'
    };

    private void TypingGameButton_Click(object sender, RoutedEventArgs e)
    {
        IsTypingGameActive = !IsTypingGameActive;
        _nextPromptTimer.Stop();
        TypingGamePanel.Visibility = IsTypingGameActive ? Visibility.Visible : Visibility.Collapsed;
        RestartGameButton.Visibility = IsTypingGameActive ? Visibility.Visible : Visibility.Collapsed;
        TypingGameButton.Content = IsTypingGameActive ? "Exit typing game" : "Typing game";
        if (IsTypingGameActive) StartNextGamePrompt();
    }

    private void RestartGameButton_Click(object sender, RoutedEventArgs e) => StartNextGamePrompt();

    private void StartNextGamePrompt()
    {
        _nextPromptTimer.Stop();
        _currentGamePrompt = _gamePrompts[_gamePromptIndex++ % _gamePrompts.Length];
        _gameCorrectCharacters.Clear();
        UpdateGamePrompt();
    }

    private void UpdateGamePrompt()
    {
        GamePromptText.Inlines.Clear();
        for (int index = 0; index < _currentGamePrompt.Length; index++)
        {
            Brush color = index >= _gameCorrectCharacters.Count
                ? (Brush)FindResource("TextSecondaryBrush")
                : _gameCorrectCharacters[index]
                    ? (Brush)FindResource("SuccessBrush")
                    : (Brush)FindResource("DangerBrush");
            GamePromptText.Inlines.Add(new Run(_currentGamePrompt[index].ToString()) { Foreground = color });
        }

        int progress = _currentGamePrompt.Length == 0
            ? 0
            : (int)Math.Round(_gameCorrectCharacters.Count * 100.0 / _currentGamePrompt.Length);
        GameProgressBar.Value = progress;
        GameProgressText.Text = $"{progress}%";
        if (_gameCorrectCharacters.Count == _currentGamePrompt.Length && _currentGamePrompt.Length > 0)
        {
            GameProgressText.Text = "Complete";
            if (!_nextPromptTimer.IsEnabled) _nextPromptTimer.Start();
        }
        else
        {
            _nextPromptTimer.Stop();
        }
    }

    private void BuildKeyboard()
    {
        _plate.Fill = PlateFill;
        _plate.Stroke = _rgbBrush;
        _plate.StrokeThickness = 2;
        _plate.StrokeLineJoin = PenLineJoin.Round;
        _lip.Fill = LipFill;
        _plate.IsHitTestVisible = false;
        _lip.IsHitTestVisible = false;

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

        // Layering: case, then all underglow, then keys back-to-front so near rows overlap far rows.
        KeyboardCanvas.Children.Add(_plate);
        KeyboardCanvas.Children.Add(_lip);
        foreach (var key in _keyList) KeyboardCanvas.Children.Add(key.Glow);
        foreach (var key in _keyList) KeyboardCanvas.Children.Add(key.Root);
    }

    private void KeyboardCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Math.Abs(e.NewSize.Width - _layoutWidth) < 0.5) return;
        _layoutWidth = e.NewSize.Width;
        ApplyLayout(_layoutWidth);
    }

    // ------------------------------------------------------------------
    // Perspective keyboard
    // The board is a flat plane tilted away from the typist. Every key corner is projected through a
    // simple pinhole camera, so the far (top) edge comes out narrower than the near (bottom) edge:
    // a true trapezoid, with each key a smaller trapezoid of its own.
    // ------------------------------------------------------------------
    private const double BoardCols = 15;
    private const double BoardRows = 5;
    private const double TiltDegrees = 36;      // higher = stronger trapezoid
    private const double CameraDistance = 12;   // lower = stronger perspective
    private const double KeyGap = 0.07;
    private const double KeyInset = 0.045;      // top of a key is slightly smaller than its base
    private const double KeyHeightUnits = 0.30; // key thickness
    private const double CasePad = 0.55;
    private const double RgbCycleSeconds = 6;
    private const double IdleGlowOpacity = 0.28;

    private static readonly Brush FaceFill = Frozen(new LinearGradientBrush(Color.FromRgb(0x3A, 0x3F, 0x4D), Color.FromRgb(0x23, 0x26, 0x32), 90));
    private static readonly Brush FaceStroke = Frozen(new LinearGradientBrush(Color.FromRgb(0x5C, 0x65, 0x7A), Color.FromRgb(0x17, 0x1A, 0x20), 90));
    private static readonly Brush WallFill = Frozen(new LinearGradientBrush(Color.FromRgb(0x15, 0x17, 0x1C), Color.FromRgb(0x07, 0x08, 0x0A), 90));
    private static readonly Brush BaseFill = Frozen(new SolidColorBrush(Color.FromRgb(0x07, 0x08, 0x0A)));
    private static readonly Brush PlateFill = Frozen(new LinearGradientBrush(Color.FromRgb(0x1A, 0x1D, 0x24), Color.FromRgb(0x0B, 0x0C, 0x10), 90));
    private static readonly Brush LipFill = Frozen(new SolidColorBrush(Color.FromRgb(0x08, 0x09, 0x0C)));

    // ONE shared rainbow brush for the whole board (instead of one animated brush + blur per key).
    // It uses absolute coordinates, so each key shows the slice of the gradient under it and the colours
    // sweep across the board as a wave. Only a single transform is animated.
    private readonly TranslateTransform _rgbShift = new();
    private readonly LinearGradientBrush _rgbBrush;
    private readonly Polygon _plate = new();
    private readonly Polygon _lip = new();
    private double _layoutWidth;
    private double _focal, _originX, _bottomY, _tiltSin, _tiltCos;

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private static LinearGradientBrush CreateRgbBrush(Transform shift)
    {
        Color[] stops =
        {
            Color.FromRgb(255, 40, 40),
            Color.FromRgb(255, 200, 0),
            Color.FromRgb(40, 255, 90),
            Color.FromRgb(0, 220, 255),
            Color.FromRgb(70, 90, 255),
            Color.FromRgb(220, 60, 255),
            Color.FromRgb(255, 40, 40)
        };

        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            SpreadMethod = GradientSpreadMethod.Repeat,
            StartPoint = new Point(0, 0),
            EndPoint = new Point(600, 130),
            Transform = shift
        };
        for (int i = 0; i < stops.Length; i++)
        {
            brush.GradientStops.Add(new GradientStop(stops[i], i / (double)(stops.Length - 1)));
        }

        return brush;
    }

    private void StartRgbAnimation()
    {
        if (_layoutWidth <= 0) return;
        var end = _rgbBrush.EndPoint;
        var duration = TimeSpan.FromSeconds(RgbCycleSeconds);

        var animateX = new DoubleAnimation(0, end.X, duration) { RepeatBehavior = RepeatBehavior.Forever };
        var animateY = new DoubleAnimation(0, end.Y, duration) { RepeatBehavior = RepeatBehavior.Forever };
        Timeline.SetDesiredFrameRate(animateX, 30);
        Timeline.SetDesiredFrameRate(animateY, 30);
        _rgbShift.BeginAnimation(TranslateTransform.XProperty, animateX);
        _rgbShift.BeginAnimation(TranslateTransform.YProperty, animateY);
    }

    private void StopRgbAnimation()
    {
        _rgbShift.BeginAnimation(TranslateTransform.XProperty, null);
        _rgbShift.BeginAnimation(TranslateTransform.YProperty, null);
    }

    // u = column position (0..15), w = row position (0 = far/top row, 5 = near/bottom edge)
    private Point Project(double u, double w)
    {
        double far = BoardRows - w;
        double z = CameraDistance + far * _tiltSin;
        double scale = _focal / z;
        return new Point(_originX + (u - BoardCols / 2) * scale, _bottomY - far * _tiltCos * scale);
    }

    private static Point Lerp(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private static Point OnQuad(Point tl, Point tr, Point br, Point bl, double s, double t) =>
        Lerp(Lerp(tl, tr, s), Lerp(bl, br, s), t);

    private static Point Up(Point p, double amount) => new(p.X, p.Y - amount);

    private void ApplyLayout(double width)
    {
        if (width < 80) return;

        double phi = TiltDegrees * Math.PI / 180;
        _tiltSin = Math.Sin(phi);
        _tiltCos = Math.Cos(phi);

        double nearZ = CameraDistance - CasePad * _tiltSin;
        double farZ = CameraDistance + (BoardRows + CasePad) * _tiltSin;
        _focal = 0.96 * width * nearZ / (BoardCols + 2 * CasePad);
        _originX = width / 2;
        _bottomY = 8 + (BoardRows + CasePad) * _tiltCos * _focal / farZ;

        double lip = 0.5 * _focal / nearZ;
        KeyboardCanvas.Height = _bottomY + CasePad * _tiltCos * _focal / nearZ + lip + 14;

        Point tl = Project(-CasePad, -CasePad);
        Point tr = Project(BoardCols + CasePad, -CasePad);
        Point br = Project(BoardCols + CasePad, BoardRows + CasePad);
        Point bl = Project(-CasePad, BoardRows + CasePad);
        _plate.Points = new PointCollection { tl, tr, br, bl };
        _lip.Points = new PointCollection { bl, br, new Point(br.X, br.Y + lip), new Point(bl.X, bl.Y + lip) };

        foreach (var key in _keyList) LayoutKey(key);

        double period = width * 0.8;
        _rgbBrush.EndPoint = new Point(period, period * 0.22);
        if (IsVisible) StartRgbAnimation();
    }

    private void LayoutKey(KeyVisual key)
    {
        double u0 = key.Column + KeyGap, u1 = key.Column + key.Span - KeyGap;
        double w0 = key.Row + KeyGap, w1 = key.Row + 1 - KeyGap;

        double rowScale = _focal / (CameraDistance + (BoardRows - (key.Row + 0.5)) * _tiltSin);
        double height = KeyHeightUnits * rowScale;

        // base footprint (what touches the board)
        Point b0 = Project(u0, w0), b1 = Project(u1, w0), b2 = Project(u1, w1), b3 = Project(u0, w1);
        // top face: slightly smaller, lifted up by the key height
        Point f0 = Up(Project(u0 + KeyInset, w0 + KeyInset), height);
        Point f1 = Up(Project(u1 - KeyInset, w0 + KeyInset), height);
        Point f2 = Up(Project(u1 - KeyInset, w1 - KeyInset), height);
        Point f3 = Up(Project(u0 + KeyInset, w1 - KeyInset), height);

        key.Glow.Points = new PointCollection
        {
            Project(u0 - 0.10, w0 - 0.10), Project(u1 + 0.10, w0 - 0.10),
            Project(u1 + 0.10, w1 + 0.10), Project(u0 - 0.10, w1 + 0.10)
        };
        key.Base.Points = new PointCollection { b0, b1, b2, b3 };
        key.Wall.Points = new PointCollection { f3, f2, b2, b3 };
        key.Face.Points = new PointCollection { f0, f1, f2, f3 };
        key.Tint.Points = key.Face.Points;
        key.Strip.Points = new PointCollection
        {
            OnQuad(f0, f1, f2, f3, 0.10, 0.80), OnQuad(f0, f1, f2, f3, 0.90, 0.80),
            OnQuad(f0, f1, f2, f3, 0.90, 0.89), OnQuad(f0, f1, f2, f3, 0.10, 0.89)
        };

        key.Text.FontSize = Math.Clamp(rowScale * 0.27, 8, 14);
        key.Text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Point center = OnQuad(f0, f1, f2, f3, 0.5, 0.42);
        Canvas.SetLeft(key.Text, center.X - key.Text.DesiredSize.Width / 2);
        Canvas.SetTop(key.Text, center.Y - key.Text.DesiredSize.Height / 2);

        key.PressDepth = height * 0.7;
        if (key.IsPressed) key.FaceShift.Y = key.PressDepth;
    }

    private void AddKey(string label, KeyCode key, int row, int column, int columnSpan = 1)
    {
        var visual = new KeyVisual(label, key, row, column, columnSpan, _rgbBrush, (Brush)FindResource("TextPrimaryBrush"));
        visual.Root.MouseLeftButtonDown += (_, e) =>
        {
            PreviewKey(visual);
            e.Handled = true;
        };
        _keys[key] = visual;
        _keyList.Add(visual);
    }

    private void PreviewKey(KeyVisual visual)
    {
        if (_selectedKey != null && _selectedKey != visual)
        {
            _selectedKey.SetPressed(false);
        }

        visual.SetPressed(true);
        _selectedKey = visual;
        SelectedKeyText.Text = visual.Label;

        var pack = AppInstance.PackManager.ActivePack;
        if (pack == null) return;

        var sound = pack.EnsureLoaded().PickSound(visual.Key);
        AppInstance.AudioEngine.PlayPreview(sound);
    }

    private sealed class KeyVisual
    {
        public readonly string Label;
        public readonly KeyCode Key;
        public readonly int Row, Column, Span;

        public readonly Canvas Root = new() { Cursor = Cursors.Hand };
        public readonly Polygon Glow = new() { IsHitTestVisible = false, Opacity = IdleGlowOpacity };
        public readonly Polygon Base = new();
        public readonly Polygon Wall = new();
        public readonly Canvas FaceGroup = new();
        public readonly Polygon Face = new();
        public readonly Polygon Tint = new() { IsHitTestVisible = false, Opacity = 0 };
        public readonly Polygon Strip = new() { IsHitTestVisible = false, Opacity = 0.9 };
        public readonly TextBlock Text = new();
        public readonly TranslateTransform FaceShift = new();

        public double PressDepth;
        public bool IsPressed { get; private set; }

        public KeyVisual(string label, KeyCode key, int row, int column, int span, Brush rgb, Brush textBrush)
        {
            Label = label;
            Key = key;
            Row = row;
            Column = column;
            Span = span;

            Glow.Fill = rgb;
            Base.Fill = BaseFill;
            Wall.Fill = WallFill;
            Face.Fill = FaceFill;
            Face.Stroke = FaceStroke;
            Face.StrokeThickness = 1;
            Face.StrokeLineJoin = PenLineJoin.Round;
            Tint.Fill = rgb;
            Strip.Fill = rgb;

            Text.Text = label;
            Text.Foreground = textBrush;
            Text.FontWeight = FontWeights.SemiBold;
            Text.IsHitTestVisible = false;
            // squash the label a little vertically to match the foreshortened key tops
            Text.RenderTransformOrigin = new Point(0.5, 0.5);
            Text.RenderTransform = new ScaleTransform(1, 0.86);

            FaceGroup.RenderTransform = FaceShift;
            FaceGroup.Children.Add(Face);
            FaceGroup.Children.Add(Tint);
            FaceGroup.Children.Add(Strip);
            FaceGroup.Children.Add(Text);

            Root.Children.Add(Base);
            Root.Children.Add(Wall);
            Root.Children.Add(FaceGroup);
        }

        // Press is applied instantly (no animation) so the key reacts the moment the event arrives;
        // only the release eases back, and that is a short 90 ms animation.
        public void SetPressed(bool pressed)
        {
            if (IsPressed == pressed) return;
            IsPressed = pressed;

            FaceShift.BeginAnimation(TranslateTransform.YProperty, null);
            if (pressed)
            {
                FaceShift.Y = PressDepth;
                Tint.Opacity = 0.45;
                Glow.Opacity = 0.85;
            }
            else
            {
                FaceShift.Y = 0;
                FaceShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(PressDepth, 0, TimeSpan.FromMilliseconds(90))
                {
                    FillBehavior = FillBehavior.Stop
                });
                Tint.Opacity = 0;
                Glow.Opacity = IdleGlowOpacity;
            }
        }
    }
}