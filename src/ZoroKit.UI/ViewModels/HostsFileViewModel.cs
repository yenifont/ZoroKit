using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoroKit.Core.Constants;
using ZoroKit.Core.Enums;
using ZoroKit.Core.Interfaces.Services;
using ZoroKit.Core.Models;
using ZoroKit.UI.Services;

namespace ZoroKit.UI.ViewModels;

public sealed partial class HostsFileViewModel : ObservableObject
{
    private readonly IHostsFileManager _hostsFileManager;
    private readonly IAutoVirtualHostManager _autoVHostManager;
    private readonly IServiceController _apacheController;
    private readonly DialogService _dialogService;
    private readonly ToastService _toastService;

    [ObservableProperty]
    private string _newIpAddress = "127.0.0.1";

    [ObservableProperty]
    private string _newHostname = string.Empty;

    [ObservableProperty]
    private string _newSubFolder = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _requiresElevation;

    public ObservableCollection<HostEntry> Entries { get; } = [];

    public HostsFileViewModel(
        IHostsFileManager hostsFileManager,
        IAutoVirtualHostManager autoVHostManager,
        IServiceController apacheController,
        DialogService dialogService,
        ToastService toastService)
    {
        _hostsFileManager = hostsFileManager;
        _autoVHostManager = autoVHostManager;
        _apacheController = apacheController;
        _dialogService = dialogService;
        _toastService = toastService;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            RequiresElevation = await _hostsFileManager.RequiresElevationAsync();
            var entries = await _hostsFileManager.GetManagedEntriesAsync();
            Entries.Clear();
            foreach (var entry in entries)
            {
                try
                {
                    entry.SubFolder = await _autoVHostManager.GetSubFolderForHostnameAsync(entry.Hostname);
                }
                catch { /* alt klasör bilgisi opsiyonel */ }
                Entries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private static readonly Regex ValidHostnameRegex = new(
        @"^[a-zA-Z0-9]([a-zA-Z0-9\-]*[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9\-]*[a-zA-Z0-9])?)*$",
        RegexOptions.Compiled);

    [RelayCommand]
    private async Task AddEntryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewHostname))
        {
            _dialogService.ShowWarning("Lütfen bir hostname girin.");
            return;
        }

        var trimmed = NewHostname.Trim();
        if (!ValidHostnameRegex.IsMatch(trimmed))
        {
            _dialogService.ShowWarning("Hostname yalnızca harf, rakam, tire (-) ve nokta (.) içerebilir. Boşluk ve özel karakter kullanılamaz.");
            return;
        }

        try
        {
            var entry = new HostEntry
            {
                IpAddress = NewIpAddress,
                Hostname = trimmed
            };

            await _hostsFileManager.AddEntryAsync(entry);

            try
            {
                var subFolder = string.IsNullOrWhiteSpace(NewSubFolder) ? null : NewSubFolder.Trim();
                await _autoVHostManager.EnsureVHostForHostnameAsync(entry.Hostname, subFolder);
                var status = await _apacheController.GetStatusAsync();
                if (status == ServiceStatus.Running)
                    await _apacheController.ReloadAsync();
            }
            catch (DirectoryNotFoundException ex)
            {
                // Alt klasör bulunamadı hatası - kullanıcıya göster
                _dialogService.ShowError(ex.Message, "Klasör Bulunamadı");
                return;
            }
            catch (Exception ex)
            {
                // Hosts kaydı eklendi ama vhost/reload başarısız
                _toastService.ShowWarning($"Hosts kaydı eklendi ancak VHost yapılandırması başarısız: {ex.Message}");
            }

            NewHostname = string.Empty;
            NewSubFolder = string.Empty;
            StatusMessage = $"{entry.Hostname} eklendi — tarayıcıda açılabilir";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, "Kayıt Eklenemedi");
        }
    }

    [RelayCommand]
    private async Task RemoveEntryAsync(HostEntry? entry)
    {
        if (entry == null) return;

        try
        {
            await _hostsFileManager.RemoveEntryAsync(entry.Hostname);

            try
            {
                await _autoVHostManager.RemoveVHostForHostnameAsync(entry.Hostname);
                var status = await _apacheController.GetStatusAsync();
                if (status == ServiceStatus.Running)
                    await _apacheController.ReloadAsync();
            }
            catch
            {
                /* vhost silme/reload best-effort; hosts kaydı zaten kaldırıldı */
            }

            StatusMessage = $"{entry.Hostname} kaldırıldı";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, "Kayıt Kaldırılamadı");
        }
    }

    [RelayCommand]
    private void OpenHostsFile()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = HostsFileMarkers.HostsFilePath,
                UseShellExecute = true,
                Verb = "runas" // Admin olarak aç
            };
            Process.Start(startInfo)?.Dispose();
            StatusMessage = "Hosts dosyası Notepad'de açıldı";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Hosts dosyası açılamadı: {ex.Message}", "Hata");
        }
    }
}
