using System.ComponentModel;
using System.Windows;
using ZaraGON.UI.Services;
using ZaraGON.UI.ViewModels;

namespace ZaraGON.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel, ToastService toastService)
    {
        InitializeComponent();
        DataContext = viewModel;
        ToastContainer.ItemsSource = toastService.Toasts;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Allow closing when application is shutting down, otherwise minimize to tray
        if (_isExiting)
            return;

        e.Cancel = true;
        Hide();
    }

    public void AllowClose() => _isExiting = true;
    private bool _isExiting;
}
