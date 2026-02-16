using ZaraGON.Core.Models;

namespace ZaraGON.Core.Interfaces.Services;

public interface IHealthChecker
{
    Task<IReadOnlyList<HealthCheckResult>> RunAllChecksAsync(CancellationToken ct = default);
    Task<HealthCheckResult> CheckPortAsync(int port, CancellationToken ct = default);
    Task<HealthCheckResult> CheckProcessAsync(int processId, CancellationToken ct = default);
    Task<HealthCheckResult> CheckConfigAsync(string configPath, CancellationToken ct = default);
}
