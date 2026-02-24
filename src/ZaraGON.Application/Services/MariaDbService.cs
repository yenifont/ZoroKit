using System.Security.AccessControl;
using System.Security.Principal;
using ZaraGON.Application.ConfigGeneration;
using ZaraGON.Core.Constants;
using ZaraGON.Core.Enums;
using ZaraGON.Core.Exceptions;
using ZaraGON.Core.Interfaces.Infrastructure;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;

namespace ZaraGON.Application.Services;

public sealed class MariaDbService : IServiceController
{
    private readonly IVersionManager _versionManager;
    private readonly IProcessManager _processManager;
    private readonly IPortManager _portManager;
    private readonly IConfigurationManager _configManager;
    private readonly IFileSystem _fileSystem;
    private readonly MariaDbConfigGenerator _configGenerator;
    private readonly string _basePath;
    private int? _processId;

    public ServiceType ServiceType => ServiceType.MariaDb;
    public ServiceStatus Status { get; private set; } = ServiceStatus.Stopped;
    public event EventHandler<ServiceStatus>? StatusChanged;

    public MariaDbService(
        IVersionManager versionManager,
        IProcessManager processManager,
        IPortManager portManager,
        IConfigurationManager configManager,
        IFileSystem fileSystem,
        MariaDbConfigGenerator configGenerator,
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
            if (!await _portManager.IsPortAvailableAsync(config.MySqlPort, ct))
            {
                var conflict = await _portManager.GetPortConflictAsync(config.MySqlPort, ct);
                throw new PortConflictException(config.MySqlPort, conflict?.ProcessId);
            }

            // Get mysqld binary path
            string mysqldPath;
            try
            {
                mysqldPath = await _versionManager.GetBinaryPathAsync(ServiceType.MariaDb, ct);
            }
            catch (VersionNotFoundException)
            {
                throw new ServiceStartException("MariaDB",
                    "No MariaDB version installed. Go to the MariaDB page, fetch available versions, and download one first.");
            }

            if (!_fileSystem.FileExists(mysqldPath))
                throw new ServiceStartException("MariaDB", $"mysqld.exe not found at: {mysqldPath}");

            // Initialize data directory if first run
            var dataDir = GetDataDir();
            if (!_fileSystem.DirectoryExists(dataDir) || IsDataDirEmpty(dataDir))
            {
                await InitializeDataDirAsync(mysqldPath, ct);
            }

            // Ensure data directory has write permissions for current user
            EnsureDataDirPermissions(dataDir);

            // Remove rogue my.ini from data dir (bootstrap leftover, conflicts with --defaults-file)
            var rogueIni = Path.Combine(dataDir, "my.ini");
            if (_fileSystem.FileExists(rogueIni))
            {
                try { _fileSystem.DeleteFile(rogueIni); } catch { /* best effort */ }
            }

            // Generate config
            await GenerateConfigAsync(config, ct);

            var configPath = GetConfigPath();
            var workingDir = Path.GetDirectoryName(mysqldPath);

            _processId = await _processManager.StartProcessAsync(
                mysqldPath, $"\"--defaults-file={configPath}\"", workingDir, ct);

            // Poll until port is bound (max 5 seconds)
            for (int i = 0; i < 25; i++)
            {
                await Task.Delay(200, ct);
                if (!await _portManager.IsPortAvailableAsync(config.MySqlPort, ct))
                {
                    SetStatus(ServiceStatus.Running);
                    return;
                }
                // Check if process died
                if (!await _processManager.IsProcessRunningAsync(_processId.Value, ct))
                    break;
            }

            _processId = null;

            // Read error log for meaningful error message
            var errorDetail = ReadLastErrorLogLine();
            throw new ServiceStartException("MariaDB",
                string.IsNullOrEmpty(errorDetail)
                    ? "Process exited immediately after start."
                    : $"Process exited immediately: {errorDetail}");
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
            // Try graceful shutdown via mysqladmin
            var active = await _versionManager.GetActiveVersionAsync(ServiceType.MariaDb, ct);
            if (active != null)
            {
                var mysqladminPath = Path.Combine(active.InstallPath, "bin", "mysqladmin.exe");
                var config = await _configManager.LoadAsync(ct);

                if (_fileSystem.FileExists(mysqladminPath))
                {
                    await _processManager.RunCommandAsync(
                        mysqladminPath, $"--port={config.MySqlPort} shutdown", null, ct);
                }
            }

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

    public Task ReloadAsync(CancellationToken ct = default) => RestartAsync(ct);

    public async Task RestartAsync(CancellationToken ct = default)
    {
        await StopAsync(ct);
        await StartAsync(ct);
    }

    public async Task<bool> ValidateConfigAsync(CancellationToken ct = default)
    {
        try
        {
            var mysqldPath = await _versionManager.GetBinaryPathAsync(ServiceType.MariaDb, ct);
            var configPath = GetConfigPath();

            return _fileSystem.FileExists(mysqldPath) && _fileSystem.FileExists(configPath);
        }
        catch
        {
            return false;
        }
    }

    public async Task<ServiceStatus> GetStatusAsync(CancellationToken ct = default)
    {
        if (_processId.HasValue && await _processManager.IsProcessRunningAsync(_processId.Value, ct))
        {
            var config = await _configManager.LoadAsync(ct);
            if (!await _portManager.IsPortAvailableAsync(config.MySqlPort, ct))
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
            if (await _portManager.IsPortAvailableAsync(config.MySqlPort, ct))
                return; // Port is free, nothing running

            var conflict = await _portManager.GetPortConflictAsync(config.MySqlPort, ct);
            if (conflict != null && (
                string.Equals(conflict.ProcessName, "mysqld", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(conflict.ProcessName, "mariadbd", StringComparison.OrdinalIgnoreCase)))
            {
                _processId = conflict.ProcessId;
                SetStatus(ServiceStatus.Running);
            }
        }
        catch { /* detection is best-effort */ }
    }

    private async Task InitializeDataDirAsync(string mysqldPath, CancellationToken ct)
    {
        var installDir = Path.GetDirectoryName(Path.GetDirectoryName(mysqldPath))!;
        var installDbExe = Path.Combine(installDir, "bin", "mariadb-install-db.exe");
        var dataDir = GetDataDir();

        _fileSystem.CreateDirectory(dataDir);

        if (_fileSystem.FileExists(installDbExe))
        {
            await _processManager.RunCommandAsync(
                installDbExe, $"\"--datadir={dataDir}\"", installDir, ct);
        }
        else
        {
            // Bootstrap: create SQL file with system tables, pipe into mysqld --bootstrap
            var shareDir = Path.Combine(installDir, "share");
            var bootstrapSql = Path.Combine(Path.GetTempPath(), $"mariadb_bootstrap_{Guid.NewGuid():N}.sql");

            try
            {
                var sqlScripts = new[] { "mariadb_system_tables.sql", "mariadb_system_tables_data.sql",
                    "fill_help_tables.sql", "mariadb_performance_tables.sql" };

                using (var writer = new StreamWriter(bootstrapSql, false, System.Text.Encoding.UTF8))
                {
                    await writer.WriteLineAsync("CREATE DATABASE IF NOT EXISTS mysql;");
                    await writer.WriteLineAsync("USE mysql;");
                    foreach (var script in sqlScripts)
                    {
                        var scriptPath = Path.Combine(shareDir, script);
                        if (_fileSystem.FileExists(scriptPath))
                        {
                            var content = await _fileSystem.ReadAllTextAsync(scriptPath, ct);
                            await writer.WriteLineAsync(content);
                        }
                    }
                }

                await _processManager.RunCommandAsync(
                    "cmd.exe",
                    $"/c \"\"{mysqldPath}\" --no-defaults --bootstrap --datadir=\"{dataDir}\" --basedir=\"{installDir}\" --log-warnings=0 < \"{bootstrapSql}\"\"",
                    installDir, ct);
            }
            finally
            {
                try { File.Delete(bootstrapSql); } catch { }
            }
        }
    }

    public async Task RegenerateConfigAsync(CancellationToken ct = default)
    {
        try
        {
            var config = await _configManager.LoadAsync(ct);
            await GenerateConfigAsync(config, ct);
        }
        catch { /* version may not be installed yet */ }
    }

    private async Task GenerateConfigAsync(AppConfiguration config, CancellationToken ct)
    {
        var active = await _versionManager.GetActiveVersionAsync(ServiceType.MariaDb, ct)
            ?? throw new ServiceStartException("MariaDB", "No active MariaDB version.");

        var dataDir = GetDataDir();
        var configDir = Path.Combine(_basePath, Defaults.MariaDbConfigDir);
        var logDir = Path.Combine(_basePath, Defaults.LogDir);

        _fileSystem.CreateDirectory(dataDir);
        _fileSystem.CreateDirectory(configDir);
        _fileSystem.CreateDirectory(logDir);

        var mariaDbSettings = new MariaDbSettings
        {
            InnodbBufferPoolSize = config.MariaDbInnodbBufferPoolSize,
            MaxConnections = config.MariaDbMaxConnections,
            MaxAllowedPacket = config.MariaDbMaxAllowedPacket
        };

        var myIni = _configGenerator.Generate(active.InstallPath, dataDir, config.MySqlPort, logDir, mariaDbSettings);
        await _fileSystem.AtomicWriteAsync(GetConfigPath(), myIni, ct);
    }

    private string GetConfigPath() =>
        Path.Combine(_basePath, Defaults.MariaDbConfigDir, "my.ini");

    private string GetDataDir() =>
        _fileSystem.GetFullPath(Path.Combine(_basePath, Defaults.MariaDbDataDir));

    private bool IsDataDirEmpty(string path)
    {
        try
        {
            return _fileSystem.GetFiles(path).Length == 0
                && _fileSystem.GetDirectories(path).Length == 0;
        }
        catch { return true; }
    }

    private void EnsureDataDirPermissions(string dataDir)
    {
        try
        {
            var dirInfo = new DirectoryInfo(dataDir);
            var security = dirInfo.GetAccessControl();
            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser == null) return;

            security.AddAccessRule(new FileSystemAccessRule(
                currentUser,
                FileSystemRights.Modify | FileSystemRights.Synchronize,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            dirInfo.SetAccessControl(security);
        }
        catch
        {
            /* best effort — may already have correct permissions */
        }
    }

    private string? ReadLastErrorLogLine()
    {
        try
        {
            var logPath = Path.Combine(_basePath, Defaults.LogDir, "mariadb-error.log");
            if (!_fileSystem.FileExists(logPath)) return null;

            var lines = File.ReadAllLines(logPath);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (lines[i].Contains("[ERROR]", StringComparison.OrdinalIgnoreCase))
                    return lines[i];
            }
        }
        catch { /* best effort */ }
        return null;
    }

    private void SetStatus(ServiceStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(this, status);
    }
}
