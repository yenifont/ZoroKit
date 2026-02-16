using CommunityToolkit.Mvvm.ComponentModel;

namespace ZaraGON.UI.ViewModels;

public sealed partial class PortStatusItem : ObservableObject
{
    [ObservableProperty]
    private int _port;

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private bool _isInUse;

    [ObservableProperty]
    private string? _processName;

    [ObservableProperty]
    private int? _processId;

    [ObservableProperty]
    private bool _isSystemCritical;

    public bool CanKill => IsInUse && !IsSystemCritical;

    partial void OnIsInUseChanged(bool value) => OnPropertyChanged(nameof(CanKill));
    partial void OnIsSystemCriticalChanged(bool value) => OnPropertyChanged(nameof(CanKill));
}
