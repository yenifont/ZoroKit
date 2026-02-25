using ZaraGON.Application.ConfigGeneration;
using ZaraGON.Core.Constants;
using ZaraGON.Core.Enums;
using ZaraGON.Core.Exceptions;
using ZaraGON.Core.Interfaces.Infrastructure;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;

namespace ZaraGON.Application.Services;

public sealed class ApacheService : IServiceController
{
    private readonly IVersionManager _versionManager;
    private readonly IProcessManager _processManager;
    private readonly IPortManager _portManager;
    private readonly IConfigurationManager _configManager;
    private readonly IFileSystem _fileSystem;
    private readonly ApacheConfigGenerator _configGenerator;
    private readonly string _basePath;
    private int? _processId;

    public ServiceType ServiceType => ServiceType.Apache;
    public ServiceStatus Status { get; private set; } = ServiceStatus.Stopped;
    public event EventHandler<ServiceStatus>? StatusChanged;

    public ApacheService(
        IVersionManager versionManager,
        IProcessManager processManager,
        IPortManager portManager,
        IConfigurationManager configManager,
        IFileSystem fileSystem,
        ApacheConfigGenerator configGenerator,
        string basePath)
    {
        _versionManager = versionManager;
        _processManager = processManager;
        _portManager = portManager;
        _configManager = configManager;
        _fileSystem = fileSystem;
        _configGenerator = configGenerator;
        _basePath = basePath;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (Status == ServiceStatus.Running)
            return;

        SetStatus(ServiceStatus.Starting);

        try
        {
            var config = await _configManager.LoadAsync(ct);

            // Check port availability
            if (!await _portManager.IsPortAvailableAsync(config.ApachePort, ct))
            {
                var conflict = await _portManager.GetPortConflictAsync(config.ApachePort, ct);
                throw new PortConflictException(config.ApachePort, conflict?.ProcessId);
            }

            // Get Apache binary path
            string httpdPath;
            try
            {
                httpdPath = await _versionManager.GetBinaryPathAsync(ServiceType.Apache, ct);
            }
            catch (VersionNotFoundException)
            {
                throw new ServiceStartException("Apache",
                    "No Apache version installed. Go to the Apache page, fetch available versions, and download one first.");
            }

            if (!_fileSystem.FileExists(httpdPath))
                throw new ServiceStartException("Apache", $"httpd.exe not found at: {httpdPath}");

            // Get PHP version info early — needed for DLL copy before validation
            var phpVersion = await _versionManager.GetActiveVersionAsync(ServiceType.Php, ct);

            // Copy VC++ runtime DLLs from PHP dir to Apache bin dir BEFORE validation
            // httpd -t loads php module which needs correct VCRUNTIME — must copy first
            // (same approach as XAMPP/Laragon)
            if (phpVersion != null)
            {
                var apacheBinDir = Path.GetDirectoryName(httpdPath)!;
                CopyRuntimeDlls(phpVersion.InstallPath, apacheBinDir);
            }

            // Generate config
            await GenerateConfigAsync(config, ct);

            // Validate config (now with correct DLLs in place)
            if (!await ValidateConfigAsync(ct))
            {
                var errorDetail = await GetConfigValidationErrorAsync(ct);
                var message = string.IsNullOrWhiteSpace(errorDetail)
                    ? "Yapılandırma hatası. Ayarlar > Apache veya Loglar sayfasından httpd.conf kontrol edin."
                    : $"Yapılandırma hatası: {errorDetail.Trim()}";
                throw new ServiceStartException("Apache", message);
            }

            var configPath = GetConfigPath();
            var workingDir = Path.GetDirectoryName(httpdPath);

            // Add PHP directory to PATH so extensions like intl can find their DLL dependencies (e.g. ICU)
            Dictionary<string, string>? envVars = null;
            if (phpVersion != null)
            {
                var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                envVars = new Dictionary<string, string>
                {
                    ["PATH"] = phpVersion.InstallPath + ";" + currentPath
                };
            }

            _processId = await _processManager.StartProcessAsync(
                httpdPath, $"-f \"{configPath}\"", workingDir, envVars, ct);

            // Poll until port is bound (max 3 seconds)
            // Also check if process crashed early (e.g. PHP VC++ incompatibility)
            for (int i = 0; i < 15; i++)
            {
                await Task.Delay(200, ct);

                // Check if process crashed
                if (!await _processManager.IsProcessRunningAsync(_processId.Value, ct))
                {
                    _processId = null;
                    var errorDetail = await ReadLastErrorLogLineAsync(ct);
                    var message = string.IsNullOrEmpty(errorDetail)
                        ? "Apache başlatıldıktan hemen sonra çöktü. Hata logunu kontrol edin."
                        : $"Apache başlatılamadı: {errorDetail}";
                    throw new ServiceStartException("Apache", message);
                }

                if (!await _portManager.IsPortAvailableAsync(config.ApachePort, ct))
                {
                    SetStatus(ServiceStatus.Running);
                    return;
                }
            }

            // Process is still alive but port not bound — kill it
            try { await _processManager.KillProcessAsync(_processId.Value, ct); } catch { }
            _processId = null;
            throw new ServiceStartException("Apache", "Apache başlatıldı ancak port dinlemeye başlamadı.");
        }
        catch (Exception) when (Status == ServiceStatus.Starting)
        {
            SetStatus(ServiceStatus.Error);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (Status != ServiceStatus.Running || !_processId.HasValue)
        {
            SetStatus(ServiceStatus.Stopped);
            return;
        }

        SetStatus(ServiceStatus.Stopping);

        try
        {
            // Try graceful stop first
            var httpdPath = await _versionManager.GetBinaryPathAsync(ServiceType.Apache, ct);
            var configPath = GetConfigPath();

            var (_, stderr, exitCode) = await _processManager.RunCommandAsync(
                httpdPath, $"-f \"{configPath}\" -k stop", null, ct);

            // Wait for graceful shutdown (max 3 seconds)
            for (int i = 0; i < 15; i++)
            {
                if (!await _processManager.IsProcessRunningAsync(_processId.Value, ct))
                    break;
                await Task.Delay(200, ct);
            }

            // Force kill if still running
            if (await _processManager.IsProcessRunningAsync(_processId.Value, ct))
            {
                await _processManager.KillProcessAsync(_processId.Value, ct);
            }
        }
        catch { /* best effort */ }
        finally
        {
            _processId = null;
            SetStatus(ServiceStatus.Stopped);
        }
    }

    public async Task RestartAsync(CancellationToken ct = default)
    {
        await StopAsync(ct);
        await StartAsync(ct);
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        if (Status != ServiceStatus.Running)
            return;

        var httpdPath = await _versionManager.GetBinaryPathAsync(ServiceType.Apache, ct);
        var configPath = GetConfigPath();

        // Regenerate config before reload
        var config = await _configManager.LoadAsync(ct);
        await GenerateConfigAsync(config, ct);

        await _processManager.RunCommandAsync(
            httpdPath, $"-f \"{configPath}\" -k graceful", null, ct);
    }

    public async Task<bool> ValidateConfigAsync(CancellationToken ct = default)
    {
        try
        {
            var httpdPath = await _versionManager.GetBinaryPathAsync(ServiceType.Apache, ct);
            var configPath = GetConfigPath();

            if (!_fileSystem.FileExists(configPath))
                return false;

            var (stdout, stderr, exitCode) = await _processManager.RunCommandAsync(
                httpdPath, $"-t -f \"{configPath}\"", null, ct);

            return exitCode == 0 || stderr.Contains("Syntax OK", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>httpd -t ciktisini dondurur; hata varsa kullaniciya gosterilecek metin.</summary>
    private async Task<string?> GetConfigValidationErrorAsync(CancellationToken ct = default)
    {
        try
        {
            var httpdPath = await _versionManager.GetBinaryPathAsync(ServiceType.Apache, ct);
            var configPath = GetConfigPath();
            if (!_fileSystem.FileExists(configPath)) return null;

            var (stdout, stderr, exitCode) = await _processManager.RunCommandAsync(
                httpdPath, $"-t -f \"{configPath}\"", null, ct);

            if (exitCode == 0) return null;
            var combined = $"{stderr}\n{stdout}".Trim();
            return string.IsNullOrWhiteSpace(combined) ? null : combined;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public async Task<ServiceStatus> GetStatusAsync(CancellationToken ct = default)
    {
        if (_processId.HasValue && await _processManager.IsProcessRunningAsync(_processId.Value, ct))
        {
            var config = await _configManager.LoadAsync(ct);
            if (!await _portManager.IsPortAvailableAsync(config.ApachePort, ct))
            {
                Status = ServiceStatus.Running;
                return ServiceStatus.Running;
            }
        }

        if (Status == ServiceStatus.Running)
            SetStatus(ServiceStatus.Stopped);

        return Status;
    }

    public async Task DetectRunningAsync(CancellationToken ct = default)
    {
        try
        {
            var config = await _configManager.LoadAsync(ct);
            if (await _portManager.IsPortAvailableAsync(config.ApachePort, ct))
                return; // Port is free, nothing running

            var conflict = await _portManager.GetPortConflictAsync(config.ApachePort, ct);
            if (conflict != null && string.Equals(conflict.ProcessName, "httpd", StringComparison.OrdinalIgnoreCase))
            {
                _processId = conflict.ProcessId;
                SetStatus(ServiceStatus.Running);
            }
        }
        catch { /* detection is best-effort */ }
    }

    public async Task RegenerateConfigAsync(CancellationToken ct = default)
    {
        var config = await _configManager.LoadAsync(ct);
        await GenerateConfigAsync(config, ct);
    }

    private async Task GenerateConfigAsync(AppConfiguration config, CancellationToken ct)
    {
        var apacheVersion = await _versionManager.GetActiveVersionAsync(ServiceType.Apache, ct);
        var phpVersion = await _versionManager.GetActiveVersionAsync(ServiceType.Php, ct);

        if (apacheVersion == null) throw new ServiceStartException("Apache", "No active Apache version.");

        var serverRoot = apacheVersion.InstallPath;
        var docRoot = _fileSystem.GetFullPath(Path.Combine(_basePath, config.DocumentRoot));
        var logDir = _fileSystem.GetFullPath(Path.Combine(_basePath, Defaults.LogDir, "apache"));
        var vhostsPath = _fileSystem.GetFullPath(Path.Combine(_basePath, Defaults.ApacheConfigDir, "httpd-vhosts.conf"));
        var configDir = Path.Combine(_basePath, Defaults.ApacheConfigDir);

        _fileSystem.CreateDirectory(docRoot);
        _fileSystem.CreateDirectory(logDir);
        _fileSystem.CreateDirectory(configDir);

        // Determine PHP module path
        var phpModulePath = "";
        var phpPath = "";
        if (phpVersion != null)
        {
            phpPath = phpVersion.InstallPath;
            // Find php*apache*.dll in the PHP directory
            var phpDlls = _fileSystem.GetFiles(phpVersion.InstallPath, "php*apache*.dll");
            if (phpDlls.Length > 0)
                phpModulePath = phpDlls[0];
            else
            {
                // Try common name
                var commonPath = Path.Combine(phpVersion.InstallPath, "php8apache2_4.dll");
                phpModulePath = commonPath;
            }
        }

        var sitesEnabledDir = _fileSystem.GetFullPath(Path.Combine(_basePath, Defaults.SitesEnabledDir));
        _fileSystem.CreateDirectory(sitesEnabledDir);

        var sslDir = _fileSystem.GetFullPath(Path.Combine(_basePath, Defaults.SslDir));

        var aliasDir = _fileSystem.GetFullPath(Path.Combine(_basePath, Defaults.ApacheAliasDir));
        _fileSystem.CreateDirectory(aliasDir);

        var httpdConf = _configGenerator.Generate(
            serverRoot, docRoot, config.ApachePort,
            phpModulePath, phpPath, logDir, vhostsPath,
            sitesEnabledDir,
            config.SslEnabled, config.ApacheSslPort, sslDir,
            aliasDir);

        await _fileSystem.AtomicWriteAsync(GetConfigPath(), httpdConf, ct);

        // Generate empty vhosts if not exists
        if (!_fileSystem.FileExists(vhostsPath))
        {
            await _fileSystem.WriteAllTextAsync(vhostsPath,
                "# Generated by ZaraGON - Virtual Hosts\n", ct);
        }
    }

    private static readonly string[] RuntimeDlls =
    [
        "vcruntime140.dll",
        "vcruntime140_1.dll",
        "msvcp140.dll",
        "msvcp140_1.dll",
        "msvcp140_2.dll"
    ];

    private static void CopyRuntimeDlls(string phpDir, string apacheBinDir)
    {
        foreach (var dll in RuntimeDlls)
        {
            var source = Path.Combine(phpDir, dll);
            var target = Path.Combine(apacheBinDir, dll);
            try
            {
                if (File.Exists(source))
                    File.Copy(source, target, overwrite: true);
            }
            catch { /* file might be locked if Apache was running, ignore */ }
        }
    }

    private async Task<string?> ReadLastErrorLogLineAsync(CancellationToken ct)
    {
        try
        {
            var errorLog = Path.Combine(_basePath, Defaults.LogDir, "apache", "error.log");
            if (!_fileSystem.FileExists(errorLog)) return null;

            var content = await _fileSystem.ReadAllTextAsync(errorLog, ct);
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // First pass: look for VCRUNTIME error (highest priority)
            for (int i = lines.Length - 1; i >= Math.Max(0, lines.Length - 10); i--)
            {
                var line = lines[i].Trim();
                if (line.Contains("VCRUNTIME", StringComparison.OrdinalIgnoreCase))
                    return "VC++ Runtime uyumsuz — PHP için güncel Visual C++ Redistributable gerekli. Dashboard > Sorunları Tara > Düzelt ile kurabilirsiniz.";
            }

            // Second pass: look for emerg/crit errors
            for (int i = lines.Length - 1; i >= Math.Max(0, lines.Length - 5); i--)
            {
                var line = lines[i].Trim();
                if (line.Contains(":emerg]") || line.Contains(":crit]") || line.Contains("AH00020"))
                    return line;
            }

            return lines.Length > 0 ? lines[^1].Trim() : null;
        }
        catch { return null; }
    }

    private string GetConfigPath() =>
        Path.Combine(_basePath, Defaults.ApacheConfigDir, "httpd.conf");

    private void SetStatus(ServiceStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(this, status);
    }
}
