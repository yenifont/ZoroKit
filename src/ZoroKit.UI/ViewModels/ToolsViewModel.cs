using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoroKit.Application.Services;
using ZoroKit.Core.Constants;
using ZoroKit.Core.Enums;
using ZoroKit.Core.Interfaces.Services;
using ZoroKit.UI.Services;
using ZoroKit.UI.Views;

namespace ZoroKit.UI.ViewModels;

public sealed partial class ToolsViewModel : ObservableObject
{
    private readonly IConfigurationManager _configManager;
    private readonly MariaDbService _mariaDbController;
    private readonly IVersionManager _versionManager;
    private readonly DialogService _dialogService;
    private readonly ToastService _toastService;
    private readonly string _basePath;

    // MariaDB Settings
    [ObservableProperty]
    private string _mariaDbInnodbBufferPoolSize = "128M";

    [ObservableProperty]
    private int _mariaDbMaxConnections = 151;

    [ObservableProperty]
    private string _mariaDbMaxAllowedPacket = "16M";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Hazır";

    [ObservableProperty]
    private ServiceStatus _mariaDbStatus = ServiceStatus.Stopped;

    public ToolsViewModel(
        IConfigurationManager configManager,
        MariaDbService mariaDbController,
        IVersionManager versionManager,
        DialogService dialogService,
        ToastService toastService,
        string basePath)
    {
        _configManager = configManager;
        _mariaDbController = mariaDbController;
        _versionManager = versionManager;
        _dialogService = dialogService;
        _toastService = toastService;
        _basePath = basePath;

        _mariaDbController.StatusChanged += (_, status) => MariaDbStatus = status;

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var config = await _configManager.LoadAsync();
            MariaDbInnodbBufferPoolSize = config.MariaDbInnodbBufferPoolSize;
            MariaDbMaxConnections = config.MariaDbMaxConnections;
            MariaDbMaxAllowedPacket = config.MariaDbMaxAllowedPacket;
            MariaDbStatus = await _mariaDbController.GetStatusAsync();
        }
        catch { /* first run */ }
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

