using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZaraGON.Core.Enums;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;
using ZaraGON.UI.Services;

namespace ZaraGON.UI.ViewModels;

public sealed partial class HostsFileViewModel : ObservableObject
{
    private readonly IHostsFileManager _hostsFileManager;
    private readonly IAutoVirtualHostManager _autoVHostManager;
    private readonly IServiceController _apacheController;
    private readonly DialogService _dialogService;

    [ObservableProperty]
    private string _newIpAddress = "127.0.0.1";

    [ObservableProperty]
    private string _newHostname = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _requiresElevation;

    public ObservableCollection<HostEntry> Entries { get; } = [];

    public HostsFileViewModel(
        IHostsFileManager hostsFileManager,
        IAutoVirtualHostManager autoVHostManager,
        IServiceController apacheController,
        DialogService dialogService)
    {
        _hostsFileManager = hostsFileManager;
        _autoVHostManager = autoVHostManager;
        _apacheController = apacheController;
        _dialogService = dialogService;
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
                Entries.Add(entry);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AddEntryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewHostname))
        {
            _dialogService.ShowWarning("Lütfen bir hostname girin.");
            return;
        }

        try
        {
            var entry = new HostEntry
            {
                IpAddress = NewIpAddress,
                Hostname = NewHostname.Trim()
            };

            await _hostsFileManager.AddEntryAsync(entry);

            try
            {
                await _autoVHostManager.EnsureVHostForHostnameAsync(entry.Hostname);
                var status = await _apacheController.GetStatusAsync();
                if (status == ServiceStatus.Running)
                    await _apacheController.ReloadAsync();
            }
            catch
            {
                /* vhost/reload best-effort; hosts kaydı zaten eklendi */
            }

            NewHostname = string.Empty;
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
            StatusMessage = $"{entry.Hostname} kaldırıldı";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, "Kayıt Kaldırılamadı");
        }
    }
}
