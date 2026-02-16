using System.Windows;
using ZaraGON.UI.Views;

namespace ZaraGON.UI.Services;

public sealed class DialogService
{
    public bool Confirm(string message, string title = "Onay")
    {
        return ShowOnUiThread(() =>
        {
            var dialog = ModernDialog.Create(DialogType.Confirm, message, title);
            dialog.ShowDialog();
            return dialog.DialogConfirmed;
        });
    }

    public void ShowInfo(string message, string title = "Bilgi")
    {
        ShowOnUiThread(() =>
        {
            var dialog = ModernDialog.Create(DialogType.Info, message, title);
            dialog.ShowDialog();
        });
    }

    public void ShowError(string message, string title = "Hata")
    {
        ShowOnUiThread(() =>
        {
            var dialog = ModernDialog.Create(DialogType.Error, message, title);
            dialog.ShowDialog();
        });
    }

    public void ShowWarning(string message, string title = "Uyar\u0131")
    {
        ShowOnUiThread(() =>
        {
            var dialog = ModernDialog.Create(DialogType.Warning, message, title);
            dialog.ShowDialog();
        });
    }

    public string? PromptInput(string message, string title = "Giri\u015f", string defaultValue = "")
    {
        return ShowOnUiThread(() =>
        {
            var dialog = ModernDialog.CreateInput(message, title, defaultValue);
            dialog.ShowDialog();
            return dialog.DialogConfirmed ? dialog.InputValue : null;
        });
    }

    private static void ShowOnUiThread(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
            action();
        else
            System.Windows.Application.Current?.Dispatcher.Invoke(action);
    }

    private static T ShowOnUiThread<T>(Func<T> func)
    {
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
            return func();
        return System.Windows.Application.Current!.Dispatcher.Invoke(func);
    }
}
