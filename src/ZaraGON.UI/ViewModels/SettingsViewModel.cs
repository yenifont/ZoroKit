using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ZaraGON.Application.Services;
using ZaraGON.Core.Constants;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.UI.Services;

namespace ZaraGON.UI.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigurationManager _configManager;
    private readonly OrchestratorService _orchestrator;
    private readonly ISslCertificateManager _sslManager;
    private readonly DialogService _dialogService;
    private readonly ToastService _toastService;
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
        DialogService dialogService,
        ToastService toastService,
        string basePath)
    {
        _configManager = configManager;
        _orchestrator = orchestrator;
        _sslManager = sslManager;
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

            // Handle Windows Startup toggle
            if (oldStartup != RunOnWindowsStartup)
                ApplyWindowsStartup(RunOnWindowsStartup);

            // Handle System PATH toggle
            if (oldPath != AddToSystemPath)
                ApplySystemPath(config, AddToSystemPath);

            StatusMessage = "Ayarlar kaydedildi";
            _toastService.ShowSuccess("Ayarlar kaydedildi");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, "Kaydetme Başarısız");
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
                    key.SetValue("ZaraGON", $"\"{exePath}\" --minimized");
            }
            else
            {
                key.DeleteValue("ZaraGON", false);
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
