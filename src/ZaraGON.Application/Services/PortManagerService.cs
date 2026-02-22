using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;

namespace ZaraGON.Application.Services;

public sealed class PortManagerService : IPortManager
{
    private readonly IPortManager _inner;

    public PortManagerService(IPortManager inner)
    {
        _inner = inner;
    }

    public Task<bool> IsPortAvailableAsync(int port, CancellationToken ct = default)
        => _inner.IsPortAvailableAsync(port, ct);

    public Task<PortConflict?> GetPortConflictAsync(int port, CancellationToken ct = default)
        => _inner.GetPortConflictAsync(port, ct);

    public Task<IReadOnlyList<PortBinding>> GetActiveBindingsAsync(CancellationToken ct = default)
        => _inner.GetActiveBindingsAsync(ct);

    public Task<bool> KillProcessOnPortAsync(int port, CancellationToken ct = default)
        => _inner.KillProcessOnPortAsync(port, ct);

    public Task<int?> FindAvailablePortAsync(int startPort, int maxPort = 65535, CancellationToken ct = default)
        => _inner.FindAvailablePortAsync(startPort, maxPort, ct);
}
