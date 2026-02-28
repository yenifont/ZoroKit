using System.Text.RegularExpressions;
using ZoroKit.Application.ConfigGeneration;
using ZoroKit.Core.Constants;
using ZoroKit.Core.Interfaces.Infrastructure;
using ZoroKit.Core.Interfaces.Services;
using ZoroKit.Core.Models;

namespace ZoroKit.Application.Services;

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
        await UpdateExtensionInIniAsync(phpVersion, extensionName, enable: true, ct);
    }

    public async Task DisableExtensionAsync(string phpVersion, string extensionName, CancellationToken ct = default)
    {
        await UpdateExtensionInIniAsync(phpVersion, extensionName, enable: false, ct);
    }

    public async Task SetExtensionsAsync(string phpVersion, IEnumerable<string> extensionNames, CancellationToken ct = default)
    {
        await RegeneratePhpIniAsync(phpVersion, extensionNames.ToHashSet(), ct);
    }

    public async Task GeneratePhpIniAsync(string phpVersion, IEnumerable<string>? extensions = null, bool forceGenerate = false, CancellationToken ct = default)
    {
        // Skip if php.ini already exists (preserve user edits) unless forced
        var iniPath = GetPhpIniPath();
        if (!forceGenerate && _fileSystem.FileExists(iniPath))
            return;

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

        await WriteIniToBothLocationsAsync(ini, versionPointer.InstallPath, ct);
    }

    /// <summary>
    /// Updates specific PHP settings in an existing php.ini via regex find-replace.
    /// Preserves all other user edits (custom directives, comments, etc.).
    /// </summary>
    public async Task UpdatePhpSettingsInIniAsync(string phpVersion, PhpSettings settings, CancellationToken ct = default)
    {
        var iniPath = GetPhpIniPath();
        if (!_fileSystem.FileExists(iniPath))
        {
            // No ini yet — generate fresh
            await GeneratePhpIniAsync(phpVersion, forceGenerate: true, ct: ct);
            return;
        }

        var installed = await _versionManager.GetInstalledVersionsAsync(Core.Enums.ServiceType.Php, ct);
        var versionPointer = installed.FirstOrDefault(v => v.Version == phpVersion);
        if (versionPointer == null) return;

        var content = await _fileSystem.ReadAllTextAsync(iniPath, ct);
        var displayErrors = settings.DisplayErrors ? "On" : "Off";

        // Regex replacements for each managed setting
        content = ReplaceSetting(content, "memory_limit", settings.MemoryLimit);
        content = ReplaceSetting(content, "upload_max_filesize", settings.UploadMaxFilesize);
        content = ReplaceSetting(content, "post_max_size", settings.PostMaxSize);
        content = ReplaceSetting(content, "max_execution_time", settings.MaxExecutionTime.ToString());
        content = ReplaceSetting(content, "max_input_time", settings.MaxInputTime.ToString());
        content = ReplaceSetting(content, "max_file_uploads", settings.MaxFileUploads.ToString());
        content = ReplaceSetting(content, "max_input_vars", settings.MaxInputVars.ToString());
        content = ReplaceSetting(content, "display_errors", displayErrors);
        content = ReplaceSetting(content, "display_startup_errors", displayErrors);
        content = ReplaceSetting(content, "error_reporting", settings.ErrorReporting);
        content = ReplaceSetting(content, @"date\.timezone", settings.DateTimezone, "date.timezone");

        await WriteIniToBothLocationsAsync(content, versionPointer.InstallPath, ct);
    }

    /// <summary>
    /// Adds or removes a single extension line in php.ini without regenerating the whole file.
    /// </summary>
    public async Task UpdateExtensionInIniAsync(string phpVersion, string extensionName, bool enable, CancellationToken ct = default)
    {
        var iniPath = GetPhpIniPath();
        if (!_fileSystem.FileExists(iniPath))
        {
            // No ini yet — fall back to full generation with this extension toggled
            var exts = Defaults.DefaultPhpExtensions.ToHashSet();
            if (enable) exts.Add(extensionName); else exts.Remove(extensionName);
            await GeneratePhpIniAsync(phpVersion, exts, forceGenerate: true, ct: ct);
            return;
        }

        var installed = await _versionManager.GetInstalledVersionsAsync(Core.Enums.ServiceType.Php, ct);
        var versionPointer = installed.FirstOrDefault(v => v.Version == phpVersion);
        if (versionPointer == null) return;

        var content = await _fileSystem.ReadAllTextAsync(iniPath, ct);
        var lines = content.Split('\n').ToList();
        var extensionLine = $"extension={extensionName}";

        if (enable)
        {
            // Check if already present
            var alreadyExists = lines.Any(l =>
                l.Trim().Equals(extensionLine, StringComparison.OrdinalIgnoreCase));
            if (!alreadyExists)
            {
                // Find the last extension= line and insert after it
                var lastExtIdx = -1;
                for (var i = 0; i < lines.Count; i++)
                {
                    if (lines[i].TrimStart().StartsWith("extension=", StringComparison.OrdinalIgnoreCase))
                        lastExtIdx = i;
                }

                if (lastExtIdx >= 0)
                    lines.Insert(lastExtIdx + 1, extensionLine);
                else
                {
                    // No extension lines found — add after "; Extensions" comment or at end
                    var extCommentIdx = lines.FindIndex(l => l.Trim().Equals("; Extensions", StringComparison.OrdinalIgnoreCase));
                    if (extCommentIdx >= 0)
                        lines.Insert(extCommentIdx + 1, extensionLine);
                    else
                        lines.Add(extensionLine);
                }
            }
        }
        else
        {
            // Remove the extension line
            lines.RemoveAll(l =>
                l.Trim().Equals(extensionLine, StringComparison.OrdinalIgnoreCase));
        }

        var newContent = string.Join('\n', lines);
        await WriteIniToBothLocationsAsync(newContent, versionPointer.InstallPath, ct);
    }

    /// <summary>
    /// Updates extension_dir path and removes extensions whose DLLs don't exist in the new version.
    /// Called when switching PHP versions.
    /// </summary>
    public async Task UpdateExtensionDirInIniAsync(string phpVersion, CancellationToken ct = default)
    {
        var iniPath = GetPhpIniPath();
        if (!_fileSystem.FileExists(iniPath))
        {
            await GeneratePhpIniAsync(phpVersion, forceGenerate: true, ct: ct);
            return;
        }

        var installed = await _versionManager.GetInstalledVersionsAsync(Core.Enums.ServiceType.Php, ct);
        var versionPointer = installed.FirstOrDefault(v => v.Version == phpVersion);
        if (versionPointer == null) return;

        var newExtDir = Path.Combine(versionPointer.InstallPath, "ext").Replace('\\', '/');
        var content = await _fileSystem.ReadAllTextAsync(iniPath, ct);

        // Update extension_dir
        content = Regex.Replace(
            content,
            @"^(extension_dir\s*=\s*)""?[^""\r\n]*""?",
            $"""extension_dir = "{newExtDir}" """.TrimEnd(),
            RegexOptions.Multiline);

        // Remove extensions whose DLLs don't exist in the new PHP version
        var lines = content.Split('\n').ToList();
        var extDirNative = Path.Combine(versionPointer.InstallPath, "ext");
        lines.RemoveAll(l =>
        {
            var trimmed = l.Trim();
            if (!trimmed.StartsWith("extension=", StringComparison.OrdinalIgnoreCase))
                return false;
            var extName = trimmed["extension=".Length..].Trim();
            var dllPath = Path.Combine(extDirNative, $"php_{extName}.dll");
            return !_fileSystem.FileExists(dllPath);
        });

        var newContent = string.Join('\n', lines);
        await WriteIniToBothLocationsAsync(newContent, versionPointer.InstallPath, ct);
    }

    private async Task RegeneratePhpIniAsync(string phpVersion, HashSet<string> enabledExtensions, CancellationToken ct)
    {
        await GeneratePhpIniAsync(phpVersion, enabledExtensions, forceGenerate: true, ct: ct);
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

    /// <summary>
    /// Writes php.ini content to both config/php/php.ini and bin/php/{version}/php.ini.
    /// </summary>
    private async Task WriteIniToBothLocationsAsync(string content, string phpInstallPath, CancellationToken ct)
    {
        var iniPath = GetPhpIniPath();
        _fileSystem.CreateDirectory(Path.GetDirectoryName(iniPath)!);
        await _fileSystem.AtomicWriteAsync(iniPath, content, ct);

        // Also copy to PHP directory so PHP can find it
        var phpIniInDir = Path.Combine(phpInstallPath, "php.ini");
        await _fileSystem.WriteAllTextAsync(phpIniInDir, content, ct);
    }

    /// <summary>
    /// Replaces a single INI directive value using regex.
    /// </summary>
    private static string ReplaceSetting(string content, string regexKey, string newValue, string? literalKey = null)
    {
        var key = literalKey ?? regexKey;
        var pattern = $@"^({Regex.Escape(regexKey)}\s*=\s*).*$";

        if (Regex.IsMatch(content, pattern, RegexOptions.Multiline))
        {
            return Regex.Replace(content, pattern, $"{key} = {newValue}", RegexOptions.Multiline);
        }

        // Setting not found in file — append it
        return content.TrimEnd() + $"\n{key} = {newValue}\n";
    }

    private string GetPhpIniPath() =>
        Path.Combine(_basePath, Defaults.PhpConfigDir, "php.ini");
}
