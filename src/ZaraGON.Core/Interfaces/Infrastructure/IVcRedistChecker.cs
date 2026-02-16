using ZaraGON.Core.Models;

namespace ZaraGON.Core.Interfaces.Infrastructure;

public interface IVcRedistChecker
{
    Task<Version?> GetInstalledVersionAsync(CancellationToken ct = default);
    Task<VcRedistStatus> CheckCompatibilityAsync(string? vsVersion, CancellationToken ct = default);
    Task<VcRedistStatus> TestPhpBinaryAsync(string phpExePath, CancellationToken ct = default);
    Task InstallAsync(IProgress<double>? progress = null, CancellationToken ct = default);
}
