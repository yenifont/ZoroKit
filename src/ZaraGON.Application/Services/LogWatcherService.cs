using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;

namespace ZaraGON.Application.Services;

public sealed class LogWatcherService : ILogWatcher
{
    private readonly ILogWatcher _inner;

    public LogWatcherService(ILogWatcher inner)
    {
        _inner = inner;
    }

    public event EventHandler<LogEntry>? LogReceived
    {
        add => _inner.LogReceived += value;
        remove => _inner.LogReceived -= value;
    }

    public Task StartWatchingAsync(string filePath, string source, CancellationToken ct = default)
        => _inner.StartWatchingAsync(filePath, source, ct);

    public Task StopWatchingAsync(string filePath)
        => _inner.StopWatchingAsync(filePath);

    public Task StopAllAsync()
        => _inner.StopAllAsync();

    public Task<IReadOnlyList<LogEntry>> GetRecentEntriesAsync(string source, int count = 100, CancellationToken ct = default)
        => _inner.GetRecentEntriesAsync(source, count, ct);
}
