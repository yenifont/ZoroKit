using System.IO.Compression;
using System.Net.Http;
using ZaraGON.Core.Constants;
using ZaraGON.Core.Enums;
using ZaraGON.Core.Interfaces.Infrastructure;
using ZaraGON.Core.Interfaces.Services;

namespace ZaraGON.Application.Services;

public sealed class OrchestratorService
{
    private readonly IServiceController _apacheController;
    private readonly MariaDbService _mariaDbController;
    private readonly IVersionManager _versionManager;
    private readonly IConfigurationManager _configManager;
    private readonly ILogWatcher _logWatcher;
    private readonly IHealthChecker _healthChecker;
    private readonly IFileSystem _fileSystem;
    private readonly PhpService _phpService;
    private readonly IAutoVirtualHostManager _autoVHostManager;
    private readonly HttpClient _httpClient;
    private readonly string _basePath;

    public OrchestratorService(
        IServiceController apacheController,
        MariaDbService mariaDbController,
        IVersionManager versionManager,
        IConfigurationManager configManager,
        ILogWatcher logWatcher,
        IHealthChecker healthChecker,
        IFileSystem fileSystem,
        PhpService phpService,
        IAutoVirtualHostManager autoVHostManager,
        HttpClient httpClient,
        string basePath)
    {
        _apacheController = apacheController;
        _mariaDbController = mariaDbController;
        _versionManager = versionManager;
        _configManager = configManager;
        _logWatcher = logWatcher;
        _healthChecker = healthChecker;
        _fileSystem = fileSystem;
        _phpService = phpService;
        _autoVHostManager = autoVHostManager;
        _httpClient = httpClient;
        _basePath = basePath;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        // Create directory structure
        var dirs = new[]
        {
            Path.Combine(_basePath, "bin", "apache"),
            Path.Combine(_basePath, "bin", "php"),
            Path.Combine(_basePath, "bin", "mariadb"),
            Path.Combine(_basePath, "config", "apache"),
            Path.Combine(_basePath, "config", "php"),
            Path.Combine(_basePath, "config", "mariadb"),
            Path.Combine(_basePath, "config", "apache", "alias"),
            Path.Combine(_basePath, "www"),
            Path.Combine(_basePath, "apps"),
            Path.Combine(_basePath, "logs", "apache"),
            Path.Combine(_basePath, "temp")
        };

        foreach (var dir in dirs)
            _fileSystem.CreateDirectory(dir);

        // Create default index.php
        var indexPath = Path.Combine(_basePath, "www", "index.php");
        if (!_fileSystem.FileExists(indexPath))
            await _fileSystem.WriteAllTextAsync(indexPath, Defaults.DefaultIndexPhp, ct);

        // Load/create config
        await _configManager.LoadAsync(ct);

        // Create SSL and sites-enabled dirs
        _fileSystem.CreateDirectory(Path.Combine(_basePath, Defaults.SitesEnabledDir));
        _fileSystem.CreateDirectory(Path.Combine(_basePath, Defaults.SslDir));

        // Sync all config files with current settings
        await SyncAllConfigsAsync(ct);

        // Detect already running services (from a previous session) — run in parallel
        await Task.WhenAll(
            _apacheController.DetectRunningAsync(ct),
            _mariaDbController.DetectRunningAsync(ct));

        // Start log watchers — run in parallel
        var apacheErrorLog = Path.Combine(_basePath, Defaults.LogDir, "apache", "error.log");
        var apacheAccessLog = Path.Combine(_basePath, Defaults.LogDir, "apache", "access.log");
        var appLog = Path.Combine(_basePath, Defaults.LogDir, "zoragon.log");
        await Task.WhenAll(
            _logWatcher.StartWatchingAsync(apacheErrorLog, "Apache Error", ct),
            _logWatcher.StartWatchingAsync(apacheAccessLog, "Apache Access", ct),
            _logWatcher.StartWatchingAsync(appLog, "ZaraGON", ct));

        // Start Auto Virtual Hosts watcher
        try { await _autoVHostManager.StartWatchingAsync(ct); }
        catch { /* non-critical */ }

        // Auto-install phpMyAdmin, Composer, and CA certificates if not present
        _ = Task.Run(() => EnsurePhpMyAdminAsync(ct), ct);
        _ = Task.Run(() => EnsureComposerAsync(ct), ct);
        _ = Task.Run(() => EnsureCaCertAsync(ct), ct);
    }

    private async Task EnsurePhpMyAdminAsync(CancellationToken ct)
    {
        var pmaPath = Path.GetFullPath(Path.Combine(_basePath, Defaults.AppsDir, "phpmyadmin"));
        if (_fileSystem.DirectoryExists(pmaPath) && _fileSystem.FileExists(Path.Combine(pmaPath, "index.php")))
        {
            // Ensure alias config exists even if phpMyAdmin is already installed
            await EnsurePhpMyAdminAliasAsync(pmaPath, ct);
            // Ensure config.inc.php has deprecation suppression for PHP 8.4+
            await EnsurePhpMyAdminConfigAsync(pmaPath, ct);
            return;
        }

        const string pmaVersion = "5.2.3";
        var downloadUrl = $"https://files.phpmyadmin.net/phpMyAdmin/{pmaVersion}/phpMyAdmin-{pmaVersion}-all-languages.zip";

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);

