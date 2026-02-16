using System.Net.Sockets;
using ZaraGON.Core.Enums;
using ZaraGON.Core.Interfaces.Infrastructure;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;

namespace ZaraGON.Application.Services;

public sealed class HealthCheckerService : IHealthChecker
{
    private readonly IServiceController _apacheController;
    private readonly MariaDbService _mariaDbController;
    private readonly IVersionManager _versionManager;
    private readonly IProcessManager _processManager;
    private readonly IConfigurationManager _configManager;
    private readonly IVcRedistChecker _vcRedistChecker;

    public HealthCheckerService(
        IServiceController apacheController,
        MariaDbService mariaDbController,
        IVersionManager versionManager,
        IProcessManager processManager,
        IConfigurationManager configManager,
        IVcRedistChecker vcRedistChecker)
    {
        _apacheController = apacheController;
        _mariaDbController = mariaDbController;
        _versionManager = versionManager;
        _processManager = processManager;
        _configManager = configManager;
        _vcRedistChecker = vcRedistChecker;
    }

    public async Task<IReadOnlyList<HealthCheckResult>> RunAllChecksAsync(CancellationToken ct = default)
    {
        var config = await _configManager.LoadAsync(ct);

        // Run all independent checks in parallel
        var apachePortTask = CheckPortAsync(config.ApachePort, ct);
        var apacheBinaryTask = CheckBinaryAsync(ServiceType.Apache, "Apache Binary", ct);
        var phpBinaryTask = CheckBinaryAsync(ServiceType.Php, "PHP Binary", ct);
        var apacheConfigTask = CheckApacheConfigAsync(ct);
        var mariaDbPortTask = CheckPortAsync(config.MySqlPort, ct);
        var mariaDbBinaryTask = CheckBinaryAsync(ServiceType.MariaDb, "MariaDB Binary", ct);
        var mariaDbConfigTask = CheckMariaDbConfigAsync(ct);
        var vcRedistTask = CheckVcRedistAsync(config.ActivePhpVersion, ct);

        await Task.WhenAll(apachePortTask, apacheBinaryTask, phpBinaryTask,
            apacheConfigTask, mariaDbPortTask, mariaDbBinaryTask, mariaDbConfigTask, vcRedistTask);

        return
        [
            await apachePortTask,
            await apacheBinaryTask,
            await phpBinaryTask,
            await apacheConfigTask,
            await mariaDbPortTask,
            await mariaDbBinaryTask,
            await mariaDbConfigTask,
            await vcRedistTask
        ];
    }

    private async Task<HealthCheckResult> CheckBinaryAsync(ServiceType type, string checkName, CancellationToken ct)
    {
        try
        {
            var path = await _versionManager.GetBinaryPathAsync(type, ct);
            var exists = File.Exists(path);
            return new HealthCheckResult
            {
                CheckName = checkName,
                IsHealthy = exists,
                Message = exists ? $"Found at {path}" : $"Not found at {path}"
            };
        }
        catch
        {
            return new HealthCheckResult
            {
                CheckName = checkName,
                IsHealthy = false,
                Message = $"No active {checkName.Replace(" Binary", "")} version configured"
            };
        }
    }

    private async Task<HealthCheckResult> CheckApacheConfigAsync(CancellationToken ct)
    {
        var configValid = await _apacheController.ValidateConfigAsync(ct);
        return new HealthCheckResult
        {
            CheckName = "Apache Config",
            IsHealthy = configValid,
            Message = configValid ? "Configuration is valid" : "Configuration has errors"
        };
    }

    private async Task<HealthCheckResult> CheckMariaDbConfigAsync(CancellationToken ct)
    {
        var configValid = await _mariaDbController.ValidateConfigAsync(ct);
        return new HealthCheckResult
        {
            CheckName = "MariaDB Config",
            IsHealthy = configValid,
            Message = configValid ? "Configuration is valid" : "Configuration missing or invalid"
        };
    }

    public async Task<HealthCheckResult> CheckPortAsync(int port, CancellationToken ct = default)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync("127.0.0.1", port, ct).AsTask();
            var completed = await Task.WhenAny(connectTask, Task.Delay(2000, ct));

            if (completed == connectTask && client.Connected)
            {
                return new HealthCheckResult
                {
                    CheckName = $"Port {port}",
                    IsHealthy = true,
                    Message = $"Port {port} is listening"
                };
            }
        }
        catch { /* port not listening */ }

        return new HealthCheckResult
        {
            CheckName = $"Port {port}",
            IsHealthy = false,
            Message = $"Port {port} is not responding"
        };
    }

    public async Task<HealthCheckResult> CheckProcessAsync(int processId, CancellationToken ct = default)
    {
        var isRunning = await _processManager.IsProcessRunningAsync(processId, ct);
        return new HealthCheckResult
        {
            CheckName = $"Process {processId}",
            IsHealthy = isRunning,
            Message = isRunning ? $"Process {processId} is running" : $"Process {processId} is not running"
        };
    }

    private async Task<HealthCheckResult> CheckVcRedistAsync(string? activePhpVersion, CancellationToken ct)
    {
        try
        {
            // Determine vsVersion from active PHP installation
            string? vsVersion = null;
            if (!string.IsNullOrEmpty(activePhpVersion))
            {
                var installed = await _versionManager.GetInstalledVersionsAsync(ServiceType.Php, ct);
                var active = installed.FirstOrDefault(v => v.IsActive);
                vsVersion = active?.VsVersion;
            }

            var status = await _vcRedistChecker.CheckCompatibilityAsync(vsVersion, ct);

            if (status.IsCompatible)
            {
                var versionText = status.InstalledVersion != null
                    ? $"v{status.InstalledVersion.Major}.{status.InstalledVersion.Minor}.{status.InstalledVersion.Build}"
                    : "mevcut";
                return new HealthCheckResult
                {
                    CheckName = "VC++ Runtime",
                    IsHealthy = true,
                    Message = $"VC++ Redistributable uyumlu ({versionText})"
                };
            }

            var installedText = status.InstalledVersion != null
                ? $"v{status.InstalledVersion.Major}.{status.InstalledVersion.Minor}.{status.InstalledVersion.Build}"
                : "yüklü değil";
            var requiredText = status.RequiredMinimumVersion != null
                ? $"v{status.RequiredMinimumVersion.Major}.{status.RequiredMinimumVersion.Minor}"
                : "bilinmiyor";

            return new HealthCheckResult
            {
                CheckName = "VC++ Runtime",
                IsHealthy = false,
                Message = $"VC++ Redistributable uyumsuz (mevcut: {installedText}, gereken: {requiredText}+)"
            };
        }
        catch
        {
            return new HealthCheckResult
            {
                CheckName = "VC++ Runtime",
                IsHealthy = false,
                Message = "VC++ Redistributable durumu kontrol edilemedi"
            };
        }
    }

    public Task<HealthCheckResult> CheckConfigAsync(string configPath, CancellationToken ct = default)
    {
        var exists = File.Exists(configPath);
        return Task.FromResult(new HealthCheckResult
        {
            CheckName = $"Config: {Path.GetFileName(configPath)}",
            IsHealthy = exists,
            Message = exists ? "Configuration file exists" : "Configuration file not found"
        });
    }
}
