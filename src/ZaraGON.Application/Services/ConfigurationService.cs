using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;

namespace ZaraGON.Application.Services;

public sealed class ConfigurationService : IConfigurationManager
{
    private readonly IConfigurationManager _store;

    public ConfigurationService(IConfigurationManager store)
    {
        _store = store;
    }

    public Task<AppConfiguration> LoadAsync(CancellationToken ct = default)
        => _store.LoadAsync(ct);

    public Task SaveAsync(AppConfiguration config, CancellationToken ct = default)
        => _store.SaveAsync(config, ct);

    public Task<T> GetValueAsync<T>(string key, T defaultValue, CancellationToken ct = default)
        => _store.GetValueAsync(key, defaultValue, ct);

    public Task SetValueAsync<T>(string key, T value, CancellationToken ct = default)
        => _store.SetValueAsync(key, value, ct);
}
