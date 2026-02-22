using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZaraGON.Application.Services;
using ZaraGON.Core.Constants;
using ZaraGON.Core.Enums;
using ZaraGON.Core.Exceptions;
using ZaraGON.Core.Interfaces.Infrastructure;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;
using ZaraGON.UI.Services;

namespace ZaraGON.UI.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly OrchestratorService _orchestrator;
    private readonly IServiceController _apacheController;
    private readonly MariaDbService _mariaDbController;
    private readonly IVersionManager _versionManager;
    private readonly IConfigurationManager _configManager;
    private readonly IHealthChecker _healthChecker;
    private readonly IVcRedistChecker _vcRedistChecker;
    private readonly IPortManager _portManager;
    private readonly DialogService _dialogService;
    private readonly ToastService _toastService;
    private readonly HttpClient _httpClient;
    private readonly string _basePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartApache))]
    [NotifyPropertyChangedFor(nameof(CanStopApache))]
    [NotifyPropertyChangedFor(nameof(CanReloadApache))]
    [NotifyPropertyChangedFor(nameof(IsApacheRunning))]
    [NotifyPropertyChangedFor(nameof(CanToggleApache))]
    private ServiceStatus _apacheStatus = ServiceStatus.Stopped;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartMariaDb))]
    [NotifyPropertyChangedFor(nameof(CanStopMariaDb))]
    [NotifyPropertyChangedFor(nameof(IsMariaDbRunning))]
    [NotifyPropertyChangedFor(nameof(CanToggleMariaDb))]
    private ServiceStatus _mariaDbStatus = ServiceStatus.Stopped;

    public bool CanStartApache => ApacheStatus != ServiceStatus.Running && !IsBusy;
    public bool CanStopApache => ApacheStatus == ServiceStatus.Running && !IsBusy;
    public bool CanReloadApache => ApacheStatus == ServiceStatus.Running && !IsBusy;
    public bool CanStartMariaDb => MariaDbStatus != ServiceStatus.Running && !IsBusy;
    public bool CanStopMariaDb => MariaDbStatus == ServiceStatus.Running && !IsBusy;

    public bool IsApacheRunning => ApacheStatus == ServiceStatus.Running;
    public bool IsMariaDbRunning => MariaDbStatus == ServiceStatus.Running;
    public bool CanToggleApache => ApacheStatus != ServiceStatus.Starting && ApacheStatus != ServiceStatus.Stopping && !IsBusy;
    public bool CanToggleMariaDb => MariaDbStatus != ServiceStatus.Starting && MariaDbStatus != ServiceStatus.Stopping && !IsBusy;

    [ObservableProperty]
    private string _activeApacheVersion = "Yüklü değil";

    [ObservableProperty]
    private string _activePhpVersion = "Yüklü değil";

    [ObservableProperty]
    private string _activeMariaDbVersion = "Yüklü değil";

    [ObservableProperty]
    private int _apachePort = 8080;

    [ObservableProperty]
    private int _mysqlPort = 3306;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartApache))]
    [NotifyPropertyChangedFor(nameof(CanStopApache))]
    [NotifyPropertyChangedFor(nameof(CanReloadApache))]
    [NotifyPropertyChangedFor(nameof(CanStartMariaDb))]
    [NotifyPropertyChangedFor(nameof(CanStopMariaDb))]
    [NotifyPropertyChangedFor(nameof(CanToggleApache))]
    [NotifyPropertyChangedFor(nameof(CanToggleMariaDb))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Hazır";

    // Tunnel
    private Process? _tunnelProcess;

    [ObservableProperty]
    private string _tunnelUrl = "";

    [ObservableProperty]
    private bool _isTunnelRunning;

    // MariaDB Settings
    [ObservableProperty]
    private string _mariaDbInnodbBufferPoolSize = "128M";

    [ObservableProperty]
    private int _mariaDbMaxConnections = 151;

    [ObservableProperty]
    private string _mariaDbMaxAllowedPacket = "16M";

    public ObservableCollection<HealthCheckResult> HealthResults { get; } = [];
    public ObservableCollection<QuickFix> DetectedIssues { get; } = [];

    public DashboardViewModel(
        OrchestratorService orchestrator,
        IServiceController apacheController,
        MariaDbService mariaDbController,
        IVersionManager versionManager,
        IConfigurationManager configManager,
        IHealthChecker healthChecker,
        IVcRedistChecker vcRedistChecker,
        IPortManager portManager,
        DialogService dialogService,
        ToastService toastService,
        HttpClient httpClient,
        string basePath)
    {
        _orchestrator = orchestrator;
        _apacheController = apacheController;
        _mariaDbController = mariaDbController;
        _versionManager = versionManager;
        _configManager = configManager;
        _healthChecker = healthChecker;
        _vcRedistChecker = vcRedistChecker;
        _portManager = portManager;
        _dialogService = dialogService;
        _toastService = toastService;
        _httpClient = httpClient;
        _basePath = basePath;

        _apacheController.StatusChanged += (_, status) => ApacheStatus = status;
        _mariaDbController.StatusChanged += (_, status) => MariaDbStatus = status;

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();
            ApachePort = config.ApachePort;
            MysqlPort = config.MySqlPort;
            ActiveApacheVersion = string.IsNullOrEmpty(config.ActiveApacheVersion) ? "Yüklü değil" : config.ActiveApacheVersion;
            ActivePhpVersion = string.IsNullOrEmpty(config.ActivePhpVersion) ? "Yüklü değil" : config.ActivePhpVersion;
            ActiveMariaDbVersion = string.IsNullOrEmpty(config.ActiveMariaDbVersion) ? "Yüklü değil" : config.ActiveMariaDbVersion;
            ApacheStatus = await _apacheController.GetStatusAsync();
            MariaDbStatus = await _mariaDbController.GetStatusAsync();

            // Load MariaDB settings
            MariaDbInnodbBufferPoolSize = config.MariaDbInnodbBufferPoolSize;
            MariaDbMaxConnections = config.MariaDbMaxConnections;
            MariaDbMaxAllowedPacket = config.MariaDbMaxAllowedPacket;
        }
        catch { /* first run */ }
    }

    [RelayCommand]
    private async Task StartApacheAsync()
    {
        IsBusy = true;
        StatusMessage = "Apache başlatılıyor...";
        try
        {
            var installed = await _versionManager.GetInstalledVersionsAsync(ServiceType.Apache);
            if (installed.Count == 0)
            {
                _dialogService.ShowWarning(
                    "Apache henüz yüklü değil.\nApache sayfasına gidin → Sürümleri Getir → Bir sürüm indirin.",
                    "Başlatılamıyor");
                StatusMessage = "Apache yüklü değil";
                return;
            }

            try
            {
                await _apacheController.StartAsync();
            }
            catch (PortConflictException pcEx)
            {
                var oldPort = pcEx.Port;
                var newPort = await _portManager.FindAvailablePortAsync(oldPort + 1, oldPort + 100);
                if (newPort == null)
                    throw new ServiceStartException("Apache", $"Port {oldPort} meşgul ve uygun boş port bulunamadı.");

                var config = await _configManager.LoadAsync();
                config.ApachePort = newPort.Value;
                await _configManager.SaveAsync(config);
                await _apacheController.RegenerateConfigAsync();

                await _apacheController.StartAsync();
                ApachePort = newPort.Value;
                _toastService.ShowWarning($"Port {oldPort} meşgul olduğundan Apache port {newPort.Value}'de başlatıldı.");
            }

            StatusMessage = "Apache başlatıldı";
            _toastService.ShowSuccess("Apache başarıyla başlatıldı");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Hata: {ex.Message}";
            _toastService.ShowError($"Apache başlatılamadı: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task StopApacheAsync()
    {
        IsBusy = true;
        StatusMessage = "Apache durduruluyor...";
        try
        {
            await _apacheController.StopAsync();
            StatusMessage = "Apache durduruldu";
            _toastService.ShowInfo("Apache durduruldu");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Hata: {ex.Message}";
            _toastService.ShowError($"Apache durdurulamadı: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ReloadApacheAsync()
    {
        IsBusy = true;
        try
        {
            await _apacheController.ReloadAsync();
            _toastService.ShowSuccess("Apache yapılandırması yeniden yüklendi");
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Apache reload başarısız: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ToggleApacheAsync()
    {
        if (ApacheStatus == ServiceStatus.Running)
            await StopApacheAsync();
        else
            await StartApacheAsync();
    }

    [RelayCommand]
    private async Task ToggleMariaDbAsync()
    {
        if (MariaDbStatus == ServiceStatus.Running)
            await StopMariaDbAsync();
        else
            await StartMariaDbAsync();
    }

    [RelayCommand]
    private async Task StartMariaDbAsync()
    {
        IsBusy = true;
        StatusMessage = "MariaDB başlatılıyor...";
        try
        {
            var installed = await _versionManager.GetInstalledVersionsAsync(ServiceType.MariaDb);
            if (installed.Count == 0)
            {
                var download = _dialogService.Confirm(
                    "MariaDB henüz yüklü değil.\nŞimdi indirip yüklemek ister misiniz?",
                    "MariaDB Yüklü Değil");

                if (!download)
                {
                    StatusMessage = "MariaDB yüklü değil";
                    return;
                }

                await DownloadMariaDbAsync();
                return;
            }

            try
            {
                await _mariaDbController.StartAsync();
            }
            catch (PortConflictException pcEx)
            {
                var oldPort = pcEx.Port;
                var newPort = await _portManager.FindAvailablePortAsync(oldPort + 1, oldPort + 100);
                if (newPort == null)
                    throw new ServiceStartException("MariaDB", $"Port {oldPort} meşgul ve uygun boş port bulunamadı.");

                var config = await _configManager.LoadAsync();
                config.MySqlPort = newPort.Value;
                await _configManager.SaveAsync(config);
                await _mariaDbController.RegenerateConfigAsync();

                await _mariaDbController.StartAsync();
                MysqlPort = newPort.Value;
                _toastService.ShowWarning($"Port {oldPort} meşgul olduğundan MariaDB port {newPort.Value}'de başlatıldı.");
            }

            StatusMessage = "MariaDB başlatıldı";
            _toastService.ShowSuccess("MariaDB başarıyla başlatıldı");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Hata: {ex.Message}";
            _toastService.ShowError($"MariaDB başlatılamadı: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private async Task DownloadMariaDbAsync()
    {
        try
        {
            StatusMessage = "MariaDB sürümleri alınıyor...";
            var versions = await _versionManager.GetAvailableVersionsAsync(ServiceType.MariaDb);
            if (versions.Count == 0)
            {
                _dialogService.ShowError("MariaDB sürümü bulunamadı.", "İndirme Başarısız");
                StatusMessage = "İndirme başarısız";
                return;
            }

            var latest = versions[0];
            StatusMessage = $"MariaDB {latest.Version} indiriliyor...";

            await _versionManager.InstallVersionAsync(latest);
            await _versionManager.SetActiveVersionAsync(ServiceType.MariaDb, latest.Version);

            var config = await _configManager.LoadAsync();
            config.ActiveMariaDbVersion = latest.Version;
            await _configManager.SaveAsync(config);

            ActiveMariaDbVersion = latest.Version;
            StatusMessage = $"MariaDB {latest.Version} yüklendi! Başlatmak için 'Başlat' butonuna tıklayın.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"İndirme başarısız: {ex.Message}";
            _dialogService.ShowError(ex.Message, "MariaDB İndirme Başarısız");
        }
    }

    [RelayCommand]
    private async Task StopMariaDbAsync()
    {
        IsBusy = true;
        StatusMessage = "MariaDB durduruluyor...";
        try
        {
            await _mariaDbController.StopAsync();
            StatusMessage = "MariaDB durduruldu";
            _toastService.ShowInfo("MariaDB durduruldu");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Hata: {ex.Message}";
            _toastService.ShowError($"MariaDB durdurulamadı: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void OpenWeb()
    {
        try
        {
            var url = $"http://localhost:{ApachePort}";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) { _toastService.ShowError($"Tarayıcı açılamadı: {ex.Message}"); }
    }

    [RelayCommand]
    private void OpenRoot()
    {
        try
        {
            var rootPath = Path.GetFullPath(Path.Combine(_basePath, Defaults.DocumentRoot));
            if (Directory.Exists(rootPath))
                Process.Start(new ProcessStartInfo("explorer.exe", rootPath))?.Dispose();
            else
                _toastService.ShowWarning("Document root klasörü bulunamadı");
        }
        catch (Exception ex) { _toastService.ShowError($"Klasör açılamadı: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task OpenPhpMyAdminAsync()
    {
        var pmaPath = Path.GetFullPath(Path.Combine(_basePath, Defaults.AppsDir, "phpmyadmin"));

        if (!Directory.Exists(pmaPath) || !File.Exists(Path.Combine(pmaPath, "index.php")))
        {
            var install = _dialogService.Confirm(
                "phpMyAdmin henüz yüklü değil.\nŞimdi indirip yüklemek ister misiniz?",
                "phpMyAdmin Bulunamadı");

            if (!install) return;

            IsBusy = true;
            try
            {
                await InstallPhpMyAdminAsync(pmaPath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"phpMyAdmin yükleme başarısız: {ex.Message}";
                _dialogService.ShowError(ex.Message, "phpMyAdmin Yükleme Başarısız");
                return;
            }
            finally { IsBusy = false; }
        }

        try
        {
            var url = $"http://localhost:{ApachePort}/phpmyadmin";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) { _toastService.ShowError($"phpMyAdmin açılamadı: {ex.Message}"); }
    }

    private async Task InstallPhpMyAdminAsync(string targetPath)
    {
        const string pmaVersion = "5.2.3";
        var downloadUrl = $"https://files.phpmyadmin.net/phpMyAdmin/{pmaVersion}/phpMyAdmin-{pmaVersion}-all-languages.zip";

        StatusMessage = $"phpMyAdmin {pmaVersion} indiriliyor...";

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        var tempZip = Path.Combine(Path.GetTempPath(), $"phpmyadmin-{pmaVersion}.zip");
        try
        {
            var bytes = await _httpClient.GetByteArrayAsync(downloadUrl, cts.Token);
            await File.WriteAllBytesAsync(tempZip, bytes);

            StatusMessage = "phpMyAdmin çıkarılıyor...";

            var tempExtract = Path.Combine(Path.GetTempPath(), "phpmyadmin-extract");
            if (Directory.Exists(tempExtract))
                Directory.Delete(tempExtract, true);

            ZipFile.ExtractToDirectory(tempZip, tempExtract);

            // phpMyAdmin extracts into a subfolder like phpMyAdmin-5.2.3-all-languages
            var extractedDir = Directory.GetDirectories(tempExtract)[0];

            if (Directory.Exists(targetPath))
                Directory.Delete(targetPath, true);

            Directory.Move(extractedDir, targetPath);

            // Create config.inc.php
            var config = await _configManager.LoadAsync();
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
            await File.WriteAllTextAsync(Path.Combine(targetPath, "config.inc.php"), configPhp);

            // Create tmp directory
            Directory.CreateDirectory(Path.Combine(targetPath, "tmp"));

            // Create Apache alias config
            var aliasDir = Path.GetFullPath(Path.Combine(_basePath, Defaults.ApacheAliasDir));
            Directory.CreateDirectory(aliasDir);
            var pmaPathForward = targetPath.Replace('\\', '/');
            var aliasConf = $"""
                Alias /phpmyadmin "{pmaPathForward}/"
                <Directory "{pmaPathForward}/">
                    Options Indexes FollowSymLinks
                    AllowOverride All
                    Require all granted
                </Directory>
                """;
            await File.WriteAllTextAsync(Path.Combine(aliasDir, "phpmyadmin.conf"), aliasConf);

            // Cleanup
            Directory.Delete(tempExtract, true);

            StatusMessage = "phpMyAdmin başarıyla yüklendi!";
        }
        finally
        {
            if (File.Exists(tempZip))
                File.Delete(tempZip);
        }
    }

    [RelayCommand]
    private async Task OpenTerminalAsync()
    {
        try
        {
            var rootPath = Path.GetFullPath(Path.Combine(_basePath, Defaults.DocumentRoot));
            if (!Directory.Exists(rootPath))
                rootPath = _basePath;

            var config = await _configManager.LoadAsync();
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/K \"color 0A && title ZaraGON Terminal && echo. && echo  ======================================== && echo   ZaraGON Terminal && echo   Document Root: %CD% && echo  ======================================== && echo.\"",
                WorkingDirectory = rootPath,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            var extraDirs = new List<string>();
            if (!string.IsNullOrEmpty(config.ActivePhpVersion))
            {
                var phpDir = Path.GetFullPath(Path.Combine(_basePath, Defaults.BinDir, "php", config.ActivePhpVersion));
                if (Directory.Exists(phpDir)) extraDirs.Add(phpDir);
            }
            if (!string.IsNullOrEmpty(config.ActiveMariaDbVersion))
            {
                var mariaDir = Path.GetFullPath(Path.Combine(_basePath, Defaults.BinDir, "mariadb", config.ActiveMariaDbVersion, "bin"));
                if (Directory.Exists(mariaDir)) extraDirs.Add(mariaDir);
            }
            var composerDir = Path.GetFullPath(Path.Combine(_basePath, Defaults.ComposerDir));
            if (Directory.Exists(composerDir)) extraDirs.Add(composerDir);

            if (extraDirs.Count > 0)
            {
                var existingPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                psi.Environment["PATH"] = string.Join(";", extraDirs) + ";" + existingPath;
            }

            Process.Start(psi)?.Dispose();
        }
        catch (Exception ex) { _toastService.ShowError($"Terminal açılamadı: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task StartAllAsync()
    {
        IsBusy = true;
        StatusMessage = "Tüm servisler başlatılıyor...";
        try
        {
            await _orchestrator.StartAllAsync();
            StatusMessage = "Tüm servisler başlatıldı";
            _toastService.ShowSuccess("T\u00fcm servisler ba\u015flat\u0131ld\u0131");
            await RefreshHealthAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Hata: {ex.Message}";
            _dialogService.ShowError(ex.Message, "Başlatma Başarısız");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task StopAllAsync()
    {
        IsBusy = true;
        StatusMessage = "Tüm servisler durduruluyor...";
        try
        {
            await _orchestrator.StopAllAsync();
            StatusMessage = "Tüm servisler durduruldu";
            _toastService.ShowInfo("T\u00fcm servisler durduruldu");
            await RefreshHealthAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Hata: {ex.Message}";
            _toastService.ShowError($"Servisler durdurulamadı: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveMariaDbSettingsAsync()
    {
        IsBusy = true;
        try
        {
            var config = await _configManager.LoadAsync();
            config.MariaDbInnodbBufferPoolSize = MariaDbInnodbBufferPoolSize;
            config.MariaDbMaxConnections = MariaDbMaxConnections;
            config.MariaDbMaxAllowedPacket = MariaDbMaxAllowedPacket;
            await _configManager.SaveAsync(config);

            // Restart MariaDB if running to apply new config
            if (MariaDbStatus == ServiceStatus.Running)
            {
                StatusMessage = "MariaDB yeniden başlatılıyor...";
                await _mariaDbController.RestartAsync();
            }

            StatusMessage = "MariaDB ayarlar\u0131 kaydedildi";
            _toastService.ShowSuccess("MariaDB ayarlar\u0131 kaydedildi");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Kaydetme hatas\u0131: {ex.Message}";
            _toastService.ShowError($"Kaydetme hatas\u0131: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void OpenApacheConfig()
    {
        try
        {
            var confPath = Path.GetFullPath(Path.Combine(_basePath, Defaults.ApacheConfigDir, "httpd.conf"));
            if (File.Exists(confPath))
                Process.Start(new ProcessStartInfo(confPath) { UseShellExecute = true })?.Dispose();
            else
                _toastService.ShowWarning("httpd.conf bulunamadı");
        }
        catch (Exception ex) { _toastService.ShowError($"Dosya açılamadı: {ex.Message}"); }
    }

    [RelayCommand]
    private void OpenPhpIni()
    {
        try
        {
            var iniPath = Path.GetFullPath(Path.Combine(_basePath, Defaults.PhpConfigDir, "php.ini"));
            if (File.Exists(iniPath))
                Process.Start(new ProcessStartInfo(iniPath) { UseShellExecute = true })?.Dispose();
            else
                _toastService.ShowWarning("php.ini bulunamadı");
        }
        catch (Exception ex) { _toastService.ShowError($"Dosya açılamadı: {ex.Message}"); }
    }

    [RelayCommand]
    private void OpenMariaDbConfig()
    {
        try
        {
            var confPath = Path.GetFullPath(Path.Combine(_basePath, Defaults.MariaDbConfigDir, "my.ini"));
            if (File.Exists(confPath))
                Process.Start(new ProcessStartInfo(confPath) { UseShellExecute = true })?.Dispose();
            else
                _toastService.ShowWarning("my.ini bulunamadı");
        }
        catch (Exception ex) { _toastService.ShowError($"Dosya açılamadı: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task ScanForIssuesAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();
            DetectedIssues.Clear();

            // Check upload limit
            if (ParseSizeToMb(config.PhpUploadMaxFilesize) < 10)
            {
                DetectedIssues.Add(new QuickFix
                {
                    Title = "Düşük Upload Limiti",
                    Description = $"upload_max_filesize = {config.PhpUploadMaxFilesize} (önerilen: 128M)",
                    IconKind = "Upload",
                    Category = "upload_limit",
                    IsDetected = true,
                    DetectionDetail = config.PhpUploadMaxFilesize
                });
            }

            // Check memory limit
            if (ParseSizeToMb(config.PhpMemoryLimit) < 128)
            {
                DetectedIssues.Add(new QuickFix
                {
                    Title = "Düşük Bellek Limiti",
                    Description = $"memory_limit = {config.PhpMemoryLimit} (önerilen: 256M)",
                    IconKind = "Memory",
                    Category = "memory_limit",
                    IsDetected = true,
                    DetectionDetail = config.PhpMemoryLimit
                });
            }

            // Check execution timeout
            if (config.PhpMaxExecutionTime < 60)
            {
                DetectedIssues.Add(new QuickFix
                {
                    Title = "Kısa Zaman Aşımı",
                    Description = $"max_execution_time = {config.PhpMaxExecutionTime}s (önerilen: 300s)",
                    IconKind = "TimerAlert",
                    Category = "execution_time",
                    IsDetected = true,
                    DetectionDetail = config.PhpMaxExecutionTime.ToString()
                });
            }

            // Check display_errors
            if (!config.PhpDisplayErrors)
            {
                DetectedIssues.Add(new QuickFix
                {
                    Title = "Hata Görüntüleme Kapalı",
                    Description = "display_errors = Off (geliştirme için On önerilir)",
                    IconKind = "BugOutline",
                    Category = "display_errors",
                    IsDetected = true,
                    DetectionDetail = "Off"
                });
            }

            // Check VC++ Redistributable
            try
            {
                if (!string.IsNullOrEmpty(config.ActivePhpVersion))
                {
                    var installed = await _versionManager.GetInstalledVersionsAsync(ServiceType.Php);
                    var active = installed.FirstOrDefault(v => v.IsActive);
                    if (active?.VsVersion != null)
                    {
                        var vcStatus = await _vcRedistChecker.CheckCompatibilityAsync(active.VsVersion);
                        if (!vcStatus.IsCompatible)
                        {
                            var installedText = vcStatus.InstalledVersion != null
                                ? $"v{vcStatus.InstalledVersion.Major}.{vcStatus.InstalledVersion.Minor}"
                                : "yüklü değil";
                            DetectedIssues.Add(new QuickFix
                            {
                                Title = "VC++ Runtime Uyumsuz",
                                Description = $"PHP ({active.VsVersion}) için VC++ Runtime gerekli (mevcut: {installedText})",
                                IconKind = "PackageVariantClosed",
                                Category = "vcredist",
                                IsDetected = true,
                                DetectionDetail = active.VsVersion
                            });
                        }
                    }
                }
            }
            catch { /* VC++ check failed, skip */ }

            if (DetectedIssues.Count == 0)
                StatusMessage = "Sorun bulunamadı - yapılandırma iyi görünüyor!";
            else
                StatusMessage = $"{DetectedIssues.Count} sorun tespit edildi";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Tarama hatası: {ex.Message}";
            _toastService.ShowError($"Tarama hatası: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ApplyQuickFixAsync(QuickFix? fix)
    {
        if (fix == null) return;

        IsBusy = true;
        try
        {
            var config = await _configManager.LoadAsync();

            switch (fix.Category)
            {
                case "upload_limit":
                    config.PhpUploadMaxFilesize = "128M";
                    config.PhpPostMaxSize = "128M";
                    break;
                case "memory_limit":
                    config.PhpMemoryLimit = "256M";
                    break;
                case "execution_time":
                    config.PhpMaxExecutionTime = 300;
                    config.PhpMaxInputTime = 300;
                    break;
                case "display_errors":
                    config.PhpDisplayErrors = true;
                    break;
                case "vcredist":
                    StatusMessage = "VC++ Runtime kuruluyor...";
                    await _vcRedistChecker.InstallAsync();
                    StatusMessage = "VC++ Runtime kuruldu";
                    _toastService.ShowSuccess("VC++ Runtime başarıyla kuruldu");
                    await ScanForIssuesAsync();
                    return;
                default:
                    _toastService.ShowWarning($"Bilinmeyen kategori: {fix.Category}");
                    return;
            }

            await _configManager.SaveAsync(config);

            // Regenerate php.ini
            if (!string.IsNullOrEmpty(config.ActivePhpVersion))
            {
                await _orchestrator.SwitchPhpVersionAsync(config.ActivePhpVersion);
            }

            StatusMessage = $"'{fix.Title}' d\u00fczeltildi";
            _toastService.ShowSuccess($"'{fix.Title}' d\u00fczeltildi");

            // Rescan
            await ScanForIssuesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Düzeltme hatası: {ex.Message}";
            _dialogService.ShowError(ex.Message);
        }
        finally { IsBusy = false; }
    }

    private static int ParseSizeToMb(string size)
    {
        if (string.IsNullOrWhiteSpace(size)) return 0;
        size = size.Trim().ToUpperInvariant();
        if (size.EndsWith("G"))
            return int.TryParse(size[..^1], out var g) ? g * 1024 : 0;
        if (size.EndsWith("M"))
            return int.TryParse(size[..^1], out var m) ? m : 0;
        if (size.EndsWith("K"))
            return int.TryParse(size[..^1], out var k) ? (k + 1023) / 1024 : 0;
        // Bare number treated as bytes
        return int.TryParse(size, out var b) ? (b + 1048575) / (1024 * 1024) : 0;
    }

    [RelayCommand]
    private async Task CreateDatabaseAsync()
    {
        var dbName = _dialogService.PromptInput("Veritaban\u0131 ad\u0131 girin:", "Veritaban\u0131 Olu\u015ftur");
        if (string.IsNullOrWhiteSpace(dbName)) return;

        // Sanitize: only allow alphanumeric + underscore
        if (!System.Text.RegularExpressions.Regex.IsMatch(dbName, @"^[a-zA-Z0-9_]+$"))
        {
            _toastService.ShowWarning("Veritaban\u0131 ad\u0131 yaln\u0131zca harf, rakam ve alt \u00e7izgi i\u00e7erebilir");
            return;
        }

        IsBusy = true;
        try
        {
            var config = await _configManager.LoadAsync();
            var mariaDir = Path.GetFullPath(Path.Combine(_basePath, Defaults.BinDir, "mariadb", config.ActiveMariaDbVersion, "bin"));
            var mysqlExe = Path.Combine(mariaDir, "mysql.exe");

            if (!File.Exists(mysqlExe))
            {
                _toastService.ShowError("mysql.exe bulunamad\u0131");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = mysqlExe,
                Arguments = $"-u root -P {config.MySqlPort} -e \"CREATE DATABASE `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    _toastService.ShowSuccess($"Veritaban\u0131 '{dbName}' olu\u015fturuldu");
                    StatusMessage = $"Veritaban\u0131 '{dbName}' olu\u015fturuldu";
                }
                else
                {
                    _toastService.ShowError($"Veritaban\u0131 olu\u015fturulamad\u0131: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Veritaban\u0131 olu\u015fturma hatas\u0131: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task BackupDatabaseAsync()
    {
        IsBusy = true;
        try
        {
            var config = await _configManager.LoadAsync();
            var mariaDir = Path.GetFullPath(Path.Combine(_basePath, Defaults.BinDir, "mariadb", config.ActiveMariaDbVersion, "bin"));
            var dumpExe = Path.Combine(mariaDir, "mysqldump.exe");

            if (!File.Exists(dumpExe))
            {
                _toastService.ShowError("mysqldump.exe bulunamad\u0131");
                return;
            }

            var backupDir = Path.GetFullPath(Path.Combine(_basePath, "backups"));
            Directory.CreateDirectory(backupDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFile = Path.Combine(backupDir, $"backup_{timestamp}.sql");

            StatusMessage = "Veritaban\u0131 yedekleniyor...";

            var psi = new ProcessStartInfo
            {
                FileName = dumpExe,
                Arguments = $"-u root -P {config.MySqlPort} --all-databases",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    await File.WriteAllTextAsync(backupFile, output);
                    _toastService.ShowSuccess($"Yedekleme tamamland\u0131: backup_{timestamp}.sql");
                    StatusMessage = $"Yedekleme tamamland\u0131: backup_{timestamp}.sql";
                }
                else
                {
                    _toastService.ShowError($"Yedekleme ba\u015far\u0131s\u0131z: {error}");
                    StatusMessage = "Yedekleme ba\u015far\u0131s\u0131z";
                }
            }
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Yedekleme hatas\u0131: {ex.Message}");
            StatusMessage = $"Yedekleme hatas\u0131: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ImportDatabaseAsync()
    {
        try
        {
            var openDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "SQL Dosyası Seçin",
                Filter = "SQL Dosyaları (*.sql)|*.sql|Tüm Dosyalar (*.*)|*.*",
                DefaultExt = ".sql"
            };

            if (openDialog.ShowDialog() != true) return;

            var sqlFile = openDialog.FileName;

            var dbName = _dialogService.PromptInput(
                "Hedef veritabanı adı girin (boş bırakırsanız tüm veritabanlarına import edilir):",
                "Veritabanı İçe Aktar");

            // User cancelled the dialog
            if (dbName == null) return;

            // Sanitize db name if provided
            if (!string.IsNullOrWhiteSpace(dbName) &&
                !System.Text.RegularExpressions.Regex.IsMatch(dbName, @"^[a-zA-Z0-9_]+$"))
            {
                _toastService.ShowWarning("Veritabanı adı yalnızca harf, rakam ve alt çizgi içerebilir");
                return;
            }

            IsBusy = true;
            StatusMessage = "Veritabanı içe aktarılıyor...";

            var config = await _configManager.LoadAsync();
            var mariaDir = Path.GetFullPath(Path.Combine(_basePath, Defaults.BinDir, "mariadb", config.ActiveMariaDbVersion, "bin"));
            var mysqlExe = Path.Combine(mariaDir, "mysql.exe");

            if (!File.Exists(mysqlExe))
            {
                _toastService.ShowError("mysql.exe bulunamadı");
                return;
            }

            var dbArg = string.IsNullOrWhiteSpace(dbName) ? "" : $" \"{dbName}\"";
            var escapedFile = sqlFile.Replace("\\", "\\\\");

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C \"\"{mysqlExe}\" -u root -P {config.MySqlPort}{dbArg} < \"{sqlFile}\"\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    var target = string.IsNullOrWhiteSpace(dbName) ? "tüm veritabanları" : $"'{dbName}'";
                    _toastService.ShowSuccess($"İçe aktarma tamamlandı: {target}");
                    StatusMessage = $"İçe aktarma tamamlandı: {target}";
                }
                else
                {
                    _toastService.ShowError($"İçe aktarma başarısız: {error}");
                    StatusMessage = "İçe aktarma başarısız";
                }
            }
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"İçe aktarma hatası: {ex.Message}");
            StatusMessage = $"İçe aktarma hatası: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ToggleTunnelAsync()
    {
        if (IsTunnelRunning)
        {
            // Stop tunnel
            try
            {
                if (_tunnelProcess is { HasExited: false })
                {
                    _tunnelProcess.Kill(entireProcessTree: true);
                    _tunnelProcess.Dispose();
                }
            }
            catch { /* process already exited */ }

            _tunnelProcess = null;
            IsTunnelRunning = false;
            TunnelUrl = "";
            StatusMessage = "Tünel kapatıldı";
            _toastService.ShowInfo("Tünel kapatıldı");
            return;
        }

        IsBusy = true;
        StatusMessage = "Tünel başlatılıyor...";
        try
        {
            var cloudflaredDir = Path.GetFullPath(Path.Combine(_basePath, Defaults.CloudflaredDir));
            var cloudflaredExe = Path.Combine(cloudflaredDir, "cloudflared.exe");

            // Download if not exists
            if (!File.Exists(cloudflaredExe))
            {
                StatusMessage = "cloudflared indiriliyor...";
                Directory.CreateDirectory(cloudflaredDir);

                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                var bytes = await _httpClient.GetByteArrayAsync(Defaults.CloudflaredDownloadUrl, cts.Token);
                await File.WriteAllBytesAsync(cloudflaredExe, bytes);

                _toastService.ShowSuccess("cloudflared indirildi");
            }

            var config = await _configManager.LoadAsync();
            var psi = new ProcessStartInfo
            {
                FileName = cloudflaredExe,
                Arguments = $"tunnel --url http://localhost:{config.ApachePort}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _tunnelProcess = Process.Start(psi);
            if (_tunnelProcess == null)
            {
                _toastService.ShowError("Tünel işlemi başlatılamadı");
                return;
            }

            // Parse URL from stderr (cloudflared outputs connection info to stderr)
            var urlFound = false;
            _tunnelProcess.ErrorDataReceived += (_, e) =>
            {
                if (urlFound || string.IsNullOrEmpty(e.Data)) return;

                // cloudflared outputs the URL in a line like: "... https://xxxxx.trycloudflare.com ..."
                var match = System.Text.RegularExpressions.Regex.Match(e.Data, @"https://[a-zA-Z0-9\-]+\.trycloudflare\.com");
                if (match.Success)
                {
                    urlFound = true;
                    var url = match.Value;
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        TunnelUrl = url;
                        IsTunnelRunning = true;
                        IsBusy = false;
                        StatusMessage = $"Tünel aktif: {url}";

                        // Copy to clipboard
                        try { System.Windows.Clipboard.SetText(url); } catch { }
                        _toastService.ShowSuccess($"Tünel açıldı! URL kopyalandı: {url}");
                    });
                }
            };
            _tunnelProcess.BeginErrorReadLine();

            // Also read stdout in case URL appears there
            _tunnelProcess.OutputDataReceived += (_, e) =>
            {
                if (urlFound || string.IsNullOrEmpty(e.Data)) return;

                var match = System.Text.RegularExpressions.Regex.Match(e.Data, @"https://[a-zA-Z0-9\-]+\.trycloudflare\.com");
                if (match.Success)
                {
                    urlFound = true;
                    var url = match.Value;
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        TunnelUrl = url;
                        IsTunnelRunning = true;
                        IsBusy = false;
                        StatusMessage = $"Tünel aktif: {url}";

                        try { System.Windows.Clipboard.SetText(url); } catch { }
                        _toastService.ShowSuccess($"Tünel açıldı! URL kopyalandı: {url}");
                    });
                }
            };
            _tunnelProcess.BeginOutputReadLine();

            // Wait up to 15 seconds for URL
            await Task.Run(async () =>
            {
                for (var i = 0; i < 30 && !urlFound; i++)
                    await Task.Delay(500);
            });

            if (!urlFound)
            {
                // Process may have crashed
                if (_tunnelProcess.HasExited)
                {
                    _toastService.ShowError("Tünel başlatılamadı — cloudflared beklenmedik şekilde kapandı");
                    _tunnelProcess.Dispose();
                    _tunnelProcess = null;
                }
                else
                {
                    // Still running but no URL yet — mark as running anyway
                    IsTunnelRunning = true;
                    StatusMessage = "Tünel başlatıldı (URL henüz alınamadı)";
                    _toastService.ShowWarning("Tünel başlatıldı ancak URL henüz alınamadı");
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Tünel hatası: {ex.Message}";
            _toastService.ShowError($"Tünel başlatılamadı: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    /// <summary>Kills the tunnel process if running. Called during app shutdown.</summary>
    public void StopTunnel()
    {
        try
        {
            if (_tunnelProcess is { HasExited: false })
            {
                _tunnelProcess.Kill(entireProcessTree: true);
                _tunnelProcess.Dispose();
            }
        }
        catch { /* already exited */ }

        _tunnelProcess = null;
        IsTunnelRunning = false;
        TunnelUrl = "";
    }

    [RelayCommand]
    private async Task RefreshHealthAsync()
    {
        try
        {
            var results = await _healthChecker.RunAllChecksAsync();
            HealthResults.Clear();
            foreach (var result in results)
                HealthResults.Add(result);
        }
        catch { /* health check failed */ }
    }
}
