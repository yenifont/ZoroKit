using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZaraGON.Application.Services;
using ZaraGON.Core.Constants;
using ZaraGON.Core.Enums;
using ZaraGON.Core.Interfaces.Infrastructure;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;
using ZaraGON.UI.Services;

namespace ZaraGON.UI.ViewModels;

public sealed partial class PhpViewModel : ObservableObject
{
    private readonly IVersionManager _versionManager;
    private readonly IPhpExtensionManager _extensionManager;
    private readonly IConfigurationManager _configManager;
    private readonly OrchestratorService _orchestrator;
    private readonly IVcRedistChecker _vcRedistChecker;
    private readonly DialogService _dialogService;
    private readonly ToastService _toastService;
    private readonly string _basePath;

    [ObservableProperty]
    private string _activeVersion = "None";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStatus = string.Empty;

    // PHP Settings
    [ObservableProperty]
    private string _phpMemoryLimit = "256M";

    [ObservableProperty]
    private string _phpUploadMaxFilesize = "128M";

    [ObservableProperty]
    private string _phpPostMaxSize = "128M";

    [ObservableProperty]
    private int _phpMaxExecutionTime = 300;

    [ObservableProperty]
    private int _phpMaxInputTime = 300;

    [ObservableProperty]
    private int _phpMaxFileUploads = 20;

    [ObservableProperty]
    private int _phpMaxInputVars = 1000;

    [ObservableProperty]
    private bool _phpDisplayErrors = true;

    [ObservableProperty]
    private string _phpErrorReporting = "E_ALL";

    [ObservableProperty]
    private string _phpDateTimezone = "UTC";

    public ObservableCollection<ServiceVersion> AvailableVersions { get; } = [];
    public ObservableCollection<VersionPointer> InstalledVersions { get; } = [];
    public ObservableCollection<PhpExtension> Extensions { get; } = [];