            StatusMessage = "MariaDB ayarları kaydedildi";
            _toastService.ShowSuccess("MariaDB ayarları kaydedildi");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Kaydetme hatası: {ex.Message}";
            _toastService.ShowError($"Kaydetme hatası: {ex.Message}");
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
    private async Task CreateDatabaseAsync()
    {
        var dbName = _dialogService.PromptInput("Veritabanı adı girin:", "Veritabanı Oluştur");
        if (string.IsNullOrWhiteSpace(dbName)) return;

        // Sanitize: only allow alphanumeric + underscore
        if (!System.Text.RegularExpressions.Regex.IsMatch(dbName, @"^[a-zA-Z0-9_]+$"))
        {
            _toastService.ShowWarning("Veritabanı adı yalnızca harf, rakam ve alt çizgi içerebilir");
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
                _toastService.ShowError("mysql.exe bulunamadı");
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
                    _toastService.ShowSuccess($"Veritabanı '{dbName}' oluşturuldu");
                    StatusMessage = $"Veritabanı '{dbName}' oluşturuldu";
                }
                else
                {
                    _toastService.ShowError($"Veritabanı oluşturulamadı: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Veritabanı oluşturma hatası: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private async Task<string?> GetMariaDbBinDirAsync()
    {
        var config = await _configManager.LoadAsync();
        if (string.IsNullOrEmpty(config.ActiveMariaDbVersion)) return null;
        return Path.GetFullPath(Path.Combine(_basePath, Defaults.BinDir, "mariadb", config.ActiveMariaDbVersion, "bin"));
    }

    private async Task<List<string>> GetDatabaseListAsync()
    {
        var mariaDir = await GetMariaDbBinDirAsync();
        if (mariaDir == null) return [];

        var mysqlExe = Path.Combine(mariaDir, "mysql.exe");
        if (!File.Exists(mysqlExe)) return [];

        var config = await _configManager.LoadAsync();
        var psi = new ProcessStartInfo
        {
            FileName = mysqlExe,
            Arguments = $"-u root -P {config.MySqlPort} -e \"SHOW DATABASES\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return [];

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0) return [];

        var systemDbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "information_schema", "mysql", "performance_schema", "sys"
        };

        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(1) // skip "Database" header
            .Where(db => !systemDbs.Contains(db))
            .ToList();
    }

    [RelayCommand]
    private async Task BackupDatabaseAsync()
    {
        if (MariaDbStatus != ServiceStatus.Running)
        {
            _toastService.ShowWarning("Yedekleme için önce MariaDB'yi başlatın");
            return;
        }

        IsBusy = true;
        try
        {
            var mariaDir = await GetMariaDbBinDirAsync();
            if (mariaDir == null)
            {
                _toastService.ShowError("MariaDB yüklü değil");
                return;
            }

            var dumpExe = Path.Combine(mariaDir, "mysqldump.exe");
            if (!File.Exists(dumpExe))
            {
                _toastService.ShowError("mysqldump.exe bulunamadı");
                return;
            }

            StatusMessage = "Veritabanı listesi alınıyor...";
            var databases = await GetDatabaseListAsync();
            if (databases.Count == 0)
            {
                _toastService.ShowWarning("Yedeklenecek veritabanı bulunamadı");
                StatusMessage = "Hazır";
                return;
            }

            // Show multi-select dialog
            DatabaseSelectionDialog? dialog = null;
            var confirmed = false;
            List<string> selected = [];

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                dialog = DatabaseSelectionDialog.CreateForBackup(databases);
                dialog.ShowDialog();
                confirmed = dialog.DialogConfirmed;
                selected = [.. dialog.SelectedDatabases];
            });

            if (!confirmed || selected.Count == 0)
            {
                StatusMessage = "Hazır";
                return;
            }

            var config = await _configManager.LoadAsync();
            var backupDir = Path.GetFullPath(Path.Combine(_basePath, "backups"));
            Directory.CreateDirectory(backupDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var successCount = 0;

            foreach (var dbName in selected)
            {
                StatusMessage = $"Yedekleniyor: {dbName}...";

                var backupFile = Path.Combine(backupDir, $"{dbName}_{timestamp}.sql");
                var psi = new ProcessStartInfo
                {
                    FileName = dumpExe,
                    Arguments = $"-u root -P {config.MySqlPort} \"{dbName}\"",
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
                        successCount++;
                    }
                    else
                    {
                        _toastService.ShowError($"'{dbName}' yedeklenemedi: {error}");
                    }
                }
            }

            if (successCount > 0)
            {
                _toastService.ShowSuccess($"{successCount} veritabanı yedeklendi (backups/ klasörüne)");
                StatusMessage = $"{successCount} veritabanı yedeklendi";
            }
            else
            {
                StatusMessage = "Yedekleme başarısız";
            }
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Yedekleme hatası: {ex.Message}");
            StatusMessage = $"Yedekleme hatası: {ex.Message}";
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

            if (MariaDbStatus != ServiceStatus.Running)
            {
                _toastService.ShowWarning("İçe aktarma için önce MariaDB'yi başlatın");
                return;
            }

            IsBusy = true;
            StatusMessage = "Veritabanı listesi alınıyor...";

            var config = await _configManager.LoadAsync();
            var mariaDir = await GetMariaDbBinDirAsync();
            if (mariaDir == null)
            {
                _toastService.ShowError("MariaDB yüklü değil");
                return;
            }

            var mysqlExe = Path.Combine(mariaDir, "mysql.exe");
            if (!File.Exists(mysqlExe))
            {
                _toastService.ShowError("mysql.exe bulunamadı");
                return;
            }

            var databases = await GetDatabaseListAsync();

            // Show single-select dialog
            DatabaseSelectionDialog? dialog = null;
            var confirmed = false;
            var isNewDb = false;
            string? selectedDb = null;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                dialog = DatabaseSelectionDialog.CreateForImport(databases);
                dialog.ShowDialog();
                confirmed = dialog.DialogConfirmed;
                isNewDb = dialog.IsNewDatabase;
                selectedDb = dialog.SelectedDatabases.FirstOrDefault();
            });

            if (!confirmed)
            {
                StatusMessage = "Hazır";
                return;
            }

            string dbName;

            if (isNewDb)
            {
                // Prompt for new database name
                var newName = _dialogService.PromptInput("Yeni veritabanı adını girin:", "Yeni Veritabanı");
                if (string.IsNullOrWhiteSpace(newName))
                {
                    StatusMessage = "Hazır";
                    return;
                }

                if (!System.Text.RegularExpressions.Regex.IsMatch(newName, @"^[a-zA-Z0-9_]+$"))
                {
                    _toastService.ShowWarning("Veritabanı adı yalnızca harf, rakam ve alt çizgi içerebilir");
                    StatusMessage = "Hazır";
                    return;
                }

                dbName = newName;

                // Create the database
                StatusMessage = $"Veritabanı '{dbName}' oluşturuluyor...";
                var createPsi = new ProcessStartInfo
                {
                    FileName = mysqlExe,
                    Arguments = $"-u root -P {config.MySqlPort} -e \"CREATE DATABASE IF NOT EXISTS `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var createProcess = Process.Start(createPsi);
                if (createProcess != null)
                {
                    var createError = await createProcess.StandardError.ReadToEndAsync();
                    await createProcess.WaitForExitAsync();
                    if (createProcess.ExitCode != 0)
                    {
                        _toastService.ShowError($"Veritabanı oluşturulamadı: {createError}");
                        StatusMessage = "Veritabanı oluşturulamadı";
                        return;
                    }
                }
            }
            else
            {
                dbName = selectedDb!;
            }

            StatusMessage = $"'{dbName}' veritabanına içe aktarılıyor...";

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C \"\"{mysqlExe}\" -u root -P {config.MySqlPort} \"{dbName}\" < \"{sqlFile}\"\"",
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
                    _toastService.ShowSuccess($"İçe aktarma tamamlandı: '{dbName}'");
                    StatusMessage = $"İçe aktarma tamamlandı: '{dbName}'";
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
}
