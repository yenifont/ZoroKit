using ZaraGON.Core.Models;

namespace ZaraGON.Core.Interfaces.Services;

public interface IHostsFileManager
{
    Task<IReadOnlyList<HostEntry>> GetManagedEntriesAsync(CancellationToken ct = default);
    Task AddEntryAsync(HostEntry entry, CancellationToken ct = default);
    Task RemoveEntryAsync(string hostname, CancellationToken ct = default);
    Task SyncEntriesAsync(IEnumerable<HostEntry> entries, CancellationToken ct = default);
    Task<bool> RequiresElevationAsync(CancellationToken ct = default);
}