            var tempZip = Path.Combine(Path.GetTempPath(), $"phpmyadmin-{pmaVersion}.zip");
            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(downloadUrl, linked.Token);
                await File.WriteAllBytesAsync(tempZip, bytes, ct);

                var tempExtract = Path.Combine(Path.GetTempPath(), "phpmyadmin-extract");
                if (Directory.Exists(tempExtract))
                    Directory.Delete(tempExtract, true);

                ZipFile.ExtractToDirectory(tempZip, tempExtract);

                var extractedDir = Directory.GetDirectories(tempExtract)[0];

                _fileSystem.CreateDirectory(Path.GetDirectoryName(pmaPath)!);
                if (Directory.Exists(pmaPath))
                    Directory.Delete(pmaPath, true);

                Directory.Move(extractedDir, pmaPath);

                // Create config.inc.php
                var config = await _configManager.LoadAsync(ct);
                var configPhp = $"""
                    <?php
                    error_reporting(E_ALL & ~E_DEPRECATED);
                    $cfg['blowfish_secret'] = '{Guid.NewGuid():N}';
                    $i = 0;
                    $i++;
                    $cfg['Servers'][$i]['auth_type'] = 'cookie';
                    $cfg['Servers'][$i]['host'] = '127.0.0.1';
                    $cfg['Servers'][$i]['port'] = '{config.MySqlPort}';
                    $cfg['Servers'][$i]['compress'] = false;
                    $cfg['Servers'][$i]['AllowNoPassword'] = true;
                    $cfg['TempDir'] = './tmp/';
                    """;
                await File.WriteAllTextAsync(Path.Combine(pmaPath, "config.inc.php"), configPhp, ct);
                Directory.CreateDirectory(Path.Combine(pmaPath, "tmp"));

                // Create Apache alias config
                await EnsurePhpMyAdminAliasAsync(pmaPath, ct);

