using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Http;
using System.Text.RegularExpressions;
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

public partial class ComponentUpdate : ObservableObject
{
    public required ServiceType ServiceType { get; init; }
    public required string Name { get; init; }
    public required string Icon { get; init; }

    [ObservableProperty]
    private string _currentVersion = "Y\u00fckl\u00fc de\u011fil";

    [ObservableProperty]
    private string _latestVersion = string.Empty;

    [ObservableProperty]
    private bool _hasUpdate;

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private bool _isUpdating;

    [ObservableProperty]
    private double _updateProgress;

    [ObservableProperty]
    private string _updateStatus = string.Empty;

    [ObservableProperty]
    private bool _isChecked;

    public ServiceVersion? LatestServiceVersion { get; set; }
}

public sealed partial class UpdatesViewModel : ObservableObject
{
    private readonly IVersionManager _versionManager;
    private readonly IConfigurationManager _configManager;
    private readonly OrchestratorService _orchestrator;
    private readonly IServiceController _apacheController;
    private readonly MariaDbService _mariaDbController;
    private readonly IDownloadManager _downloadManager;
    private readonly IFileSystem _fileSystem;
    private readonly DialogService _dialogService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _basePath;

    private string _appDownloadUrl = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<ComponentUpdate> Components { get; } = [];

