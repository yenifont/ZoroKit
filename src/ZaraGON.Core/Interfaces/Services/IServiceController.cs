using ZaraGON.Core.Enums;

namespace ZaraGON.Core.Interfaces.Services;

public interface IServiceController
{
    ServiceType ServiceType { get; }
    ServiceStatus Status { get; }
    event EventHandler<ServiceStatus>? StatusChanged;

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task RestartAsync(CancellationToken ct = default);
    Task<bool> ValidateConfigAsync(CancellationToken ct = default);
    Task<ServiceStatus> GetStatusAsync(CancellationToken ct = default);
    Task DetectRunningAsync(CancellationToken ct = default);
    Task ReloadAsync(CancellationToken ct = default);
    Task RegenerateConfigAsync(CancellationToken ct = default);
}
