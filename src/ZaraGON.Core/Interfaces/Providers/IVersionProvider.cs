using ZaraGON.Core.Enums;
using ZaraGON.Core.Models;

namespace ZaraGON.Core.Interfaces.Providers;

public interface IVersionProvider
{
    ServiceType ServiceType { get; }
    Task<IReadOnlyList<ServiceVersion>> FetchAvailableVersionsAsync(CancellationToken ct = default);
}
