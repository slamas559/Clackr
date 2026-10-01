using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KeySonic.Core.Keyboard;
using KeySonic.Core.Settings;
using KeySonic.UI.Views;

namespace KeySonic.UI;

public partial class MainWindow : Window
{
    private sealed record NavigationEntry(System.Windows.Controls.UserControl View, string Title, Button ActiveNavigationButton);
    private const int WmNcHitTest = 0x0084;
    private const int WmGetMinMaxInfo = 0x0024;
    private const uint MonitorDefaultToNearest = 0x00000002;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private const double ResizeBorderThickness = 8;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr windowHandle, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitorHandle, ref MonitorInfo monitorInfo);

    /// <summary>Raised when the user toggles Keyboard Sounds, so the tray menu checkbox can stay in sync.</summary>
    public event Action<bool>? EnabledChanged;

    private readonly DashboardView _dashboard = new();
    private readonly SoundPackBrowserView _browser = new();
    private readonly MouseClickSoundsView _mouseSounds = new();
    private readonly SoundLabView _soundLab = new();
    private readonly SettingsView _settings = new();
    private readonly Stack<NavigationEntry> _navigationHistory = new();
    private NavigationEntry _currentNavigation = null!;

    public MainWindow()
    {
        InitializeComponent();
        _currentNavigation = new NavigationEntry(_dashboard, "Dashboard", DashboardNavButton);

        _dashboard.ChangeSoundRequested += NavigateToBrowser;
        _dashboard.MouseSoundsRequested += NavigateToMouseSounds;
        _dashboard.EnabledChanged += isEnabled => EnabledChanged?.Invoke(isEnabled);
        _browser.PackActivated += () =>
        {
            _dashboard.RefreshActivePackDisplay();
            NavigateToDashboard();
        };
        ((App)Application.Current).KeyboardHook.KeyDown += KeyboardHook_KeyDown;

        PageHost.Content = _dashboard;

        SourceInitialized += MainWindow_SourceInitialized;
        StateChanged += (_, _) => UpdateMaximizeRestoreButton();
        Loaded += (_, _) => _dashboard.RefreshActivePackDisplay();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Closing += MainWindow_Closing;
    }

    /// <summary>Called once by App right after construction, to apply settings loaded from disk
    /// before the window is shown - avoids a visible flash of default values.</summary>
    public void ApplySettingsOnLoad(float masterVolume, bool keyboardSoundsEnabled, double windowWidth, double windowHeight)
    {
        Rect workArea = SystemParameters.WorkArea;
        double maximumWidth = Math.Max(640, workArea.Width - 24);
        double maximumHeight = Math.Max(480, workArea.Height - 24);
        MinWidth = Math.Min(MinWidth, maximumWidth);
        MinHeight = Math.Min(MinHeight, maximumHeight);
        Width = Math.Clamp(double.IsFinite(windowWidth) ? windowWidth : 1060, MinWidth, maximumWidth);
        Height = Math.Clamp(double.IsFinite(windowHeight) ? windowHeight : 720, MinHeight, maximumHeight);
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + (workArea.Height - Height) / 2;
        _dashboard.SetInitialState(masterVolume, keyboardSoundsEnabled);
    }

    public void SaveWindowSize()
    {
        Rect bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var app = (App)Application.Current;
        app.Settings.WindowWidth = bounds.Width;
        app.Settings.WindowHeight = bounds.Height;
        SettingsStore.Save(app.Settings);
    }

    // ===== Navigation =====

    private void NavigateToBrowser()
    {
        _browser.RefreshCards();
        NavigateTo(_browser, "Sound packs", SoundPacksNavButton);
    }

    private void NavigateToSettings()
    {
        _settings.RefreshFromCurrentState();
        NavigateTo(_settings, "Settings", SettingsNavButton);
    }

    private void NavigateToMouseSounds()
    {
        _mouseSounds.RefreshLibrary();
        NavigateTo(_mouseSounds, "Mouse clicks", MouseSoundsNavButton);
    }

    private void NavigateToSoundLab()
    {
        _soundLab.RefreshFromCurrentState();
        NavigateTo(_soundLab, "Sound Lab");
    }

    private void NavigateToDashboard()
    {
        _dashboard.RefreshControlCenter();
        NavigateTo(_dashboard, "Dashboard", DashboardNavButton);
    }

    private void NavigateTo(System.Windows.Controls.UserControl view, string pageTitle, Button? activeNavigationButton = null)
    {
        if (ReferenceEquals(PageHost.Content, view)) return;

        _navigationHistory.Push(_currentNavigation);
        _currentNavigation = new NavigationEntry(view, pageTitle,
            activeNavigationButton ?? _currentNavigation.ActiveNavigationButton);
        UpdateNavigationControls();
        ShowPage(view, pageTitle);
    }

    private void ShowPage(System.Windows.Controls.UserControl view, string pageTitle)
    {
        PageHeaderText.Text = pageTitle;
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(100));
        fadeOut.Completed += (_, _) =>
        {
            PageHost.Content = view;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140));
            PageHost.BeginAnimation(OpacityProperty, fadeIn);
        };
        PageHost.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_navigationHistory.Count == 0) return;
        _currentNavigation = _navigationHistory.Pop();
        UpdateNavigationControls();
        ShowPage(_currentNavigation.View, _currentNavigation.Title);
    }

    private void UpdateNavigationControls()
    {
        BackButton.IsEnabled = _navigationHistory.Count > 0;
        SetActiveNavigation(_currentNavigation.ActiveNavigationButton);
    }

    private void SoundLabButton_Click(object sender, RoutedEventArgs e) => NavigateToSoundLab();

    private void DashboardNavButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToDashboard();
    }

    private void SoundPacksNavButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToBrowser();
    }

    private void MouseSoundsNavButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToMouseSounds();
    }

    private void SettingsNavButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToSettings();
    }

    private void SetActiveNavigation(Button activeButton)
    {
        DashboardNavButton.Tag = ReferenceEquals(activeButton, DashboardNavButton) ? "Active" : "Inactive";
        SoundPacksNavButton.Tag = ReferenceEquals(activeButton, SoundPacksNavButton) ? "Active" : "Inactive";
        MouseSoundsNavButton.Tag = ReferenceEquals(activeButton, MouseSoundsNavButton) ? "Active" : "Inactive";
        SettingsNavButton.Tag = ReferenceEquals(activeButton, SettingsNavButton) ? "Active" : "Inactive";
    }

    // ===== Called from App.xaml.cs (tray icon sync) =====

    public void SyncEnabledState(bool isEnabled) => _dashboard.SyncEnabledState(isEnabled);

    public void RefreshActivePackDisplay() => _dashboard.RefreshControlCenter();

    // ===== Window chrome =====

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Alt+F4 / taskbar close / the X button all close to tray. Only the tray's
        // "Exit" item actually terminates the app.
        SaveWindowSize();
        e.Cancel = true;
        Hide();
    }

    private void TitleBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source != null && !ReferenceEquals(source, TitleBar))
        {
            if (source is Button)
            {
                return;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        if (e.ClickCount == 2)
        {
            MaximizeRestore_Click(sender, e);
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // The mouse may be released before WPF enters the native move loop.
            }
        }
    }

    private void KeyboardHook_KeyDown(object? sender, KeyEventData e)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsActive || !ReferenceEquals(PageHost.Content, _soundLab)) return;
            if (!e.IsRepeat) _soundLab.HandleTypingGameKey(e.Key);
            _soundLab.FlashPhysicalKeyPress(e.Key);
        });
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsActive || !ReferenceEquals(PageHost.Content, _soundLab) || !_soundLab.IsTypingGameActive) return;
        if (e.Key is Key.Back or Key.Space or Key.OemComma or Key.OemPeriod or Key.OemMinus ||
            e.Key is >= Key.A and <= Key.Z || e.Key is >= Key.D0 and <= Key.D9)
        {
            e.Handled = true;
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WindowMessageHook);
        }
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmGetMinMaxInfo)
        {
            ApplyMonitorWorkArea(hwnd, lParam);
            handled = true;
            return IntPtr.Zero;
        }

        if (message != WmNcHitTest || WindowState == WindowState.Maximized)
        {
            return IntPtr.Zero;
        }

        long packedPoint = lParam.ToInt64();
        var screenPoint = new Point((short)(packedPoint & 0xffff), (short)((packedPoint >> 16) & 0xffff));
        Point windowPoint = PointFromScreen(screenPoint);
        double borderX = ResizeBorderThickness;
        double borderY = ResizeBorderThickness;
        bool left = windowPoint.X <= borderX;
        bool right = windowPoint.X >= ActualWidth - borderX;
        bool top = windowPoint.Y <= borderY;
        bool bottom = windowPoint.Y >= ActualHeight - borderY;

        int hitTest = (left, top, right, bottom) switch
        {
            (true, true, _, _) => HtTopLeft,
            (_, true, true, _) => HtTopRight,
            (true, _, _, true) => HtBottomLeft,
            (_, _, true, true) => HtBottomRight,
            (true, _, _, _) => HtLeft,
            (_, _, true, _) => HtRight,
            (_, true, _, _) => HtTop,
            (_, _, _, true) => HtBottom,
            _ => 0
        };

        if (hitTest == 0)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(hitTest);
    }

    private static void ApplyMonitorWorkArea(IntPtr windowHandle, IntPtr minMaxInfoPointer)
    {
        IntPtr monitorHandle = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
        if (monitorHandle == IntPtr.Zero) return;

        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitorHandle, ref monitorInfo)) return;

        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(minMaxInfoPointer);
        minMaxInfo.MaxPosition = new NativePoint
        {
            X = monitorInfo.Work.Left - monitorInfo.Monitor.Left,
            Y = monitorInfo.Work.Top - monitorInfo.Monitor.Top
        };
        minMaxInfo.MaxSize = new NativePoint
        {
            X = monitorInfo.Work.Right - monitorInfo.Work.Left,
            Y = monitorInfo.Work.Bottom - monitorInfo.Work.Top
        };
        Marshal.StructureToPtr(minMaxInfo, minMaxInfoPointer, false);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        // A real minimize - the window collapses to the taskbar like any normal
        // Windows app. This is distinct from the close button, which hides fully
        // to the system tray instead.
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void UpdateMaximizeRestoreButton()
    {
        bool isMaximized = WindowState == WindowState.Maximized;
        MaximizeRestoreIcon.Text = isMaximized ? "\uE923" : "\uE922";
        MaximizeRestoreButton.ToolTip = isMaximized ? "Restore" : "Maximize";
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => ((App)Application.Current).ExitApplication();

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
}
