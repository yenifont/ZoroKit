using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;

namespace ZaraGON.UI.ViewModels;

public sealed partial class LogViewModel : ObservableObject
{
    private readonly ILogWatcher _logWatcher;
    private readonly Dispatcher _dispatcher;

    [ObservableProperty]
    private string _selectedSource = "All";

    [ObservableProperty]
    private bool _autoScroll = true;

    public ObservableCollection<LogEntry> LogEntries { get; } = [];
    public ObservableCollection<string> Sources { get; } = ["All", "Apache Error", "Apache Access", "ZaraGON", "System"];

    public LogViewModel(ILogWatcher logWatcher)
    {
        _logWatcher = logWatcher;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _logWatcher.LogReceived += OnLogReceived;
        _ = LoadRecentAsync();
    }

    private void OnLogReceived(object? sender, LogEntry entry)
    {
        if (SelectedSource != "All" && !entry.Source.Equals(SelectedSource, StringComparison.OrdinalIgnoreCase))
            return;

        _dispatcher.BeginInvoke(() =>
        {
            LogEntries.Add(entry);
            if (LogEntries.Count > 1000)
                LogEntries.RemoveAt(0);
        });
    }

    private async Task LoadRecentAsync()
    {
        try
        {
            var entries = await _logWatcher.GetRecentEntriesAsync(SelectedSource == "All" ? "" : SelectedSource);
            foreach (var entry in entries)
                LogEntries.Add(entry);
        }
        catch { /* no logs yet */ }
    }

    [RelayCommand]
    private void CopyLogs()
    {
        if (LogEntries.Count == 0) return;

        var sb = new StringBuilder();
        foreach (var entry in LogEntries)
            sb.AppendLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] [{entry.Source}] {entry.Message}");

        Clipboard.SetText(sb.ToString());
    }

    [RelayCommand]
    private void ClearLogs()
    {
        LogEntries.Clear();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        LogEntries.Clear();
        await LoadRecentAsync();
    }

    partial void OnSelectedSourceChanged(string value)
    {
        _ = RefreshAsync();
    }
}
