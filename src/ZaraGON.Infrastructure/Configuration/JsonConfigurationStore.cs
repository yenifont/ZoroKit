using System.Text.Json;
using System.Text.Json.Serialization;
using ZaraGON.Core.Exceptions;
using ZaraGON.Core.Interfaces.Infrastructure;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;

namespace ZaraGON.Infrastructure.Configuration;

public sealed class JsonConfigurationStore : IConfigurationManager
{
    private readonly IFileSystem _fileSystem;
    private readonly string _configPath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private AppConfiguration? _cached;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public JsonConfigurationStore(IFileSystem fileSystem, string basePath)
    {
        _fileSystem = fileSystem;
        _configPath = Path.Combine(basePath, "config", "zoragon.json");
    }

    public async Task<AppConfiguration> LoadAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            if (_cached != null)
                return _cached;

            if (!_fileSystem.FileExists(_configPath))
            {
                _cached = new AppConfiguration();
                await SaveInternalAsync(_cached, ct);
                return _cached;
            }

            var json = await _fileSystem.ReadAllTextAsync(_configPath, ct);
            _cached = JsonSerializer.Deserialize<AppConfiguration>(json, JsonOptions) ?? new AppConfiguration();
            return _cached;
        }
        catch (JsonException ex)
        {
            throw new ConfigurationException($"Failed to parse configuration: {ex.Message}", ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveAsync(AppConfiguration config, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            config.LastModified = DateTime.UtcNow;
            _cached = config;
            await SaveInternalAsync(config, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<T> GetValueAsync<T>(string key, T defaultValue, CancellationToken ct = default)
    {
        var config = await LoadAsync(ct);
        var prop = typeof(AppConfiguration).GetProperty(key);
        if (prop == null) return defaultValue;

        var value = prop.GetValue(config);
        return value is T typedValue ? typedValue : defaultValue;
    }

    public async Task SetValueAsync<T>(string key, T value, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var config = _cached ?? new AppConfiguration();
            var prop = typeof(AppConfiguration).GetProperty(key);
            if (prop == null)
                throw new ConfigurationException($"Unknown configuration key: {key}");

            prop.SetValue(config, value);
            config.LastModified = DateTime.UtcNow;
            _cached = config;
            await SaveInternalAsync(config, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task SaveInternalAsync(AppConfiguration config, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await _fileSystem.AtomicWriteAsync(_configPath, json, ct);
    }
}
