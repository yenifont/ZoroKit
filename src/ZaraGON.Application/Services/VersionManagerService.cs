using System.Text.Json;
using System.Text.Json.Serialization;
using ZaraGON.Core.Enums;
using ZaraGON.Core.Exceptions;
using ZaraGON.Core.Interfaces.Infrastructure;
using ZaraGON.Core.Interfaces.Providers;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;

namespace ZaraGON.Application.Services;

public sealed class VersionManagerService : IVersionManager
{
    private readonly IEnumerable<IVersionProvider> _providers;
    private readonly IDownloadManager _downloadManager;
    private readonly IFileSystem _fileSystem;
    private readonly string _basePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public VersionManagerService(
        IEnumerable<IVersionProvider> providers,
        IDownloadManager downloadManager,
        IFileSystem fileSystem,
        string basePath)
    {
        _providers = providers;
        _downloadManager = downloadManager;
        _fileSystem = fileSystem;
        _basePath = basePath;
    }

    public async Task<IReadOnlyList<ServiceVersion>> GetAvailableVersionsAsync(ServiceType serviceType, CancellationToken ct = default)
    {
        var provider = _providers.FirstOrDefault(p => p.ServiceType == serviceType)
            ?? throw new VersionNotFoundException($"No provider for {serviceType}");

        return await provider.FetchAvailableVersionsAsync(ct);
    }

    public async Task<IReadOnlyList<VersionPointer>> GetInstalledVersionsAsync(ServiceType serviceType, CancellationToken ct = default)
    {
        var versions = await LoadVersionsFileAsync(ct);
        return versions.Where(v => v.ServiceType == serviceType).ToList();
    }

    public async Task<VersionPointer?> GetActiveVersionAsync(ServiceType serviceType, CancellationToken ct = default)
    {
        var versions = await LoadVersionsFileAsync(ct);
        return versions.FirstOrDefault(v => v.ServiceType == serviceType && v.IsActive);
    }

    public async Task InstallVersionAsync(ServiceVersion version, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        var serviceDir = GetServiceDir(version.ServiceType);
        var versionDir = Path.Combine(serviceDir, version.Version);

        _fileSystem.CreateDirectory(serviceDir);

        // Download
        var tempDir = Path.Combine(_basePath, "temp");
        _fileSystem.CreateDirectory(tempDir);

        var archivePath = await _downloadManager.DownloadFileAsync(version.DownloadUrl, tempDir, progress, ct);

        // Extract
        await _downloadManager.ExtractArchiveAsync(archivePath, versionDir, progress, ct);

        // Cleanup archive
        _fileSystem.DeleteFile(archivePath);

        // For Apache, the zip usually contains an Apache24 subfolder - flatten it
        if (version.ServiceType == ServiceType.Apache)
        {
            var apache24Dir = Path.Combine(versionDir, "Apache24");
            if (_fileSystem.DirectoryExists(apache24Dir))
                FlattenSubfolder(versionDir, apache24Dir);
        }

        // For MariaDB, the zip contains a mariadb-{version}-winx64/ subfolder - flatten it
        if (version.ServiceType == ServiceType.MariaDb)
        {
            var mariaDbSubDir = Path.Combine(versionDir, $"mariadb-{version.Version}-winx64");
            if (_fileSystem.DirectoryExists(mariaDbSubDir))
            {
                FlattenSubfolder(versionDir, mariaDbSubDir);
            }
        }

        // Register version
        var pointer = new VersionPointer
        {
            ServiceType = version.ServiceType,
            Version = version.Version,
            InstallPath = versionDir,
            VsVersion = version.VsVersion,
            InstalledAt = DateTime.UtcNow,
            IsActive = false
        };

        await SaveVersionPointerAsync(pointer, ct);
    }