    public UpdatesViewModel(
        IVersionManager versionManager,
        IConfigurationManager configManager,
        OrchestratorService orchestrator,
        IServiceController apacheController,
        MariaDbService mariaDbController,
        IDownloadManager downloadManager,
        IFileSystem fileSystem,
        DialogService dialogService,
        IHttpClientFactory httpClientFactory,
        string basePath)
    {
        _versionManager = versionManager;
        _configManager = configManager;
        _orchestrator = orchestrator;
        _apacheController = apacheController;
        _mariaDbController = mariaDbController;
        _downloadManager = downloadManager;
        _fileSystem = fileSystem;
        _dialogService = dialogService;
        _httpClientFactory = httpClientFactory;
        _basePath = basePath;

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();

            // ZaraGON app update card (first)
            Components.Add(new ComponentUpdate
            {
                ServiceType = ServiceType.App,
                Name = "ZaraGON",
                Icon = "Application",
                CurrentVersion = Defaults.AppVersion
            });

            Components.Add(new ComponentUpdate
            {
                ServiceType = ServiceType.Apache,
                Name = "Apache HTTP Server",
                Icon = "Server",
                CurrentVersion = string.IsNullOrEmpty(config.ActiveApacheVersion) ? "Y\u00fckl\u00fc de\u011fil" : config.ActiveApacheVersion
            });

            Components.Add(new ComponentUpdate
            {
                ServiceType = ServiceType.Php,
                Name = "PHP",
                Icon = "LanguagePhp",
                CurrentVersion = string.IsNullOrEmpty(config.ActivePhpVersion) ? "Y\u00fckl\u00fc de\u011fil" : config.ActivePhpVersion
            });

            Components.Add(new ComponentUpdate
            {
                ServiceType = ServiceType.MariaDb,
                Name = "MariaDB",
                Icon = "Database",
                CurrentVersion = string.IsNullOrEmpty(config.ActiveMariaDbVersion) ? "Y\u00fckl\u00fc de\u011fil" : config.ActiveMariaDbVersion
            });

            Components.Add(new ComponentUpdate
            {
                ServiceType = ServiceType.PhpMyAdmin,
                Name = "phpMyAdmin",
                Icon = "DatabaseSearch",
                CurrentVersion = DetectPhpMyAdminVersion()
            });
        }
        catch { /* first run - no config yet */ }
    }

    private string DetectPhpMyAdminVersion()
    {
        var pmaPath = Path.GetFullPath(Path.Combine(_basePath, "apps", "phpmyadmin"));
        if (!_fileSystem.DirectoryExists(pmaPath))
            return "Y\u00fckl\u00fc de\u011fil";

        // Check RELEASE-DATE-{version} file
        try
        {
            var files = _fileSystem.GetFiles(pmaPath);
            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith("RELEASE-DATE-", StringComparison.OrdinalIgnoreCase))
                    return name["RELEASE-DATE-".Length..];
            }
        }
        catch { /* ignore */ }

        // Fallback: phpMyAdmin is installed but version unknown
        return "Unknown";
    }

    [RelayCommand]
    private async Task CheckAllUpdatesAsync()
    {
        IsBusy = true;
        StatusMessage = "G\u00fcncellemeler kontrol ediliyor...";
        try
        {
            foreach (var component in Components)
            {
                await CheckComponentUpdateAsync(component);
            }

            var updateCount = Components.Count(c => c.HasUpdate);
            StatusMessage = updateCount > 0
                ? $"{updateCount} g\u00fcncelleme mevcut"
                : "T\u00fcm bile\u015fenler g\u00fcncel";
        }
        catch (Exception ex)
        {
            StatusMessage = $"G\u00fcncelleme kontrol hatas\u0131: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private async Task CheckComponentUpdateAsync(ComponentUpdate component)
    {
        if (component.CurrentVersion == "Y\u00fckl\u00fc de\u011fil")
        {
            component.IsChecked = true;
            component.UpdateStatus = "Y\u00fckl\u00fc de\u011fil";
            return;
        }

        // ZaraGON app update uses GitHub Releases API
        if (component.ServiceType == ServiceType.App)
        {
            await CheckAppUpdateAsync(component);
            return;
        }

        component.IsChecking = true;
        component.UpdateStatus = "Kontrol ediliyor...";
        try
        {
            var available = await _versionManager.GetAvailableVersionsAsync(component.ServiceType);
            if (available.Count > 0)
            {
                var latest = available[0];
                component.LatestVersion = latest.Version;
                component.LatestServiceVersion = latest;

                var currentForCompare = component.CurrentVersion == "Unknown" ? "0.0.0" : component.CurrentVersion;

                if (IsNewerVersion(latest.Version, currentForCompare))
                {
                    component.HasUpdate = true;
                    component.UpdateStatus = "G\u00fcncelleme mevcut";
                }
                else
                {
                    component.HasUpdate = false;
                    component.UpdateStatus = "G\u00fcncel";
                }
            }
            else
            {
                component.UpdateStatus = "S\u00fcr\u00fcmler al\u0131namad\u0131";
            }

            component.IsChecked = true;
        }
        catch (Exception ex)
        {
            component.UpdateStatus = $"Error: {ex.Message}";
        }
        finally { component.IsChecking = false; }
    }

    private async Task CheckAppUpdateAsync(ComponentUpdate component)
    {
        component.IsChecking = true;
        component.UpdateStatus = "GitHub kontrol ediliyor...";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var githubClient = _httpClientFactory.CreateClient("GitHub");
            var request = new HttpRequestMessage(HttpMethod.Get, Defaults.GitHubReleasesApi);
            var response = await githubClient.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                component.UpdateStatus = $"GitHub'a erişilemedi (HTTP {(int)response.StatusCode}). İnternet veya güvenlik duvarını kontrol edin.";
                component.IsChecked = true;
                return;
            }

            var json = await response.Content.ReadAsStringAsync(cts.Token);

            var tagMatch = Regex.Match(json, @"""tag_name""\s*:\s*""([^""]+)""");
            if (!tagMatch.Success)
            {
                component.UpdateStatus = "Sürüm bilgisi alınamadı";
                component.IsChecked = true;
                return;
            }

            var latestTag = tagMatch.Groups[1].Value.TrimStart('v', 'V');
            component.LatestVersion = latestTag;

            // Extract download URL for .exe asset
            var assetMatch = Regex.Match(json, @"""browser_download_url""\s*:\s*""([^""]+\.exe)""");
            if (assetMatch.Success)
                _appDownloadUrl = assetMatch.Groups[1].Value;

            if (IsNewerVersion(latestTag, Defaults.AppVersion))
            {
                component.HasUpdate = true;
                component.UpdateStatus = "Güncelleme mevcut";
            }
            else
            {
                component.HasUpdate = false;
                component.UpdateStatus = "Güncel";
            }

            component.IsChecked = true;
        }
        catch (Exception ex)
        {
            component.UpdateStatus = $"Bağlantı hatası: {ex.Message}";
        }
        finally { component.IsChecking = false; }
    }

    [RelayCommand]
    private async Task UpdateComponentAsync(ComponentUpdate? component)
    {
        if (component == null) return;

        // ZaraGON app update — open GitHub releases page or download URL
        if (component.ServiceType == ServiceType.App)
        {
            try
            {
                var url = !string.IsNullOrEmpty(_appDownloadUrl)
                    ? _appDownloadUrl
                    : $"https://github.com/{Defaults.GitHubOwner}/{Defaults.GitHubRepo}/releases/latest";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"İndirme sayfası açılamadı: {ex.Message}");
            }
            return;
        }

        if (component.LatestServiceVersion == null) return;

        component.IsUpdating = true;
        component.UpdateProgress = 0;
        component.UpdateStatus = $"{component.Name} {component.LatestVersion} indiriliyor...";
        try
        {
            if (component.ServiceType == ServiceType.PhpMyAdmin)
            {
                await UpdatePhpMyAdminAsync(component);
            }
            else
            {
                await UpdateStandardComponentAsync(component);
            }

            component.CurrentVersion = component.LatestVersion;
            component.HasUpdate = false;
            component.UpdateStatus = "Ba\u015far\u0131yla g\u00fcncellendi";
            component.UpdateProgress = 100;
        }
        catch (Exception ex)
        {
            component.UpdateStatus = $"G\u00fcncelleme ba\u015far\u0131s\u0131z: {ex.Message}";
            _dialogService.ShowError($"{component.Name} g\u00fcncellenemedi: {ex.Message}");
        }
        finally
        {
            component.IsUpdating = false;
        }
    }

    private async Task UpdateStandardComponentAsync(ComponentUpdate component)
    {
        var progress = new Progress<DownloadProgress>(p =>
        {
            component.UpdateProgress = p.ProgressPercent;
            component.UpdateStatus = p.State switch
            {
                DownloadState.Downloading => $"\u0130ndiriliyor... {p.ProgressPercent:F0}%",
                DownloadState.Extracting => "\u00c7\u0131kar\u0131l\u0131yor...",
                DownloadState.Completed => "\u0130ndirme tamamland\u0131",
                _ => p.State.ToString()
            };
        });

        await _versionManager.InstallVersionAsync(component.LatestServiceVersion!, progress);

        component.UpdateStatus = "Aktif s\u00fcr\u00fcm de\u011fi\u015ftiriliyor...";
        var version = component.LatestServiceVersion!.Version;

        if (component.ServiceType == ServiceType.Php)
        {
            await _orchestrator.SwitchPhpVersionAsync(version);
        }
        else
        {
            await _versionManager.SetActiveVersionAsync(component.ServiceType, version);

            var config = await _configManager.LoadAsync();
            switch (component.ServiceType)
            {
                case ServiceType.Apache:
                    config.ActiveApacheVersion = version;
                    break;
                case ServiceType.MariaDb:
                    config.ActiveMariaDbVersion = version;
                    break;
            }
            await _configManager.SaveAsync(config);

            component.UpdateStatus = "Servis yeniden ba\u015flat\u0131l\u0131yor...";
            await RestartServiceIfRunningAsync(component.ServiceType);
        }
    }

    private async Task UpdatePhpMyAdminAsync(ComponentUpdate component)
    {
        var version = component.LatestServiceVersion!.Version;
        var pmaPath = Path.GetFullPath(Path.Combine(_basePath, "apps", "phpmyadmin"));
        var tempDir = Path.Combine(_basePath, "temp");
        _fileSystem.CreateDirectory(tempDir);

        // Backup config.inc.php
        var configPath = Path.Combine(pmaPath, "config.inc.php");
        string? configBackup = null;
        if (_fileSystem.FileExists(configPath))
            configBackup = await _fileSystem.ReadAllTextAsync(configPath);

        // Download
        var progress = new Progress<DownloadProgress>(p =>
        {
            component.UpdateProgress = p.ProgressPercent;
            component.UpdateStatus = p.State switch
            {
                DownloadState.Downloading => $"\u0130ndiriliyor... {p.ProgressPercent:F0}%",
                DownloadState.Extracting => "\u00c7\u0131kar\u0131l\u0131yor...",
                DownloadState.Completed => "\u0130ndirme tamamland\u0131",
                _ => p.State.ToString()
            };
        });

        var archivePath = await _downloadManager.DownloadFileAsync(
            component.LatestServiceVersion!.DownloadUrl, tempDir, progress);

        // Extract to temp location
        var extractDir = Path.Combine(tempDir, "phpmyadmin-update");
        if (_fileSystem.DirectoryExists(extractDir))
            _fileSystem.DeleteDirectory(extractDir, true);

        await _downloadManager.ExtractArchiveAsync(archivePath, extractDir, progress);
        _fileSystem.DeleteFile(archivePath);

        // Find the extracted subfolder (phpMyAdmin-{version}-all-languages)
        var extractedDirs = _fileSystem.GetDirectories(extractDir);
        var sourceDir = extractedDirs.Length > 0 ? extractedDirs[0] : extractDir;

        // Remove old installation
        component.UpdateStatus = "Eski s\u00fcr\u00fcm de\u011fi\u015ftiriliyor...";
        if (_fileSystem.DirectoryExists(pmaPath))
            _fileSystem.DeleteDirectory(pmaPath, true);

        // Move new version into place
        Directory.Move(sourceDir, pmaPath);

        // Restore config.inc.php
        if (configBackup != null)
        {
            // Ensure error_reporting is present
            if (!configBackup.Contains("error_reporting"))
                configBackup = configBackup.Replace("<?php", "<?php\nerror_reporting(E_ALL & ~E_DEPRECATED);");

            await _fileSystem.AtomicWriteAsync(Path.Combine(pmaPath, "config.inc.php"), configBackup);
        }

        // Ensure tmp directory exists
        _fileSystem.CreateDirectory(Path.Combine(pmaPath, "tmp"));

        // Cleanup
        if (_fileSystem.DirectoryExists(extractDir))
            _fileSystem.DeleteDirectory(extractDir, true);
    }

    private async Task RestartServiceIfRunningAsync(ServiceType serviceType)
    {
        try
        {
            switch (serviceType)
            {
                case ServiceType.Apache:
                    var apacheStatus = await _apacheController.GetStatusAsync();
                    if (apacheStatus == ServiceStatus.Running)
                        await _apacheController.RestartAsync();
                    break;
                case ServiceType.MariaDb:
                    var mariaStatus = await _mariaDbController.GetStatusAsync();
                    if (mariaStatus == ServiceStatus.Running)
                        await _mariaDbController.RestartAsync();
                    break;
            }
        }
        catch { /* restart is best-effort */ }
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        var latestClean = latest.TrimStart('v', 'V');
        var currentClean = current.TrimStart('v', 'V');

        if (Version.TryParse(NormalizeVersion(latestClean), out var latestVer) &&
            Version.TryParse(NormalizeVersion(currentClean), out var currentVer))
        {
            return latestVer > currentVer;
        }

        return string.Compare(latestClean, currentClean, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static string NormalizeVersion(string version)
    {
        var parts = version.Split('.');
        return parts.Length switch
        {
            1 => $"{parts[0]}.0",
            _ => version
        };
    }
}
