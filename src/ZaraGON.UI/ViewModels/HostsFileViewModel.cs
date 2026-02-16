using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;
using ZaraGON.UI.Services;

namespace ZaraGON.UI.ViewModels;

public sealed partial class HostsFileViewModel : ObservableObject
{
    private readonly IHostsFileManager _hostsFileManager;
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

    public HostsFileViewModel(IHostsFileManager hostsFileManager, DialogService dialogService)
    {
        _hostsFileManager = hostsFileManager;
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
            NewHostname = string.Empty;
            StatusMessage = $"{entry.Hostname} eklendi";
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
