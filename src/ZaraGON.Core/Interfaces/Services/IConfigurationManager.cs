using ZaraGON.Core.Models;

namespace ZaraGON.Core.Interfaces.Services;

public interface IConfigurationManager
{
    Task<AppConfiguration> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppConfiguration config, CancellationToken ct = default);
    Task<T> GetValueAsync<T>(string key, T defaultValue, CancellationToken ct = default);
    Task SetValueAsync<T>(string key, T value, CancellationToken ct = default);
}
