using ZaraGON.Core.Enums;
using ZaraGON.Core.Models;

namespace ZaraGON.Core.Interfaces.Services;

public interface IVersionManager
{
    Task<IReadOnlyList<ServiceVersion>> GetAvailableVersionsAsync(ServiceType serviceType, CancellationToken ct = default);
    Task<IReadOnlyList<VersionPointer>> GetInstalledVersionsAsync(ServiceType serviceType, CancellationToken ct = default);
    Task<VersionPointer?> GetActiveVersionAsync(ServiceType serviceType, CancellationToken ct = default);
    Task InstallVersionAsync(ServiceVersion version, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default);
    Task SetActiveVersionAsync(ServiceType serviceType, string version, CancellationToken ct = default);
    Task UninstallVersionAsync(ServiceType serviceType, string version, CancellationToken ct = default);
    Task<string> GetBinaryPathAsync(ServiceType serviceType, CancellationToken ct = default);
}
