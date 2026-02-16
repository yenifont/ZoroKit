namespace ZaraGON.Core.Interfaces.Services;

public interface IAutoVirtualHostManager
{
    Task ScanAndApplyAsync(CancellationToken ct = default);
    Task StartWatchingAsync(CancellationToken ct = default);
    Task StopWatchingAsync();
    IReadOnlyList<string> GetDetectedSites();
    event EventHandler<string>? SiteAdded;
    event EventHandler<string>? SiteRemoved;
}