    public PhpViewModel(
        IVersionManager versionManager,
        IPhpExtensionManager extensionManager,
        IConfigurationManager configManager,
        OrchestratorService orchestrator,
        IVcRedistChecker vcRedistChecker,
        DialogService dialogService,
        ToastService toastService,
        string basePath)
    {
        _versionManager = versionManager;
        _extensionManager = extensionManager;
        _configManager = configManager;
        _orchestrator = orchestrator;
        _vcRedistChecker = vcRedistChecker;
        _dialogService = dialogService;
        _toastService = toastService;
        _basePath = basePath;

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();
            ActiveVersion = string.IsNullOrEmpty(config.ActivePhpVersion) ? "None" : config.ActivePhpVersion;

            // Load PHP settings from config
            PhpMemoryLimit = config.PhpMemoryLimit;
            PhpUploadMaxFilesize = config.PhpUploadMaxFilesize;
            PhpPostMaxSize = config.PhpPostMaxSize;
            PhpMaxExecutionTime = config.PhpMaxExecutionTime;
            PhpMaxInputTime = config.PhpMaxInputTime;
            PhpMaxFileUploads = config.PhpMaxFileUploads;
            PhpMaxInputVars = config.PhpMaxInputVars;
            PhpDisplayErrors = config.PhpDisplayErrors;
            PhpErrorReporting = config.PhpErrorReporting;
            PhpDateTimezone = config.PhpDateTimezone;

            var installed = await _versionManager.GetInstalledVersionsAsync(ServiceType.Php);
            InstalledVersions.Clear();
            foreach (var v in installed) InstalledVersions.Add(v);

            if (!string.IsNullOrEmpty(config.ActivePhpVersion))
                await LoadExtensionsAsync(config.ActivePhpVersion);
        }
        catch { /* first run */ }
    }

    private async Task LoadExtensionsAsync(string phpVersion)
    {
        var exts = await _extensionManager.GetExtensionsAsync(phpVersion);
        Extensions.Clear();
        foreach (var ext in exts) Extensions.Add(ext);
    }

    [RelayCommand]
    private async Task SavePhpSettingsAsync()
    {
        IsBusy = true;
        try
        {
            var config = await _configManager.LoadAsync();
            config.PhpMemoryLimit = PhpMemoryLimit;
            config.PhpUploadMaxFilesize = PhpUploadMaxFilesize;
            config.PhpPostMaxSize = PhpPostMaxSize;
            config.PhpMaxExecutionTime = PhpMaxExecutionTime;
            config.PhpMaxInputTime = PhpMaxInputTime;
            config.PhpMaxFileUploads = PhpMaxFileUploads;
            config.PhpMaxInputVars = PhpMaxInputVars;
            config.PhpDisplayErrors = PhpDisplayErrors;
            config.PhpErrorReporting = PhpErrorReporting;
            config.PhpDateTimezone = PhpDateTimezone;
            await _configManager.SaveAsync(config);

            // Regenerate php.ini and restart Apache
            if (!string.IsNullOrEmpty(config.ActivePhpVersion) && config.ActivePhpVersion != "None")
            {
                await _orchestrator.SwitchPhpVersionAsync(config.ActivePhpVersion);
            }

            StatusMessage = "PHP ayarlar\u0131 kaydedildi ve uyguland\u0131";
            _toastService.ShowSuccess("PHP ayarlar\u0131 kaydedildi ve uyguland\u0131");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Kaydetme hatası: {ex.Message}";
            _dialogService.ShowError(ex.Message);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ResetPhpSettings()
    {
        PhpMemoryLimit = "256M";
        PhpUploadMaxFilesize = "128M";
        PhpPostMaxSize = "128M";
        PhpMaxExecutionTime = 300;
        PhpMaxInputTime = 300;
        PhpMaxFileUploads = 20;
        PhpMaxInputVars = 1000;
        PhpDisplayErrors = true;
        PhpErrorReporting = "E_ALL";
        PhpDateTimezone = "UTC";
        _toastService.ShowInfo("PHP ayarları varsayılana sıfırlandı. Kaydetmek için 'Kaydet' butonuna basın.");
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
                _dialogService.ShowWarning("php.ini dosyası bulunamadı. Önce bir PHP sürümü etkinleştirin.");
        }
        catch (Exception ex) { _toastService.ShowError($"Dosya açılamadı: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task FetchAvailableVersionsAsync()
    {
        IsBusy = true;
        try
        {
            var versions = await _versionManager.GetAvailableVersionsAsync(ServiceType.Php);
            var installedSet = InstalledVersions.Select(v => v.Version).ToHashSet(StringComparer.OrdinalIgnoreCase);
            AvailableVersions.Clear();
            foreach (var v in versions)
            {
                v.IsInstalled = installedSet.Contains(v.Version);
                AvailableVersions.Add(v);
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Sürümler alınamadı: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DownloadVersionAsync(ServiceVersion? version)
    {
        if (version == null) return;

        IsDownloading = true;
        DownloadStatus = $"PHP {version.Version} indiriliyor...";
        try
        {
            var progress = new Progress<DownloadProgress>(p =>
            {
                DownloadProgress = p.ProgressPercent;
                DownloadStatus = p.State switch
                {
                    DownloadState.Downloading => $"\u0130ndiriliyor... {p.ProgressPercent:F0}%",
                    DownloadState.Extracting => "\u00c7\u0131kar\u0131l\u0131yor...",
                    DownloadState.Completed => "Tamamland\u0131",
                    _ => p.State.ToString()
                };
            });

            await _versionManager.InstallVersionAsync(version, progress);
            DownloadStatus = $"PHP {version.Version} yüklendi";

            var installed = await _versionManager.GetInstalledVersionsAsync(ServiceType.Php);
            InstalledVersions.Clear();
            foreach (var v in installed) InstalledVersions.Add(v);

            // Mark available version as installed
            version.IsInstalled = true;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"\u0130ndirme ba\u015far\u0131s\u0131z: {ex.Message}");
            DownloadStatus = "\u0130ndirme ba\u015far\u0131s\u0131z";
        }
        finally
        {
            IsDownloading = false;
            DownloadProgress = 0;
        }
    }

    [RelayCommand]
    private async Task DeleteVersionAsync(VersionPointer? pointer)
    {
        if (pointer == null) return;

        if (pointer.IsActive)
        {
            _dialogService.ShowWarning("Aktif sürüm silinemez. Önce başka bir sürüme geçiş yapın.");
            return;
        }

        if (!_dialogService.Confirm($"PHP {pointer.Version} silinsin mi?", "Sürüm Silme"))
            return;

        IsBusy = true;
        try
        {
            await _versionManager.UninstallVersionAsync(ServiceType.Php, pointer.Version);

            var installed = await _versionManager.GetInstalledVersionsAsync(ServiceType.Php);
            InstalledVersions.Clear();
            foreach (var v in installed) InstalledVersions.Add(v);

            // Update available versions' IsInstalled flags
            var installedSet = InstalledVersions.Select(v => v.Version).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var av in AvailableVersions)
                av.IsInstalled = installedSet.Contains(av.Version);

            StatusMessage = $"PHP {pointer.Version} silindi";
            _toastService.ShowSuccess($"PHP {pointer.Version} silindi");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Sürüm silinemedi: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SwitchVersionAsync(VersionPointer? pointer)
    {
        if (pointer == null) return;
        if (pointer.IsActive)
        {
            StatusMessage = $"PHP {pointer.Version} zaten aktif";
            return;
        }

        IsBusy = true;
        StatusMessage = $"PHP {pointer.Version} s\u00fcr\u00fcm\u00fcne ge\u00e7iliyor...";
        try
        {
            await _orchestrator.SwitchPhpVersionAsync(pointer.Version);
            ActiveVersion = pointer.Version;
            await LoadExtensionsAsync(pointer.Version);

            // Refresh installed versions to update Active badges
            var installed = await _versionManager.GetInstalledVersionsAsync(ServiceType.Php);
            InstalledVersions.Clear();
            foreach (var v in installed) InstalledVersions.Add(v);

            StatusMessage = $"PHP {pointer.Version} s\u00fcr\u00fcm\u00fcne ge\u00e7ildi - Apache yeniden ba\u015flat\u0131ld\u0131";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Switch failed: {ex.Message}";
            _dialogService.ShowError(ex.Message);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RunComposerInstallAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();

            if (string.IsNullOrEmpty(config.ActivePhpVersion))
            {
                _toastService.ShowError("PHP yüklü değil. Önce bir PHP sürümü yükleyin.");
                return;
            }

            var phpExe = Path.GetFullPath(Path.Combine(_basePath, Defaults.BinDir, "php", config.ActivePhpVersion, "php.exe"));
            if (!File.Exists(phpExe))
            {
                _toastService.ShowError("PHP binary bulunamadı.");
                return;
            }

            var composerPhar = Path.GetFullPath(Path.Combine(_basePath, Defaults.ComposerDir, "composer.phar"));
            if (!File.Exists(composerPhar))
            {
                _toastService.ShowError("composer.phar bulunamadı. Uygulama yeniden başlatılınca otomatik indirilecek.");
                return;
            }

            var wwwRoot = Path.GetFullPath(Path.Combine(_basePath, Defaults.DocumentRoot));
            if (!Directory.Exists(wwwRoot))
                Directory.CreateDirectory(wwwRoot);

            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Composer Install - Proje Klasörü Seçin",
                InitialDirectory = wwwRoot
            };

            if (dialog.ShowDialog() != true)
                return;

            var targetDir = dialog.FolderName;

            if (!File.Exists(Path.Combine(targetDir, "composer.json")))
            {
                _toastService.ShowWarning("Seçilen klasörde composer.json bulunamadı. Bir Laravel/WordPress proje klasörü seçin.");
                return;
            }

            IsBusy = true;
            StatusMessage = "Composer install çalıştırılıyor...";

            await Task.Run(async () =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = phpExe,
                    Arguments = $"\"{composerPhar}\" install --no-interaction",
                    WorkingDirectory = targetDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var phpDir = Path.GetDirectoryName(phpExe)!;
                var existingPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                psi.Environment["PATH"] = phpDir + ";" + existingPath;

                using var process = Process.Start(psi);
                if (process == null)
                {
                    _toastService.ShowError("Composer işlemi başlatılamadı.");
                    return;
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                var combined = string.IsNullOrEmpty(error) ? output : $"{output}\n{error}";

                if (process.ExitCode == 0)
                    _toastService.ShowSuccess($"Composer install tamamlandı: {Path.GetFileName(targetDir)}");
                else
                    _dialogService.ShowError(
                        $"Composer install başarısız (çıkış kodu: {process.ExitCode}):\n\n{combined.Trim()}",
                        "Composer Hatası");
            });

            StatusMessage = "Hazır";
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Composer hatası: {ex.Message}");
            StatusMessage = "Hazır";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ToggleExtensionAsync(PhpExtension? ext)
    {
        if (ext == null || string.IsNullOrEmpty(ActiveVersion) || ActiveVersion == "None") return;

        try
        {
            if (ext.IsEnabled)
                await _extensionManager.DisableExtensionAsync(ActiveVersion, ext.Name);
            else
                await _extensionManager.EnableExtensionAsync(ActiveVersion, ext.Name);

            await LoadExtensionsAsync(ActiveVersion);
            StatusMessage = $"Eklenti {ext.Name} {(ext.IsEnabled ? "devre d\u0131\u015f\u0131 b\u0131rak\u0131ld\u0131" : "etkinle\u015ftirildi")}";
        }
        catch (Exception ex) { _dialogService.ShowError(ex.Message); }
    }
}
