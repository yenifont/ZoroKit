using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZaraGON.Application.Services;
using ZaraGON.Core.Interfaces.Infrastructure;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.UI.Services;

namespace ZaraGON.UI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly NavigationService _navigationService;
    private readonly IPortManager _portManager;
    private readonly IConfigurationManager _configManager;
    private readonly IServiceController _apacheController;
    private readonly MariaDbService _mariaDbController;
    private readonly ToastService _toastService;
    private readonly DispatcherTimer _portRefreshTimer;

    [ObservableProperty]
    private string _title = "ZaraGON";

    [ObservableProperty]
    private string _currentView = "Dashboard";

    public NavigationService Navigation => _navigationService;

    public ObservableCollection<PortStatusItem> Ports { get; } = new();

    public MainViewModel(
        NavigationService navigationService,
        IPortManager portManager,
        IConfigurationManager configManager,
        IServiceController apacheController,
        MariaDbService mariaDbController,
        ToastService toastService)
    {
        _navigationService = navigationService;
        _portManager = portManager;
        _configManager = configManager;
        _apacheController = apacheController;
        _mariaDbController = mariaDbController;
        _toastService = toastService;

        _navigationService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NavigationService.CurrentView))
                CurrentView = _navigationService.CurrentView;
        };

        _portRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _portRefreshTimer.Tick += async (_, _) => await RefreshPortsAsync();
        _portRefreshTimer.Start();

        _ = RefreshPortsAsync();
    }

    [RelayCommand]
    private void NavigateTo(string viewName)
    {
        _navigationService.NavigateTo(viewName);
    }

    [RelayCommand]
    private async Task KillPort(int port)
    {
        try
        {
            var config = await _configManager.LoadAsync();
            var result = await _portManager.KillProcessOnPortAsync(port);
            if (result)
            {
                _toastService.ShowSuccess($":{port} üzerindeki process sonlandırıldı");

                // Notify service controllers so dashboard switches update
                if (port == config.ApachePort || port == config.ApacheSslPort)
                    await _apacheController.GetStatusAsync();
                else if (port == config.MySqlPort)
                    await _mariaDbController.GetStatusAsync();
            }
            else
                _toastService.ShowError($":{port} üzerindeki process sonlandırılamadı (sistem process'i)");

            await RefreshPortsAsync();
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Process sonlandırılamadı: {ex.Message}");
        }
    }

    private async Task RefreshPortsAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();
            var ports = new (int Port, string Label)[]
            {
                (config.ApachePort, "Apache"),
                (config.MySqlPort, "MySQL"),
                (config.ApacheSslPort, "SSL")
            };

            // Initialize collection on first run
            if (Ports.Count == 0)
            {
                foreach (var (port, label) in ports)
                {
                    Ports.Add(new PortStatusItem { Port = port, Label = label });
                }
            }

            for (var i = 0; i < ports.Length; i++)
            {
                var (port, label) = ports[i];
                var item = Ports[i];

                // Update port/label if config changed
                item.Port = port;
                item.Label = label;

                var conflict = await _portManager.GetPortConflictAsync(port);
                if (conflict != null)
                {
                    item.IsInUse = true;
                    item.ProcessName = conflict.ProcessName;
                    item.ProcessId = conflict.ProcessId;
                    item.IsSystemCritical = conflict.IsSystemCritical;
                }
                else
                {
                    item.IsInUse = false;
                    item.ProcessName = null;
                    item.ProcessId = null;
                    item.IsSystemCritical = false;
                }
            }

            // Handle port count changes (config ports changed)
            while (Ports.Count > ports.Length)
                Ports.RemoveAt(Ports.Count - 1);
        }
        catch
        {
            // Port refresh is best-effort, don't disrupt UI
        }
    }

    public void StopTimer()
    {
        _portRefreshTimer.Stop();
    }
}