                Directory.Delete(tempExtract, true);
            }
            finally
            {
                if (File.Exists(tempZip))
                    File.Delete(tempZip);
            }
        }
        catch { /* non-critical - phpMyAdmin will be installed on demand */ }
    }

    private async Task EnsurePhpMyAdminConfigAsync(string pmaPath, CancellationToken ct)
    {
        var configPath = Path.Combine(pmaPath, "config.inc.php");
        if (!_fileSystem.FileExists(configPath))
            return;

        var content = await File.ReadAllTextAsync(configPath, ct);
        var modified = false;

        if (!content.Contains("error_reporting"))
        {
            content = content.Replace("<?php", "<?php\nerror_reporting(E_ALL & ~E_DEPRECATED);");
            modified = true;
        }

        // Sync phpMyAdmin port with current config
        var config = await _configManager.LoadAsync(ct);
        var portPattern = new System.Text.RegularExpressions.Regex(@"\['port'\]\s*=\s*'(\d+)'");
        var match = portPattern.Match(content);
        if (match.Success && match.Groups[1].Value != config.MySqlPort.ToString())
        {
            content = portPattern.Replace(content, $"['port'] = '{config.MySqlPort}'");
            modified = true;
        }

        if (modified)
            await File.WriteAllTextAsync(configPath, content, ct);
    }

    private async Task EnsurePhpMyAdminAliasAsync(string pmaPath, CancellationToken ct)
    {
        var aliasDir = Path.GetFullPath(Path.Combine(_basePath, Defaults.ApacheAliasDir));
        _fileSystem.CreateDirectory(aliasDir);

        var aliasConfPath = Path.Combine(aliasDir, "phpmyadmin.conf");
        var pmaPathForward = pmaPath.Replace('\\', '/');

        var aliasConf = $"""
            Alias /phpmyadmin "{pmaPathForward}/"
            <Directory "{pmaPathForward}/">
                Options Indexes FollowSymLinks
                AllowOverride All
                Require all granted
            </Directory>
            """;

        await _fileSystem.AtomicWriteAsync(aliasConfPath, aliasConf, ct);
    }

    private async Task EnsureComposerAsync(CancellationToken ct)
    {
        var composerDir = Path.GetFullPath(Path.Combine(_basePath, Defaults.ComposerDir));
        var pharPath = Path.Combine(composerDir, "composer.phar");

        try
        {
            _fileSystem.CreateDirectory(composerDir);

            // Download composer.phar if missing
            if (!_fileSystem.FileExists(pharPath))
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);

                var bytes = await _httpClient.GetByteArrayAsync("https://getcomposer.org/download/latest-stable/composer.phar", linked.Token);
                await File.WriteAllBytesAsync(pharPath, bytes, ct);
            }

            // Always regenerate composer.bat with current PHP path
            await RegenerateComposerBatAsync(ct);
        }
        catch { /* non-critical - Composer can be installed manually */ }
    }

    /// <summary>
    /// Regenerates composer.bat with the active PHP binary's absolute path.
    /// Called on startup and after PHP version switches.
    /// </summary>
    public async Task RegenerateComposerBatAsync(CancellationToken ct = default)
    {
        var composerDir = Path.GetFullPath(Path.Combine(_basePath, Defaults.ComposerDir));
        var batPath = Path.Combine(composerDir, "composer.bat");

        var config = await _configManager.LoadAsync(ct);
        var phpExe = "php";

        if (!string.IsNullOrEmpty(config.ActivePhpVersion))
        {
            var phpPath = Path.GetFullPath(Path.Combine(_basePath, Defaults.BinDir, "php", config.ActivePhpVersion, "php.exe"));
            if (File.Exists(phpPath))
                phpExe = $"\"{phpPath}\"";
        }

        var batContent = $"""
            @echo off
            {phpExe} "%~dp0composer.phar" %*
            """;

        _fileSystem.CreateDirectory(composerDir);
        await File.WriteAllTextAsync(batPath, batContent, ct);
    }

    private async Task EnsureCaCertAsync(CancellationToken ct)
    {
        var caCertPath = GetCaCertPath();

        if (_fileSystem.FileExists(caCertPath))
            return;

        try
        {
            _fileSystem.CreateDirectory(Path.GetDirectoryName(caCertPath)!);

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);

            var bytes = await _httpClient.GetByteArrayAsync("https://curl.se/ca/cacert.pem", linked.Token);
            await File.WriteAllBytesAsync(caCertPath, bytes, ct);
        }
        catch { /* non-critical - user can download manually */ }
    }

    /// <summary>
    /// Returns the absolute path to the CA certificate bundle.
    /// </summary>
    public string GetCaCertPath() =>
        Path.GetFullPath(Path.Combine(_basePath, Defaults.SslDir, "cacert.pem"));

    /// <summary>
    /// Regenerates all config files (httpd.conf, my.ini, php.ini, phpMyAdmin) to match current settings.
    /// Called on startup and after any settings change.
    /// </summary>
    public async Task SyncAllConfigsAsync(CancellationToken ct = default)
    {
        var config = await _configManager.LoadAsync(ct);

        // Regenerate Apache config
        try
        {
            if (!string.IsNullOrEmpty(config.ActiveApacheVersion))
                await _apacheController.RegenerateConfigAsync(ct);
        }
        catch { /* version may not be installed */ }

        // Regenerate MariaDB config
        try
        {
            if (!string.IsNullOrEmpty(config.ActiveMariaDbVersion))
                await _mariaDbController.RegenerateConfigAsync(ct);
        }
        catch { /* version may not be installed */ }

        // Regenerate PHP ini
        try
        {
            if (!string.IsNullOrEmpty(config.ActivePhpVersion) && config.ActivePhpVersion != "None")
                await _phpService.GeneratePhpIniAsync(config.ActivePhpVersion, ct: ct);
        }
        catch { /* version may not be installed */ }

        // Sync phpMyAdmin config
        await SyncPhpMyAdminPortAsync(ct);
    }

    public async Task SyncPhpMyAdminPortAsync(CancellationToken ct = default)
    {
        var pmaPath = Path.GetFullPath(Path.Combine(_basePath, Defaults.AppsDir, "phpmyadmin"));
        if (_fileSystem.DirectoryExists(pmaPath))
            await EnsurePhpMyAdminConfigAsync(pmaPath, ct);
    }

    public bool IsFirstRun()
    {
        return !_fileSystem.DirectoryExists(Path.Combine(_basePath, "bin"));
    }

    public async Task StartAllAsync(CancellationToken ct = default)
    {
        var mariaDbVersions = await _versionManager.GetInstalledVersionsAsync(ServiceType.MariaDb, ct);

        var apacheTask = _apacheController.StartAsync(ct);
        var mariaTask = mariaDbVersions.Count > 0
            ? _mariaDbController.StartAsync(ct)
            : Task.CompletedTask;

        await Task.WhenAll(apacheTask, mariaTask);
    }

    public async Task StopAllAsync(CancellationToken ct = default)
    {
        await Task.WhenAll(
            _mariaDbController.StopAsync(ct),
            _apacheController.StopAsync(ct));
    }

    public async Task SwitchPhpVersionAsync(string version, CancellationToken ct = default)
    {
        var config = await _configManager.LoadAsync(ct);

        await _versionManager.SetActiveVersionAsync(ServiceType.Php, version, ct);
        config.ActivePhpVersion = version;
        await _configManager.SaveAsync(config, ct);

        // Regenerate PHP ini
        await _phpService.GeneratePhpIniAsync(version, ct: ct);

        // Update composer.bat with new PHP path
        try { await RegenerateComposerBatAsync(ct); } catch { }

        // Restart Apache if running
        var status = await _apacheController.GetStatusAsync(ct);
        if (status == ServiceStatus.Running)
            await _apacheController.RestartAsync(ct);
    }

    public async Task ShutdownAsync()
    {
        await _mariaDbController.StopAsync();
        await _apacheController.StopAsync();
        await _logWatcher.StopAllAsync();
    }
}
