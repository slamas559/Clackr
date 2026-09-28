using System;
using System.Diagnostics;
using System.Windows;
using KeySonic.Core.Settings;

namespace KeySonic.UI.Views;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    private App AppInstance => (App)System.Windows.Application.Current;
    private bool _suppressEvents;

    public SettingsView()
    {
        InitializeComponent();
        VersionText.Text = "KeySonic \u2014 early preview build";
    }

    /// <summary>Called every time this page is navigated to, so toggles reflect the real current state
    /// (Start with Windows in particular can change outside the app, e.g. via Windows' own Startup Apps settings).</summary>
    public void RefreshFromCurrentState()
    {
        _suppressEvents = true;
        StartWithWindowsToggle.IsChecked = StartupRegistration.IsEnabled();
        StartMinimizedToggle.IsChecked = AppInstance.Settings.StartMinimized;
        _suppressEvents = false;
    }

    private void StartWithWindowsToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        StartupRegistration.SetEnabled(StartWithWindowsToggle.IsChecked == true);
    }

    private void StartMinimizedToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        AppInstance.Settings.StartMinimized = StartMinimizedToggle.IsChecked == true;
        SettingsStore.Save(AppInstance.Settings);
    }

    private void OpenPacksFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppInstance.PacksFolderPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Couldn't open the folder:\n{ex.Message}", "KeySonic",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
