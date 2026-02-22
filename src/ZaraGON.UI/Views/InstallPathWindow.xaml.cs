using System.IO;
using System.Windows;
using SWF = System.Windows.Forms;

namespace ZaraGON.UI.Views;

public partial class InstallPathWindow : Window
{
    public string SelectedPath { get; private set; } = @"C:\ZaraGON";

    public InstallPathWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += (_, _) => DragMove();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new SWF.FolderBrowserDialog
        {
            Description = "ZaraGON kurulum dizinini seçin",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = PathTextBox.Text
        };

        if (dialog.ShowDialog() == SWF.DialogResult.OK)
        {
            PathTextBox.Text = dialog.SelectedPath;
        }
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        var path = PathTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(path))
        {
            ShowError("Kurulum dizini boş olamaz.");
            return;
        }

        if (path.Length < 3 || !Path.IsPathFullyQualified(path))
        {
            ShowError("Lütfen geçerli bir tam dizin yolu girin. Örn: C:\\ZaraGON");
            return;
        }

        var invalidChars = Path.GetInvalidPathChars();
        if (path.IndexOfAny(invalidChars) >= 0)
        {
            ShowError("Dizin yolu geçersiz karakterler içeriyor.");
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);

            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }

            var testFile = Path.Combine(fullPath, ".zaragon_write_test");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);

            SelectedPath = fullPath;
            DialogResult = true;
            Close();
        }
        catch (UnauthorizedAccessException)
        {
            ShowError("Bu dizine yazma izniniz yok. Farklı bir dizin seçin veya uygulamayı yönetici olarak çalıştırın.");
        }
        catch (Exception ex)
        {
            ShowError($"Dizin oluşturulamadı: {ex.Message}");
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
