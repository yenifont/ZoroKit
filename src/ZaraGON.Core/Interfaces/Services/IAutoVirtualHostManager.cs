namespace ZaraGON.Core.Interfaces.Services;

public interface IAutoVirtualHostManager
{
    Task ScanAndApplyAsync(CancellationToken ct = default);
    /// <summary>
    /// Hostname (örn. migrations.test) için www klasörü ve Apache vhost oluşturur;
    /// böylece tarayıcıda açıldığında localhost gibi çalışır.
    /// </summary>
    Task EnsureVHostForHostnameAsync(string hostname, CancellationToken ct = default);
    /// <summary>
    /// İlk kurulumda varsayılan zaragon.test (veya .app) host kaydı ve ana www vhost'unu ekler.
    /// </summary>
    Task EnsureDefaultZaragonHostAsync(CancellationToken ct = default);
    Task StartWatchingAsync(CancellationToken ct = default);
    Task StopWatchingAsync();
    IReadOnlyList<string> GetDetectedSites();
    event EventHandler<string>? SiteAdded;
    event EventHandler<string>? SiteRemoved;
}
