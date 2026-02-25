using System.IO;
using System.Text.RegularExpressions;
using ZaraGON.Core.Constants;
using ZaraGON.Core.Interfaces.Infrastructure;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;

namespace ZaraGON.Application.Services;

public sealed class AutoVirtualHostService : IAutoVirtualHostManager, IDisposable
{
    private readonly IFileSystem _fileSystem;
    private readonly IHostsFileManager _hostsManager;
    private readonly IConfigurationManager _configManager;
    private readonly string _basePath;
    private FileSystemWatcher? _watcher;
    private readonly List<string> _detectedSites = [];
    private readonly object _lock = new();
    private bool _isApplying;

    private static readonly Regex ValidHostnameRegex = new(
        @"^[a-zA-Z0-9]([a-zA-Z0-9\-]*[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9\-]*[a-zA-Z0-9])?)*$",
        RegexOptions.Compiled);

    public event EventHandler<string>? SiteAdded;
    public event EventHandler<string>? SiteRemoved;

    public AutoVirtualHostService(
        IFileSystem fileSystem,
        IHostsFileManager hostsManager,
        IConfigurationManager configManager,
        string basePath)
    {
        _fileSystem = fileSystem;
        _hostsManager = hostsManager;
        _configManager = configManager;
        _basePath = basePath;
    }

    public IReadOnlyList<string> GetDetectedSites()
    {
        lock (_lock)
            return _detectedSites.ToList();
    }

