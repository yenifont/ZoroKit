using ZoroKit.Core.Enums;
using ZoroKit.Core.Models;

namespace ZoroKit.Core.Interfaces.Providers;

public interface IVersionProvider
{
    ServiceType ServiceType { get; }
    Task<IReadOnlyList<ServiceVersion>> FetchAvailableVersionsAsync(CancellationToken ct = default);
}
