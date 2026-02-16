using ZaraGON.Core.Models;

namespace ZaraGON.Core.Interfaces.Services;

public interface ILogWatcher
{
    event EventHandler<LogEntry>? LogReceived;
    Task StartWatchingAsync(string filePath, string source, CancellationToken ct = default);
    Task StopWatchingAsync(string filePath);
    Task StopAllAsync();
    Task<IReadOnlyList<LogEntry>> GetRecentEntriesAsync(string source, int count = 100, CancellationToken ct = default);
}
