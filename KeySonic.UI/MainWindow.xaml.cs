using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KeySonic.Core.Keyboard;
using KeySonic.UI.Views;

namespace KeySonic.UI;

public partial class MainWindow : Window
{
    private const int WmNcHitTest = 0x0084;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private const double ResizeBorderThickness = 8;

    /// <summary>Raised when the user toggles Keyboard Sounds, so the tray menu checkbox can stay in sync.</summary>
    public event Action<bool>? EnabledChanged;

    private readonly DashboardView _dashboard = new();
    private readonly SoundPackBrowserView _browser = new();
    private readonly SoundLabView _soundLab = new();
    private readonly SettingsView _settings = new();

    public MainWindow()
    {
        InitializeComponent();

        _dashboard.ChangeSoundRequested += NavigateToBrowser;
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
        Closing += MainWindow_Closing;
    }

    /// <summary>Called once by App right after construction, to apply settings loaded from disk
    /// before the window is shown - avoids a visible flash of default values.</summary>
    public void ApplySettingsOnLoad(float masterVolume, bool keyboardSoundsEnabled)
    {
        _dashboard.SetInitialState(masterVolume, keyboardSoundsEnabled);
    }

    // ===== Navigation =====

    private void NavigateToBrowser()
    {
        _browser.RefreshCards();
        NavigateTo(_browser, "Sound packs");
    }

    private void NavigateToSettings()
    {
        _settings.RefreshFromCurrentState();
        NavigateTo(_settings, "Settings");
    }

    private void NavigateToSoundLab()
    {
        _soundLab.RefreshFromCurrentState();
        NavigateTo(_soundLab, "Sound Lab");
    }

    private void NavigateToDashboard()
    {
        NavigateTo(_dashboard, "Dashboard");
    }

    private void NavigateTo(System.Windows.Controls.UserControl view, string pageTitle)
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

    private void SoundLabButton_Click(object sender, RoutedEventArgs e) => NavigateToSoundLab();

    private void DashboardNavButton_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNavigation(DashboardNavButton);
        NavigateToDashboard();
    }

    private void SoundPacksNavButton_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNavigation(SoundPacksNavButton);
        NavigateToBrowser();
    }

    private void SettingsNavButton_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNavigation(SettingsNavButton);
        NavigateToSettings();
    }

    private void SetActiveNavigation(Button activeButton)
    {
        DashboardNavButton.Tag = ReferenceEquals(activeButton, DashboardNavButton) ? "Active" : "Inactive";
        SoundPacksNavButton.Tag = ReferenceEquals(activeButton, SoundPacksNavButton) ? "Active" : "Inactive";
        SettingsNavButton.Tag = ReferenceEquals(activeButton, SettingsNavButton) ? "Active" : "Inactive";
    }

    // ===== Called from App.xaml.cs (tray icon sync) =====

    public void SyncEnabledState(bool isEnabled) => _dashboard.SyncEnabledState(isEnabled);

    public void RefreshActivePackDisplay() => _dashboard.RefreshActivePackDisplay();

    // ===== Window chrome =====

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Alt+F4 / taskbar close / the X button all close to tray. Only the tray's
        // "Exit" item actually terminates the app.
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
        Dispatcher.BeginInvoke(() => _soundLab.FlashPhysicalKeyPress(e.Key));
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

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
}
