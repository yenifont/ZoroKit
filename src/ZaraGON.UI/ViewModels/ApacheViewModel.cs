using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZaraGON.Core.Constants;
using ZaraGON.Core.Enums;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;
using ZaraGON.UI.Services;

namespace ZaraGON.UI.ViewModels;

public sealed partial class ApacheViewModel : ObservableObject
{
    private readonly IServiceController _apacheController;
    private readonly IVersionManager _versionManager;
    private readonly IConfigurationManager _configManager;
    private readonly IPortManager _portManager;
    private readonly DialogService _dialogService;
    private readonly string _basePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(CanRestart))]
    private ServiceStatus _status = ServiceStatus.Stopped;

    [ObservableProperty]
    private string _activeVersion = "None";

    [ObservableProperty]
    private int _port = 80;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(CanRestart))]
    private bool _isBusy;

    public bool CanStart => Status != ServiceStatus.Running && !IsBusy;
    public bool CanStop => Status == ServiceStatus.Running && !IsBusy;
    public bool CanRestart => Status == ServiceStatus.Running && !IsBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _configValidationResult = string.Empty;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStatus = string.Empty;

    public ObservableCollection<ServiceVersion> AvailableVersions { get; } = [];
    public ObservableCollection<VersionPointer> InstalledVersions { get; } = [];

    public ApacheViewModel(
        IServiceController apacheController,
        IVersionManager versionManager,
        IConfigurationManager configManager,
        IPortManager portManager,
        DialogService dialogService,
        string basePath)
    {
        _apacheController = apacheController;
        _versionManager = versionManager;
        _configManager = configManager;
        _portManager = portManager;
        _dialogService = dialogService;
        _basePath = basePath;

        _apacheController.StatusChanged += (_, status) => Status = status;

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();
            Port = config.ApachePort;
            ActiveVersion = string.IsNullOrEmpty(config.ActiveApacheVersion) ? "None" : config.ActiveApacheVersion;
            Status = await _apacheController.GetStatusAsync();

            var installed = await _versionManager.GetInstalledVersionsAsync(ServiceType.Apache);
            InstalledVersions.Clear();
            foreach (var v in installed)
                InstalledVersions.Add(v);
        }
        catch { /* first run */ }
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        IsBusy = true;
        try
        {
            await _apacheController.StartAsync();
            StatusMessage = "Apache ba\u015far\u0131yla ba\u015flat\u0131ld\u0131";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, "Apache Ba\u015flat\u0131lamad\u0131");
            StatusMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        IsBusy = true;
        try
        {
            await _apacheController.StopAsync();
            StatusMessage = "Apache durduruldu";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _dialogService.ShowError($"Apache durdurulamadı: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RestartAsync()
    {
        IsBusy = true;
        try
        {
            await _apacheController.RestartAsync();
            StatusMessage = "Apache yeniden ba\u015flat\u0131ld\u0131";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _dialogService.ShowError($"Apache yeniden başlatılamadı: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ValidateConfigAsync()
    {
        var valid = await _apacheController.ValidateConfigAsync();
        ConfigValidationResult = valid ? "Yap\u0131land\u0131rma ge\u00e7erli (Syntax OK)" : "Yap\u0131land\u0131rmada hatalar var";
    }

    [RelayCommand]
    private async Task FetchAvailableVersionsAsync()
    {
        IsBusy = true;
        try
        {
            var versions = await _versionManager.GetAvailableVersionsAsync(ServiceType.Apache);
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
        DownloadStatus = $"{version.Version} indiriliyor...";
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
            DownloadStatus = $"Apache {version.Version} yüklendi";

            // Refresh installed list
            var installed = await _versionManager.GetInstalledVersionsAsync(ServiceType.Apache);
            InstalledVersions.Clear();
            foreach (var v in installed)
                InstalledVersions.Add(v);

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
    private async Task SetActiveVersionAsync(VersionPointer? pointer)
    {
        if (pointer == null) return;

        try
        {
            await _versionManager.SetActiveVersionAsync(ServiceType.Apache, pointer.Version);
            var config = await _configManager.LoadAsync();
            config.ActiveApacheVersion = pointer.Version;
            await _configManager.SaveAsync(config);
            ActiveVersion = pointer.Version;

            await LoadAsync();
            StatusMessage = $"Apache {pointer.Version} s\u00fcr\u00fcm\u00fcne ge\u00e7ildi";
        }
        catch (Exception ex) { _dialogService.ShowError(ex.Message); }
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

        if (!_dialogService.Confirm($"Apache {pointer.Version} silinsin mi?", "Sürüm Silme"))
            return;

        IsBusy = true;
        try
        {
            await _versionManager.UninstallVersionAsync(ServiceType.Apache, pointer.Version);

            var installed = await _versionManager.GetInstalledVersionsAsync(ServiceType.Apache);
            InstalledVersions.Clear();
            foreach (var v in installed)
                InstalledVersions.Add(v);

            // Update available versions' IsInstalled flags
            var installedSet = InstalledVersions.Select(v => v.Version).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var av in AvailableVersions)
                av.IsInstalled = installedSet.Contains(av.Version);

            StatusMessage = $"Apache {pointer.Version} silindi";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Sürüm silinemedi: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SavePortAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();
            config.ApachePort = Port;
            await _configManager.SaveAsync(config);
            await _apacheController.RegenerateConfigAsync();
            StatusMessage = $"Port {Port} olarak de\u011fi\u015ftirildi";
        }
        catch (Exception ex) { _dialogService.ShowError(ex.Message); }
    }

    [RelayCommand]
    private void OpenHttpdConf()
    {
        try
        {
            var confPath = Path.GetFullPath(Path.Combine(_basePath, Defaults.ApacheConfigDir, "httpd.conf"));
            if (File.Exists(confPath))
                Process.Start(new ProcessStartInfo(confPath) { UseShellExecute = true })?.Dispose();
            else
                _dialogService.ShowWarning("httpd.conf dosyası bulunamadı. Önce Apache'yi başlatın.");
        }
        catch (Exception ex) { _dialogService.ShowError($"Dosya açılamadı: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task CheckPortAsync()
    {
        var conflict = await _portManager.GetPortConflictAsync(Port);
        if (conflict == null)
        {
            StatusMessage = $"Port {Port} kullan\u0131labilir";
        }
        else
        {
            StatusMessage = $"Port {Port}, {conflict.ProcessName} taraf\u0131ndan kullan\u0131l\u0131yor (PID: {conflict.ProcessId})";
            if (!conflict.IsSystemCritical)
            {
                if (_dialogService.Confirm($"Port {Port}, {conflict.ProcessName} taraf\u0131ndan kullan\u0131l\u0131yor. Sonland\u0131r\u0131ls\u0131n m\u0131?", "Port \u00c7ak\u0131\u015fmas\u0131"))
                {
                    await _portManager.KillProcessOnPortAsync(Port);
                    StatusMessage = $"\u0130\u015flem sonland\u0131r\u0131ld\u0131. Port {Port} art\u0131k kullan\u0131labilir.";
                }
            }
            else
            {
                _dialogService.ShowWarning($"Port {Port}, sistem i\u015flemi '{conflict.ProcessName}' taraf\u0131ndan kullan\u0131l\u0131yor. Sonland\u0131r\u0131lamaz.");
            }
        }
    }
}
