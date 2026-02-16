using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ZaraGON.UI.Services;

public enum ToastType
{
    Success,
    Error,
    Warning,
    Info
}

public sealed class ToastItem
{
    public string Message { get; init; } = string.Empty;
    public ToastType Type { get; init; }
    public string IconKind { get; init; } = "Check";
}

public sealed class ToastService
{
    private readonly Dispatcher _dispatcher;
    private readonly string? _logPath;
    private static readonly Lock _logLock = new();

    public ObservableCollection<ToastItem> Toasts { get; } = [];

    public ToastService(string? basePath = null)
    {
        _dispatcher = Dispatcher.CurrentDispatcher;

        if (!string.IsNullOrEmpty(basePath))
        {
            var logDir = Path.Combine(basePath, "logs");
            Directory.CreateDirectory(logDir);
            _logPath = Path.Combine(logDir, "zoragon.log");
        }
    }

    public void ShowSuccess(string message)
        => Show(message, ToastType.Success, "CheckCircle");

    public void ShowError(string message)
        => Show(message, ToastType.Error, "CloseCircle");

    public void ShowWarning(string message)
        => Show(message, ToastType.Warning, "AlertCircle");

    public void ShowInfo(string message)
        => Show(message, ToastType.Info, "InformationOutline");

    private void Show(string message, ToastType type, string iconKind)
    {
        // Log errors and warnings to file
        if (type is ToastType.Error or ToastType.Warning)
            WriteLog(type, message);

        _dispatcher.BeginInvoke(() =>
        {
            var toast = new ToastItem
            {
                Message = message,
                Type = type,
                IconKind = iconKind
            };

            Toasts.Add(toast);

            // Keep max 4 toasts
            while (Toasts.Count > 4)
                Toasts.RemoveAt(0);

            // Auto-dismiss after 3.5 seconds
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
            EventHandler onTick = null!;
            onTick = (_, _) =>
            {
                timer.Tick -= onTick;
                timer.Stop();
                Toasts.Remove(toast);
            };
            timer.Tick += onTick;
            timer.Start();
        });
    }

    private void WriteLog(ToastType type, string message)
    {
        if (_logPath == null) return;

        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{type.ToString().ToUpperInvariant()}] {message}{Environment.NewLine}";
            lock (_logLock)
            {
                File.AppendAllText(_logPath, line);
            }
        }
        catch { /* logging should never crash the app */ }
    }
}
