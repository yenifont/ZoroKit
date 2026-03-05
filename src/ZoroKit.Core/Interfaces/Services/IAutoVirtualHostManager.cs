namespace ZoroKit.Core.Interfaces.Services;

public interface IAutoVirtualHostManager
{
    Task ScanAndApplyAsync(CancellationToken ct = default);
    /// <summary>
    /// Hostname (örn. migrations.test) için www klasörü ve Apache vhost oluşturur;
    /// böylece tarayıcıda açıldığında localhost gibi çalışır.
    /// </summary>
    /// <param name="hostname">Domain adı (örn. sitem.com)</param>
    /// <param name="subFolder">Opsiyonel: www altındaki mevcut klasör (örn. "zoro"). Null ise hostname'den türetilir.</param>
    Task EnsureVHostForHostnameAsync(string hostname, string? subFolder = null, CancellationToken ct = default);
    /// <summary>
    /// İlk kurulumda varsayılan zorokit.app host kaydı ve ana www vhost'unu ekler.
    /// </summary>
    Task EnsureDefaultZoroKitHostAsync(CancellationToken ct = default);
    /// <summary>
    /// Hostname için tanımlı VHost conf dosyasından alt klasör bilgisini döndürür.
    /// Alt klasör yoksa veya DocumentRoot doğrudan www ise null döner.
    /// </summary>
    Task<string?> GetSubFolderForHostnameAsync(string hostname, CancellationToken ct = default);
    /// <summary>
    /// Hostname'e ait VHost conf dosyalarını (manual.*, auto.*, SSL dahil) siler.
    /// </summary>
    Task RemoveVHostForHostnameAsync(string hostname, CancellationToken ct = default);
    Task StartWatchingAsync(CancellationToken ct = default);
    Task StopWatchingAsync();
    IReadOnlyList<string> GetDetectedSites();
    event EventHandler<string>? SiteAdded;
    event EventHandler<string>? SiteRemoved;
}
