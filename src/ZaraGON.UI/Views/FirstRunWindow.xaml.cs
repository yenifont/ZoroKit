using System.Diagnostics;
using System.Windows;
using ZaraGON.Core.Enums;
using ZaraGON.Core.Interfaces.Infrastructure;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;

namespace ZaraGON.UI.Views;

public partial class FirstRunWindow : Window
{
    private readonly IVersionManager _versionManager;
    private readonly IConfigurationManager _configManager;
    private readonly IVcRedistChecker _vcRedistChecker;

    public bool SetupCompleted { get; private set; }

    public FirstRunWindow(IVersionManager versionManager, IConfigurationManager configManager, IVcRedistChecker vcRedistChecker)
    {
        InitializeComponent();
        _versionManager = versionManager;
        _configManager = configManager;
        _vcRedistChecker = vcRedistChecker;

        Loaded += async (_, _) => await RunSetupAsync();
    }

    private async Task RunSetupAsync()
    {
        try
        {
            ErrorButtons.Visibility = Visibility.Collapsed;

            // Kill any running services that might lock files during extraction
            StatusText.Text = "Mevcut servisler durduruluyor...";
            DetailText.Text = "";
            await Task.Run(() => KillRunningServices());

            StatusText.Text = "Mevcut surumler kontrol ediliyor...";
            DetailText.Text = "Indirme sunucularina baglaniliyor...";

            // Fetch all available versions in parallel
            IReadOnlyList<ServiceVersion> apacheVersions;
            IReadOnlyList<ServiceVersion> phpVersions;
            IReadOnlyList<ServiceVersion> mariaDbVersions;

            try
            {
                var apacheTask = _versionManager.GetAvailableVersionsAsync(ServiceType.Apache);
                var phpTask = _versionManager.GetAvailableVersionsAsync(ServiceType.Php);
                var mariaDbTask = _versionManager.GetAvailableVersionsAsync(ServiceType.MariaDb);

                await Task.WhenAll(apacheTask, phpTask, mariaDbTask);

                apacheVersions = await apacheTask;
                phpVersions = await phpTask;
                mariaDbVersions = await mariaDbTask;
            }
            catch (Exception ex)
            {
                ShowError($"Surum bilgileri alinamadi: {ex.Message}");
                return;
            }

            if (apacheVersions.Count == 0)
            {
                ShowError("Apache surumu bulunamadi. Indirme sayfasi formati degismis olabilir.");
                return;
            }

            if (phpVersions.Count == 0)
            {
                ShowError("PHP surumu bulunamadi. Indirme sayfasi formati degismis olabilir.");
                return;
            }

            if (mariaDbVersions.Count == 0)
            {
                ShowError("MariaDB surumu bulunamadi. REST API degismis olabilir.");
                return;
            }

            // Pick latest versions
            var apacheVersion = apacheVersions[0];
            var phpVersion = phpVersions[0];
            var mariaDbVersion = mariaDbVersions[0];

            // Download Apache
            StatusText.Text = "Apache indiriliyor...";
            ApacheLabel.Text = $"Apache {apacheVersion.Version}";
            DetailText.Text = "";

            var apacheProgress = new Progress<DownloadProgress>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    ApacheProgress.Value = p.ProgressPercent;
                    ApachePercent.Text = p.State switch
                    {
                        DownloadState.Downloading => $"{p.ProgressPercent:F0}%",
                        DownloadState.Extracting => "Cikartiliyor",
                        DownloadState.Completed => "Tamam",
                        _ => ""
                    };
                    DetailText.Text = p.State switch
                    {
                        DownloadState.Downloading => $"Indiriliyor... {p.BytesReceived / 1024 / 1024}MB",
                        DownloadState.Extracting => "Dosyalar cikartiliyor...",
                        _ => ""
                    };
                });
            });

            await _versionManager.InstallVersionAsync(apacheVersion, apacheProgress);
            ApacheProgress.Value = 100;
            ApachePercent.Text = "Tamam";

            // Set Apache as active
            await _versionManager.SetActiveVersionAsync(ServiceType.Apache, apacheVersion.Version);
            var config = await _configManager.LoadAsync();
            config.ActiveApacheVersion = apacheVersion.Version;

            // Check VC++ Runtime before PHP download
            VcRedistRow.Visibility = Visibility.Visible;
            StatusText.Text = "VC++ Runtime kontrol ediliyor...";
            DetailText.Text = "";

            try
            {
                var vcStatus = await _vcRedistChecker.CheckCompatibilityAsync(phpVersion.VsVersion);
                if (!vcStatus.IsCompatible)
                {
                    StatusText.Text = "VC++ Runtime indiriliyor...";
                    VcRedistLabel.Text = "VC++ Runtime";
                    DetailText.Text = "PHP için gerekli VC++ Redistributable kuruluyor...";

                    var vcProgress = new Progress<double>(p =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            VcRedistProgress.Value = p;
                            VcRedistPercent.Text = p < 80 ? $"{p:F0}%" : p < 100 ? "Kuruluyor" : "Tamam";
                        });
                    });

                    await _vcRedistChecker.InstallAsync(vcProgress);
                    VcRedistProgress.Value = 100;
                    VcRedistPercent.Text = "Tamam";
                }
                else
                {
                    VcRedistProgress.Value = 100;
                    VcRedistPercent.Text = "Mevcut";
                }
            }
            catch (Exception ex)
            {
                VcRedistPercent.Text = "Atlandı";
                DetailText.Text = $"VC++ Runtime kurulamadı: {ex.Message}";
                // Continue anyway — user may already have it or can install later
                await Task.Delay(2000);
            }

            // Download PHP
            StatusText.Text = "PHP indiriliyor...";
            PhpLabel.Text = $"PHP {phpVersion.Version}";
            DetailText.Text = "";

            var phpProgress = new Progress<DownloadProgress>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    PhpProgress.Value = p.ProgressPercent;
                    PhpPercent.Text = p.State switch
                    {
                        DownloadState.Downloading => $"{p.ProgressPercent:F0}%",
                        DownloadState.Extracting => "Cikartiliyor",
                        DownloadState.Completed => "Tamam",
                        _ => ""
                    };
                    DetailText.Text = p.State switch
                    {
                        DownloadState.Downloading => $"Indiriliyor... {p.BytesReceived / 1024 / 1024}MB",
                        DownloadState.Extracting => "Dosyalar cikartiliyor...",
                        _ => ""
                    };
                });
            });

            await _versionManager.InstallVersionAsync(phpVersion, phpProgress);
            PhpProgress.Value = 100;
            PhpPercent.Text = "Tamam";

            // Set PHP as active
            await _versionManager.SetActiveVersionAsync(ServiceType.Php, phpVersion.Version);
            config.ActivePhpVersion = phpVersion.Version;

            // Download MariaDB
            StatusText.Text = "MariaDB indiriliyor...";
            MariaDbLabel.Text = $"MariaDB {mariaDbVersion.Version}";
            DetailText.Text = "";

            var mariaDbProgress = new Progress<DownloadProgress>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    MariaDbProgress.Value = p.ProgressPercent;
                    MariaDbPercent.Text = p.State switch
                    {
                        DownloadState.Downloading => $"{p.ProgressPercent:F0}%",
                        DownloadState.Extracting => "Cikartiliyor",
                        DownloadState.Completed => "Tamam",
                        _ => ""
                    };
                    DetailText.Text = p.State switch
                    {
                        DownloadState.Downloading => $"Indiriliyor... {p.BytesReceived / 1024 / 1024}MB",
                        DownloadState.Extracting => "Dosyalar cikartiliyor...",
                        _ => ""
                    };
                });
            });

            await _versionManager.InstallVersionAsync(mariaDbVersion, mariaDbProgress);
            MariaDbProgress.Value = 100;
            MariaDbPercent.Text = "Tamam";

            // Set MariaDB as active
            await _versionManager.SetActiveVersionAsync(ServiceType.MariaDb, mariaDbVersion.Version);
            config.ActiveMariaDbVersion = mariaDbVersion.Version;
            await _configManager.SaveAsync(config);

            // Done
            StatusText.Text = "Kurulum tamamlandi!";
            DetailText.Text = "";
            BottomText.Text = "ZaraGON baslatiliyor...";

            await Task.Delay(1000);
            SetupCompleted = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void ShowError(string message)
    {
        StatusText.Text = "Kurulum basarisiz";
        DetailText.Text = message;
        BottomText.Text = "";
        ErrorButtons.Visibility = Visibility.Visible;
    }

    private async void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        await RunSetupAsync();
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        SetupCompleted = true; // Let user into the app to manually download
        Close();
    }

    private static void KillRunningServices()
    {
        string[] processNames = ["httpd", "mysqld", "mariadbd"];
        foreach (var name in processNames)
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName(name))
                {
                    try { proc.Kill(); proc.WaitForExit(3000); } catch { }
                    proc.Dispose();
                }
            }
            catch { }
        }
    }
}