    public async Task ScanAndApplyAsync(CancellationToken ct = default)
    {
        if (_isApplying) return;
        _isApplying = true;

        try
        {
            var config = await _configManager.LoadAsync(ct);
            if (!config.AutoVirtualHosts)
            {
                // Clean up stale auto.* files when feature is disabled
                var sitesPath = Path.Combine(_basePath, Defaults.SitesEnabledDir);
                if (Directory.Exists(sitesPath))
                {
                    foreach (var file in Directory.GetFiles(sitesPath, "auto.*.conf"))
                    {
                        try { _fileSystem.DeleteFile(file); } catch { }
                    }
                }
                return;
            }

            var wwwPath = Path.Combine(_basePath, config.DocumentRoot);
            _fileSystem.CreateDirectory(wwwPath);

            var sitesDir = Path.Combine(_basePath, Defaults.SitesEnabledDir);
            _fileSystem.CreateDirectory(sitesDir);

            // Scan for subdirectories (skip names with spaces/invalid hostname chars)
            var dirs = Directory.Exists(wwwPath)
                ? Directory.GetDirectories(wwwPath)
                    .Select(Path.GetFileName)
                    .Where(n => !string.IsNullOrEmpty(n) && !n!.StartsWith('.')
                                && ValidHostnameRegex.IsMatch(n))
                    .Select(n => n!)
                    .ToList()
                : new List<string>();

            var tld = config.VirtualHostTld;
            var port = config.ApachePort;
            var previousSites = new List<string>();

            lock (_lock)
            {
                previousSites.AddRange(_detectedSites);
                _detectedSites.Clear();
                _detectedSites.AddRange(dirs);
            }

            // Generate vhost conf for each site
            foreach (var siteName in dirs)
            {
                var hostname = $"{siteName}{tld}";
                var docRoot = Path.Combine(wwwPath, siteName).Replace('\\', '/');

                var vhostConf = GenerateVHostConf(hostname, docRoot, port);
                var confPath = Path.Combine(sitesDir, $"auto.{hostname}.conf");
                await _fileSystem.WriteAllTextAsync(confPath, vhostConf, ct);

                // SSL vhost if enabled
                if (config.SslEnabled)
                {
                    var sslDir = Path.Combine(_basePath, Defaults.SslDir);
                    var certPath = Path.Combine(sslDir, $"{hostname}.crt").Replace('\\', '/');
                    var keyPath = Path.Combine(sslDir, $"{hostname}.key").Replace('\\', '/');

                    if (File.Exists(certPath.Replace('/', '\\')))
                    {
                        var sslConf = GenerateSslVHostConf(hostname, docRoot, config.ApacheSslPort, certPath, keyPath);
                        var sslConfPath = Path.Combine(sitesDir, $"auto.{hostname}-ssl.conf");
                        await _fileSystem.WriteAllTextAsync(sslConfPath, sslConf, ct);
                    }
                }

                if (!previousSites.Contains(siteName))
                    SiteAdded?.Invoke(this, siteName);
            }

            // Remove stale conf files
            if (Directory.Exists(sitesDir))
            {
                foreach (var file in Directory.GetFiles(sitesDir, "auto.*.conf"))
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var siteDomain = fileName.StartsWith("auto.") ? fileName[5..] : fileName;
                    siteDomain = siteDomain.Replace("-ssl", "");

                    var siteName = siteDomain.EndsWith(tld)
                        ? siteDomain[..^tld.Length]
                        : siteDomain;

                    if (!dirs.Contains(siteName))
                    {
                        _fileSystem.DeleteFile(file);
                    }
                }
            }

            // Notify removed sites
            foreach (var prev in previousSites)
            {
                if (!dirs.Contains(prev))
                    SiteRemoved?.Invoke(this, prev);
            }

            // Hosts dosyasina www alt klasorleri yazilmiyor; varsayilan sadece zaragon.app (EnsureDefaultZaragonHostAsync).
            // Kullanici Hosts File sayfasindan isterse ekler.
        }
        finally
        {
            _isApplying = false;
        }
    }

    public async Task EnsureVHostForHostnameAsync(string hostname, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(hostname)) return;
        if (!ValidHostnameRegex.IsMatch(hostname))
            throw new ArgumentException($"Geçersiz hostname: '{hostname}'. Hostname yalnızca harf, rakam, tire ve nokta içerebilir.");

        var config = await _configManager.LoadAsync(ct);
        var wwwPath = Path.Combine(_basePath, config.DocumentRoot);
        _fileSystem.CreateDirectory(wwwPath);

        var tld = config.VirtualHostTld;
        var siteName = hostname.EndsWith(tld, StringComparison.OrdinalIgnoreCase)
            ? hostname[..^tld.Length]
            : hostname.Split('.')[0];
        if (string.IsNullOrEmpty(siteName)) siteName = hostname.Replace(".", "_");

        var siteDir = Path.Combine(wwwPath, siteName);
        if (!_fileSystem.DirectoryExists(siteDir))
            _fileSystem.CreateDirectory(siteDir);

        if (config.AutoVirtualHosts)
        {
            await ScanAndApplyAsync(ct);
            return;
        }

        var sitesDir = Path.Combine(_basePath, Defaults.SitesEnabledDir);
        _fileSystem.CreateDirectory(sitesDir);
        var docRoot = siteDir.Replace('\\', '/');
        var vhostConf = GenerateVHostConf(hostname, docRoot, config.ApachePort);
        var confPath = Path.Combine(sitesDir, $"manual.{hostname}.conf");
        await _fileSystem.WriteAllTextAsync(confPath, vhostConf, ct);
    }

    public async Task EnsureDefaultZaragonHostAsync(CancellationToken ct = default)
    {
        var hostname = Defaults.DefaultZaragonHostname;
        var config = await _configManager.LoadAsync(ct);
        var existing = await _hostsManager.GetManagedEntriesAsync(ct);
        if (existing.All(e => !string.Equals(e.Hostname, hostname, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                await _hostsManager.AddEntryAsync(new HostEntry { IpAddress = "127.0.0.1", Hostname = hostname }, ct);
            }
            catch
            {
                /* hosts yazma yükseltme gerektirebilir; vhost yine eklenir */
            }
        }

        var wwwPath = Path.Combine(_basePath, config.DocumentRoot);
        _fileSystem.CreateDirectory(wwwPath);
        var sitesDir = Path.Combine(_basePath, Defaults.SitesEnabledDir);
        _fileSystem.CreateDirectory(sitesDir);
        var docRoot = wwwPath.Replace('\\', '/');

        // 000- prefix ensures this is always the first VirtualHost (default catch-all)
        // ServerAlias localhost ensures http://localhost/* is explicitly matched
        var vhostConf = GenerateVHostConf(hostname, docRoot, config.ApachePort, "localhost");
        var confPath = Path.Combine(sitesDir, $"000-default.{hostname}.conf");
        await _fileSystem.WriteAllTextAsync(confPath, vhostConf, ct);

        // Clean up old naming convention
        var oldConfPath = Path.Combine(sitesDir, $"default.{hostname}.conf");
        if (_fileSystem.FileExists(oldConfPath))
        {
            try { _fileSystem.DeleteFile(oldConfPath); } catch { }
        }
    }

    public async Task StartWatchingAsync(CancellationToken ct = default)
    {
        var config = await _configManager.LoadAsync(ct);
        var wwwPath = Path.GetFullPath(Path.Combine(_basePath, config.DocumentRoot));
        _fileSystem.CreateDirectory(wwwPath);

        await ScanAndApplyAsync(ct);

        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(wwwPath)
        {
            NotifyFilter = NotifyFilters.DirectoryName,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnDirectoryChanged;
        _watcher.Deleted += OnDirectoryChanged;
        _watcher.Renamed += OnDirectoryRenamed;
    }

    public Task StopWatchingAsync()
    {
        _watcher?.Dispose();
        _watcher = null;
        return Task.CompletedTask;
    }

    private async void OnDirectoryChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce slightly
        await Task.Delay(500);
        try { await ScanAndApplyAsync(); }
        catch { /* best effort */ }
    }

    private async void OnDirectoryRenamed(object sender, RenamedEventArgs e)
    {
        await Task.Delay(500);
        try { await ScanAndApplyAsync(); }
        catch { /* best effort */ }
    }

    private static string GenerateVHostConf(string hostname, string docRoot, int port, string? extraAliases = null)
    {
        var aliasLine = string.IsNullOrEmpty(extraAliases)
            ? $"ServerAlias *.{hostname}"
            : $"ServerAlias *.{hostname} {extraAliases}";

        return $"""
            <VirtualHost *:{port}>
                DocumentRoot "{docRoot}"
                ServerName {hostname}
                {aliasLine}
                <Directory "{docRoot}">
                    Options Indexes FollowSymLinks
                    AllowOverride All
                    Require all granted
                </Directory>
            </VirtualHost>
            """;
    }

    private static string GenerateSslVHostConf(string hostname, string docRoot, int sslPort, string certPath, string keyPath)
    {
        return $"""
            <VirtualHost *:{sslPort}>
                DocumentRoot "{docRoot}"
                ServerName {hostname}
                SSLEngine on
                SSLCertificateFile "{certPath}"
                SSLCertificateKeyFile "{keyPath}"
                <Directory "{docRoot}">
                    Options Indexes FollowSymLinks
                    AllowOverride All
                    Require all granted
                </Directory>
            </VirtualHost>
            """;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
