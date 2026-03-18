using System.IO;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ZoroKit.Application.Services;
using ZoroKit.Core.Constants;
using ZoroKit.Core.Enums;
using ZoroKit.Core.Interfaces.Services;
using ZoroKit.UI.Services;

namespace ZoroKit.UI.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigurationManager _configManager;
    private readonly OrchestratorService _orchestrator;
    private readonly ISslCertificateManager _sslManager;
    private readonly IServiceController _apacheController;
    private readonly DialogService _dialogService;
    private readonly ToastService _toastService;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly string _basePath;

    [ObservableProperty]
    private int _apachePort = 80;

    [ObservableProperty]
    private int _apacheSslPort = 443;

    [ObservableProperty]
    private int _mysqlPort = 3306;

    [ObservableProperty]
    private string _documentRoot = "www";

    [ObservableProperty]
    private bool _autoStartApache;

    [ObservableProperty]
    private bool _autoStartMariaDb;

    [ObservableProperty]
    private bool _autoVirtualHosts = true;

    [ObservableProperty]
    private string _virtualHostTld = ".test";

    [ObservableProperty]
    private bool _sslEnabled;

    [ObservableProperty]
    private bool _runOnWindowsStartup;

    [ObservableProperty]
    private bool _addToSystemPath;

    [ObservableProperty]
    private bool _hasSslCertificate;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public SettingsViewModel(
        IConfigurationManager configManager,
        OrchestratorService orchestrator,
        ISslCertificateManager sslManager,
        IServiceController apacheController,
        DialogService dialogService,
        ToastService toastService,
        DashboardViewModel dashboardViewModel,
        string basePath)
    {
        _configManager = configManager;
        _orchestrator = orchestrator;
        _sslManager = sslManager;
        _apacheController = apacheController;
        _dialogService = dialogService;
        _toastService = toastService;
        _dashboardViewModel = dashboardViewModel;
        _basePath = basePath;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();
            ApachePort = config.ApachePort;
            ApacheSslPort = config.ApacheSslPort;
            MysqlPort = config.MySqlPort;
            DocumentRoot = config.DocumentRoot;
            AutoStartApache = config.AutoStartApache;
            AutoStartMariaDb = config.AutoStartMariaDb;
            AutoVirtualHosts = config.AutoVirtualHosts;
            VirtualHostTld = config.VirtualHostTld;
            SslEnabled = config.SslEnabled;
            RunOnWindowsStartup = config.RunOnWindowsStartup;
            AddToSystemPath = config.AddToSystemPath;
            HasSslCertificate = _sslManager.HasCaCertificate();
        }
        catch { /* defaults */ }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();
            var oldStartup = config.RunOnWindowsStartup;
            var oldPath = config.AddToSystemPath;
            var oldSslEnabled = config.SslEnabled;
            var oldApachePort = config.ApachePort;
            var oldSslPort = config.ApacheSslPort;

            // SSL etkinleştiriliyorsa sertifika kontrolü
            if (SslEnabled && !oldSslEnabled && !HasSslCertificate)
            {
                try
                {
                    await _sslManager.EnsureCaCertificateAsync();
                    HasSslCertificate = true;
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError($"SSL sertifikası oluşturulamadı: {ex.Message}", "SSL Hatası");
                    return;
                }
            }

            config.ApachePort = ApachePort;
            config.ApacheSslPort = ApacheSslPort;
            config.MySqlPort = MysqlPort;
            config.DocumentRoot = DocumentRoot;
            config.AutoStartApache = AutoStartApache;
            config.AutoStartMariaDb = AutoStartMariaDb;
            config.AutoVirtualHosts = AutoVirtualHosts;
            config.VirtualHostTld = VirtualHostTld;
            config.SslEnabled = SslEnabled;
            config.RunOnWindowsStartup = RunOnWindowsStartup;
            config.AddToSystemPath = AddToSystemPath;
            await _configManager.SaveAsync(config);
            await _orchestrator.SyncAllConfigsAsync();

            // Dashboard'u güncelle (port değişiklikleri için)
            _dashboardViewModel.ApachePort = ApachePort;
            _dashboardViewModel.MysqlPort = MysqlPort;

            // SSL etkinse mevcut domain'ler için eksik SSL VHost ve sertifikaları oluştur
            // (her kaydetmede kontrol — önceki başarısız denemeleri telafi eder)
            if (SslEnabled)
            {
                await GenerateSslVHostsForExistingDomainsAsync(config);
            }
            // SSL kapatıldıysa SSL VHost dosyalarını temizle
            else if (oldSslEnabled)
            {
                CleanupSslVHostConfigs();
            }

            // Handle Windows Startup toggle
            if (oldStartup != RunOnWindowsStartup)
                ApplyWindowsStartup(RunOnWindowsStartup);

            // Handle System PATH toggle
            if (oldPath != AddToSystemPath)
                ApplySystemPath(config, AddToSystemPath);

            // Apache port veya SSL değiştiyse Apache'yi yeniden başlat
            bool apacheSettingsChanged = oldApachePort != ApachePort
                || oldSslPort != ApacheSslPort
                || oldSslEnabled != SslEnabled;

            if (apacheSettingsChanged)
            {
                var status = await _apacheController.GetStatusAsync();
                if (status == ServiceStatus.Running)
                {
                    try
                    {
                        await _apacheController.ReloadAsync();
                        StatusMessage = "Ayarlar kaydedildi — Apache yeniden başlatıldı";
                        _toastService.ShowSuccess("Ayarlar kaydedildi — Apache yeniden başlatıldı");
                    }
                    catch (Exception ex)
                    {
                        _toastService.ShowWarning($"Ayarlar kaydedildi ancak Apache yeniden başlatılamadı: {ex.Message}");
                    }
                    return;
                }
            }

            StatusMessage = "Ayarlar kaydedildi";
            _toastService.ShowSuccess("Ayarlar kaydedildi");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, "Kaydetme Başarısız");
        }
    }

    /// <summary>SSL etkinleştirildiğinde mevcut tüm VHost domain'leri için SSL sertifikası ve VHost oluşturur.</summary>
    private async Task GenerateSslVHostsForExistingDomainsAsync(Core.Models.AppConfiguration config)
    {
        var sitesDir = Path.Combine(_basePath, Defaults.SitesEnabledDir);
        if (!Directory.Exists(sitesDir)) return;

        foreach (var confFile in Directory.GetFiles(sitesDir, "*.conf"))
        {
            var fileName = Path.GetFileName(confFile);
            if (fileName.Contains("-ssl")) continue; // Mevcut SSL config'leri atla

            try
            {
                var content = await File.ReadAllTextAsync(confFile);
                var serverNameMatch = Regex.Match(content, @"ServerName\s+(\S+)");
                var docRootMatch = Regex.Match(content, @"DocumentRoot\s+""([^""]+)""");
                if (!serverNameMatch.Success || !docRootMatch.Success) continue;

                var hostname = serverNameMatch.Groups[1].Value;
                var docRoot = docRootMatch.Groups[1].Value;

                var (certPath, keyPath) = await _sslManager.EnsureDomainCertificateAsync(hostname);

                var sslConf = $"""
                    <VirtualHost *:{config.ApacheSslPort}>
                        DocumentRoot "{docRoot}"
                        ServerName {hostname}
                        SSLEngine on
                        SSLCertificateFile "{certPath.Replace('\\', '/')}"
                        SSLCertificateKeyFile "{keyPath.Replace('\\', '/')}"
                        <Directory "{docRoot}">
                            Options Indexes FollowSymLinks
                            AllowOverride All
                            Require all granted
                        </Directory>
                    </VirtualHost>
                    """;

                var baseName = Path.GetFileNameWithoutExtension(confFile);
                var sslConfPath = Path.Combine(sitesDir, $"{baseName}-ssl.conf");
                await File.WriteAllTextAsync(sslConfPath, sslConf);
            }
            catch { /* Sertifika üretilemezse o domain atlanır */ }
        }
    }

    /// <summary>SSL kapatıldığında tüm SSL VHost config dosyalarını temizler.</summary>
    private void CleanupSslVHostConfigs()
    {
        var sitesDir = Path.Combine(_basePath, Defaults.SitesEnabledDir);
        if (!Directory.Exists(sitesDir)) return;

        foreach (var sslConf in Directory.GetFiles(sitesDir, "*-ssl.conf"))
        {
            try { File.Delete(sslConf); } catch { }
        }
    }

    [RelayCommand]
    private async Task GenerateSslCertificateAsync()
    {
        try
        {
            await _sslManager.EnsureCaCertificateAsync();
            HasSslCertificate = true;
            _toastService.ShowSuccess("SSL sertifikası oluşturuldu");
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Sertifika oluşturulamadı: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task TrustSslCertificateAsync()
    {
        try
        {
            await _sslManager.TrustCaCertificateAsync();
            _toastService.ShowSuccess("Sertifika Trust Store'a eklendi");
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Trust Store'a eklenemedi: {ex.Message}");
        }
    }

    private static void ApplyWindowsStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue("ZoroKit", $"\"{exePath}\" --minimized");
            }
            else
            {
                key.DeleteValue("ZoroKit", false);
            }
        }
        catch { /* registry access may fail */ }
    }

    private void ApplySystemPath(Core.Models.AppConfiguration config, bool enable)
    {
        try
        {
            var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
            var dirs = new List<string>();

            if (!string.IsNullOrEmpty(config.ActivePhpVersion))
                dirs.Add(Path.GetFullPath(Path.Combine(_basePath, Defaults.BinDir, "php", config.ActivePhpVersion)));
            if (!string.IsNullOrEmpty(config.ActiveMariaDbVersion))
                dirs.Add(Path.GetFullPath(Path.Combine(_basePath, Defaults.BinDir, "mariadb", config.ActiveMariaDbVersion, "bin")));
            dirs.Add(Path.GetFullPath(Path.Combine(_basePath, Defaults.ComposerDir)));

            if (enable)
            {
                var parts = userPath.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
                foreach (var dir in dirs)
                {
                    if (!parts.Contains(dir, StringComparer.OrdinalIgnoreCase))
                        parts.Add(dir);
                }
                Environment.SetEnvironmentVariable("PATH", string.Join(";", parts), EnvironmentVariableTarget.User);
            }
            else
            {
                var parts = userPath.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Where(p => !dirs.Contains(p, StringComparer.OrdinalIgnoreCase))
                    .ToList();
                Environment.SetEnvironmentVariable("PATH", string.Join(";", parts), EnvironmentVariableTarget.User);
            }
        }
        catch { /* PATH modification may fail */ }
    }
}