    public async Task SetActiveVersionAsync(ServiceType serviceType, string version, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var versions = await LoadVersionsFileAsync(ct);

            var target = versions.FirstOrDefault(v => v.ServiceType == serviceType && v.Version == version)
                ?? throw new VersionNotFoundException(version);

            foreach (var v in versions.Where(v => v.ServiceType == serviceType))
            {
                v.IsActive = v.Version == version;
            }

            await SaveVersionsFileAsync(versions, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UninstallVersionAsync(ServiceType serviceType, string version, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var versions = await LoadVersionsFileAsync(ct);
            var target = versions.FirstOrDefault(v => v.ServiceType == serviceType && v.Version == version);

            if (target == null) return;

            if (target.IsActive)
                throw new ZaraGONException("Cannot uninstall the active version. Switch to another version first.");

            if (_fileSystem.DirectoryExists(target.InstallPath))
                _fileSystem.DeleteDirectory(target.InstallPath, true);

            versions.Remove(target);
            await SaveVersionsFileAsync(versions, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<string> GetBinaryPathAsync(ServiceType serviceType, CancellationToken ct = default)
    {
        var active = await GetActiveVersionAsync(serviceType, ct)
            ?? throw new VersionNotFoundException($"No active {serviceType} version");

        return serviceType switch
        {
            ServiceType.Apache => Path.Combine(active.InstallPath, "bin", "httpd.exe"),
            ServiceType.Php => Path.Combine(active.InstallPath, "php.exe"),
            ServiceType.MariaDb => Path.Combine(active.InstallPath, "bin", "mysqld.exe"),
            _ => throw new ZaraGONException($"Unsupported service type: {serviceType}")
        };
    }

    private void FlattenSubfolder(string targetDir, string subDir)
    {
        foreach (var dir in _fileSystem.GetDirectories(subDir))
        {
            var destDir = Path.Combine(targetDir, Path.GetFileName(dir));
            if (!_fileSystem.DirectoryExists(destDir))
                Directory.Move(dir, destDir);
        }
        foreach (var file in _fileSystem.GetFiles(subDir))
        {
            var destFile = Path.Combine(targetDir, Path.GetFileName(file));
            _fileSystem.MoveFile(file, destFile);
        }
        _fileSystem.DeleteDirectory(subDir, true);
    }

    private string GetServiceDir(ServiceType serviceType) =>
        Path.Combine(_basePath, "bin", serviceType.ToString().ToLowerInvariant());

    private string GetVersionsFilePath() =>
        Path.Combine(_basePath, "config", "versions.json");

    private async Task<List<VersionPointer>> LoadVersionsFileAsync(CancellationToken ct)
    {
        var path = GetVersionsFilePath();
        if (!_fileSystem.FileExists(path))
            return [];

        var json = await _fileSystem.ReadAllTextAsync(path, ct);
        var versions = JsonSerializer.Deserialize<List<VersionPointer>>(json, JsonOptions) ?? [];

        // Fix InstallPath: always recompute from current basePath
        // This handles the case where the app was moved to a different directory
        var needsSave = false;
        foreach (var v in versions)
        {
            var expectedPath = Path.GetFullPath(Path.Combine(
                _basePath, "bin", v.ServiceType.ToString().ToLowerInvariant(), v.Version));

            if (!string.Equals(v.InstallPath, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                v.InstallPath = expectedPath;
                needsSave = true;
            }
        }

        if (needsSave)
            await SaveVersionsFileAsync(versions, ct);

        return versions;
    }

    private async Task SaveVersionsFileAsync(List<VersionPointer> versions, CancellationToken ct)
    {
        var path = GetVersionsFilePath();
        var json = JsonSerializer.Serialize(versions, JsonOptions);
        await _fileSystem.AtomicWriteAsync(path, json, ct);
    }

    private async Task SaveVersionPointerAsync(VersionPointer pointer, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var versions = await LoadVersionsFileAsync(ct);

            // Remove existing entry for same version
            versions.RemoveAll(v => v.ServiceType == pointer.ServiceType && v.Version == pointer.Version);
            versions.Add(pointer);

            await SaveVersionsFileAsync(versions, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
