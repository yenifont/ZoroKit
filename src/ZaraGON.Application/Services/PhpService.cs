using ZaraGON.Application.ConfigGeneration;
using ZaraGON.Core.Constants;
using ZaraGON.Core.Interfaces.Infrastructure;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;

namespace ZaraGON.Application.Services;

public sealed class PhpService : IPhpExtensionManager
{
    private readonly IVersionManager _versionManager;
    private readonly IFileSystem _fileSystem;
    private readonly IConfigurationManager _configManager;
    private readonly PhpIniGenerator _phpIniGenerator;
    private readonly string _basePath;

    public PhpService(
        IVersionManager versionManager,
        IFileSystem fileSystem,
        IConfigurationManager configManager,
        PhpIniGenerator phpIniGenerator,
        string basePath)
    {
        _versionManager = versionManager;
        _fileSystem = fileSystem;
        _configManager = configManager;
        _phpIniGenerator = phpIniGenerator;
        _basePath = basePath;
    }

    public async Task<IReadOnlyList<PhpExtension>> GetExtensionsAsync(string phpVersion, CancellationToken ct = default)
    {
        var installed = await _versionManager.GetInstalledVersionsAsync(Core.Enums.ServiceType.Php, ct);
        var versionPointer = installed.FirstOrDefault(v => v.Version == phpVersion);
        if (versionPointer == null) return [];

        var extDir = Path.Combine(versionPointer.InstallPath, "ext");
        if (!_fileSystem.DirectoryExists(extDir))
            return [];

        // Get all DLLs in ext directory
        var dlls = _fileSystem.GetFiles(extDir, "php_*.dll");
        var enabledExtensions = await GetEnabledExtensionsFromIniAsync(ct);
        var extensions = new List<PhpExtension>();

        foreach (var dll in dlls)
        {
            var dllName = Path.GetFileName(dll);
            var extName = dllName.Replace("php_", "").Replace(".dll", "");

            extensions.Add(new PhpExtension
            {
                Name = extName,
                DllFileName = dllName,
                IsEnabled = enabledExtensions.Contains(extName),
                DllExists = true,
                IsBuiltIn = false
            });
        }

        return extensions.OrderBy(e => e.Name).ToList();
    }

    public async Task EnableExtensionAsync(string phpVersion, string extensionName, CancellationToken ct = default)
    {
        var enabledExts = await GetEnabledExtensionsFromIniAsync(ct);
        enabledExts.Add(extensionName);
        await RegeneratePhpIniAsync(phpVersion, enabledExts, ct);
    }

    public async Task DisableExtensionAsync(string phpVersion, string extensionName, CancellationToken ct = default)
    {
        var enabledExts = await GetEnabledExtensionsFromIniAsync(ct);
        enabledExts.Remove(extensionName);
        await RegeneratePhpIniAsync(phpVersion, enabledExts, ct);
    }

    public async Task SetExtensionsAsync(string phpVersion, IEnumerable<string> extensionNames, CancellationToken ct = default)
    {
        await RegeneratePhpIniAsync(phpVersion, extensionNames.ToHashSet(), ct);
    }

    public async Task GeneratePhpIniAsync(string phpVersion, IEnumerable<string>? extensions = null, CancellationToken ct = default)
    {
        var installed = await _versionManager.GetInstalledVersionsAsync(Core.Enums.ServiceType.Php, ct);
        var versionPointer = installed.FirstOrDefault(v => v.Version == phpVersion);
        if (versionPointer == null) return;

        var extDir = Path.Combine(versionPointer.InstallPath, "ext");
        var exts = extensions ?? Defaults.DefaultPhpExtensions;

        // Only include extensions that have DLLs
        var validExts = new List<string>();
        foreach (var ext in exts)
        {
            var dllPath = Path.Combine(extDir, $"php_{ext}.dll");
            if (_fileSystem.FileExists(dllPath))
                validExts.Add(ext);
        }

        // Build PhpSettings from persisted configuration
        var config = await _configManager.LoadAsync(ct);
        var phpSettings = new PhpSettings
        {
            MemoryLimit = config.PhpMemoryLimit,
            UploadMaxFilesize = config.PhpUploadMaxFilesize,
            PostMaxSize = config.PhpPostMaxSize,
            MaxExecutionTime = config.PhpMaxExecutionTime,
            MaxInputTime = config.PhpMaxInputTime,
            MaxFileUploads = config.PhpMaxFileUploads,
            MaxInputVars = config.PhpMaxInputVars,
            DisplayErrors = config.PhpDisplayErrors,
            ErrorReporting = config.PhpErrorReporting,
            DateTimezone = config.PhpDateTimezone
        };

        var caCertPath = Path.GetFullPath(Path.Combine(_basePath, Defaults.SslDir, "cacert.pem"));
        var ini = _phpIniGenerator.Generate(extDir, validExts, settings: phpSettings, caCertPath: caCertPath);
        var iniPath = GetPhpIniPath();

        _fileSystem.CreateDirectory(Path.GetDirectoryName(iniPath)!);
        await _fileSystem.AtomicWriteAsync(iniPath, ini, ct);

        // Also copy to PHP directory so PHP can find it
        var phpIniInDir = Path.Combine(versionPointer.InstallPath, "php.ini");
        await _fileSystem.WriteAllTextAsync(phpIniInDir, ini, ct);
    }

    private async Task RegeneratePhpIniAsync(string phpVersion, HashSet<string> enabledExtensions, CancellationToken ct)
    {
        await GeneratePhpIniAsync(phpVersion, enabledExtensions, ct);
    }

    private async Task<HashSet<string>> GetEnabledExtensionsFromIniAsync(CancellationToken ct)
    {
        var iniPath = GetPhpIniPath();
        if (!_fileSystem.FileExists(iniPath))
            return [.. Defaults.DefaultPhpExtensions];

        var content = await _fileSystem.ReadAllTextAsync(iniPath, ct);
        var enabled = new HashSet<string>();

        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("extension=", StringComparison.OrdinalIgnoreCase))
            {
                var extName = trimmed["extension=".Length..].Trim();
                enabled.Add(extName);
            }
        }

        return enabled;
    }

    private string GetPhpIniPath() =>
        Path.Combine(_basePath, Defaults.PhpConfigDir, "php.ini");
}
