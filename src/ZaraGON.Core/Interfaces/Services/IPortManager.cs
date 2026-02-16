using ZaraGON.Core.Models;

namespace ZaraGON.Core.Interfaces.Services;

public interface IPortManager
{
    Task<bool> IsPortAvailableAsync(int port, CancellationToken ct = default);
    Task<PortConflict?> GetPortConflictAsync(int port, CancellationToken ct = default);
    Task<IReadOnlyList<PortBinding>> GetActiveBindingsAsync(CancellationToken ct = default);
    Task<bool> KillProcessOnPortAsync(int port, CancellationToken ct = default);
}
