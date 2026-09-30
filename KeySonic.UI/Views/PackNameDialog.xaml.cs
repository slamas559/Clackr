using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace KeySonic.UI.Views;

public partial class PackNameDialog : Window
{
    private TextBox _packNameTextBox = null!;
    private TextBlock _validationText = null!;

    public string PackName => _packNameTextBox.Text.Trim();

    public PackNameDialog(string suggestedName)
    {
        string resourceName = $"/{GetType().Assembly.GetName().Name};component/Views/PackNameDialog.xaml";
        Application.LoadComponent(this, new Uri(resourceName, UriKind.Relative));
        _packNameTextBox = (TextBox?)FindName("PackNameTextBox")
                           ?? throw new InvalidDataException("Pack name input is missing from the dialog.");
        _validationText = (TextBlock?)FindName("ValidationText")
                          ?? throw new InvalidDataException("Validation text is missing from the dialog.");
        _packNameTextBox.Text = suggestedName;
        _packNameTextBox.SelectAll();
        Loaded += (_, _) => _packNameTextBox.Focus();
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        string name = PackName;
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            _validationText.Text = "Enter a valid folder name for this sound pack.";
            _packNameTextBox.Focus();
            return;
        }

        DialogResult = true;
    }
}