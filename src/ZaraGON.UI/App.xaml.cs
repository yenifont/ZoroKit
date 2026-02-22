using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
#pragma warning disable SYSLIB1054
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SD = System.Drawing;
using SWF = System.Windows.Forms;
using ZaraGON.Application.ConfigGeneration;
using ZaraGON.Application.Services;
using ZaraGON.Core.Constants;
using ZaraGON.Core.Enums;
using ZaraGON.Core.Interfaces.Infrastructure;
using ZaraGON.Core.Interfaces.Providers;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Infrastructure.Configuration;
using ZaraGON.Infrastructure.FileSystem;
using ZaraGON.Infrastructure.Logging;
using ZaraGON.Infrastructure.Network;
using ZaraGON.Infrastructure.Privilege;
using ZaraGON.Infrastructure.Process;
using ZaraGON.Infrastructure.Runtime;
using ZaraGON.Infrastructure.VersionProviders;
using ZaraGON.UI.Services;
using ZaraGON.UI.ViewModels;
using ZaraGON.UI.Views;

namespace ZaraGON.UI;

public partial class App : System.Windows.Application
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    private static Mutex? _singleInstanceMutex;
    private ServiceProvider? _serviceProvider;
    private SWF.NotifyIcon? _notifyIcon;
    private SD.Bitmap? _cachedBaseBitmap;
    private IntPtr _currentIconHandle;
    private string _basePath = "";

    // Tray menu items – dynamic enable/disable based on service state
    private SWF.ToolStripMenuItem? _apacheStartItem;
    private SWF.ToolStripMenuItem? _apacheStopItem;
    private SWF.ToolStripMenuItem? _apacheRestartItem;
    private SWF.ToolStripMenuItem? _mariaStartItem;
    private SWF.ToolStripMenuItem? _mariaStopItem;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single instance enforcement — if already running, activate existing window
        _singleInstanceMutex = new Mutex(true, "ZaraGON_SingleInstance_E8F3A2B1", out var isNewInstance);
        if (!isNewInstance)
        {
            ActivateExistingInstance();
            Shutdown();
            return;
        }

        var basePath = ResolveBasePath();
        if (basePath == null)
        {
            Shutdown();
            return;
        }
        _basePath = basePath;

        var services = new ServiceCollection();
        ConfigureServices(services, basePath);
        _serviceProvider = services.BuildServiceProvider();

        // Initialize
        var orchestrator = _serviceProvider.GetRequiredService<OrchestratorService>();
        await orchestrator.InitializeAsync();

        // First run check - auto download Apache + PHP if not installed
        var versionManager = _serviceProvider.GetRequiredService<IVersionManager>();
        var installed = await versionManager.GetInstalledVersionsAsync(ServiceType.Apache);
        if (installed.Count == 0)
        {
            var configManager = _serviceProvider.GetRequiredService<IConfigurationManager>();
            var vcRedistChecker = _serviceProvider.GetRequiredService<IVcRedistChecker>();
            var firstRun = new FirstRunWindow(versionManager, configManager, vcRedistChecker);
            firstRun.ShowDialog();

            if (!firstRun.SetupCompleted)
            {
                Shutdown();
                return;
            }

            // Regenerate all config files now that versions are installed
            await orchestrator.SyncAllConfigsAsync();
        }

        // Auto-start services if configured — run in parallel
        try
        {
            var configManager = _serviceProvider.GetRequiredService<IConfigurationManager>();
            var config = await configManager.LoadAsync();
            var autoStartTasks = new List<Task>();

            if (config.AutoStartApache)
            {
                autoStartTasks.Add(Task.Run(async () =>
                {
                    var apache = _serviceProvider.GetRequiredService<IServiceController>();
                    var apacheStatus = await apache.GetStatusAsync();
                    if (apacheStatus != ServiceStatus.Running)
                        await apache.StartAsync();
                }));
            }
            if (config.AutoStartMariaDb)
            {
                autoStartTasks.Add(Task.Run(async () =>
                {
                    var mariaDb = _serviceProvider.GetRequiredService<MariaDbService>();
                    var mariaStatus = await mariaDb.GetStatusAsync();
                    if (mariaStatus != ServiceStatus.Running)
                        await mariaDb.StartAsync();
                }));
            }

            if (autoStartTasks.Count > 0)
                await Task.WhenAll(autoStartTasks);
        }
        catch { /* auto-start is best-effort */ }

        // Show main window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // Create system tray icon
        InitializeNotifyIcon(mainWindow);

        // Check for app updates in background
        _ = CheckForAppUpdateAsync();
    }

    private async Task CheckForAppUpdateAsync()
    {
        try
        {
            var httpClient = _serviceProvider!.GetRequiredService<HttpClient>();
            var toastService = _serviceProvider!.GetRequiredService<ToastService>();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            var request = new HttpRequestMessage(HttpMethod.Get, Defaults.GitHubReleasesApi);
            request.Headers.TryAddWithoutValidation("User-Agent", "ZaraGON/1.0 (Windows; https://github.com/yenifont/ZaraGON)");
            request.Headers.Add("Accept", "application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            var response = await httpClient.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync(cts.Token);

            // Parse tag_name from JSON
            var tagMatch = System.Text.RegularExpressions.Regex.Match(json, @"""tag_name""\s*:\s*""([^""]+)""");
            if (!tagMatch.Success) return;

            var latestTag = tagMatch.Groups[1].Value.TrimStart('v', 'V');
            var currentVersion = Defaults.AppVersion;

            if (Version.TryParse(latestTag, out var latestVer) &&
                Version.TryParse(currentVersion, out var currentVer) &&
                latestVer > currentVer)
            {
                Dispatcher.Invoke(() =>
                    toastService.ShowInfo($"ZaraGON v{latestTag} mevcut! Güncellemeler sayfasını kontrol edin."));
            }
        }
        catch { /* update check is best-effort */ }
    }

    private void InitializeNotifyIcon(MainWindow mainWindow)
    {
        var baseIcon = LoadAppIcon();

        // Cache the base bitmap once - reused for every status dot render
        _cachedBaseBitmap = new SD.Bitmap(baseIcon.ToBitmap(), 32, 32);

        _notifyIcon = new SWF.NotifyIcon
        {
            Icon = baseIcon,
            Text = "ZaraGON",
            Visible = true
        };

        // Resolve singleton services for tray actions
        var apache = _serviceProvider!.GetRequiredService<IServiceController>();
        var mariaDb = _serviceProvider!.GetRequiredService<MariaDbService>();
        var orchestrator = _serviceProvider!.GetRequiredService<OrchestratorService>();
        var configManager = _serviceProvider!.GetRequiredService<IConfigurationManager>();
        var dispatcher = Dispatcher.CurrentDispatcher;

        var menu = new SWF.ContextMenuStrip();

        // ── Apache ──────────────────────────────────
        _apacheStartItem = new SWF.ToolStripMenuItem("Apache Ba\u015flat", CreatePlayIcon(),
            (_, _) => TrayRunAsync(() => apache.StartAsync()));
        _apacheStopItem = new SWF.ToolStripMenuItem("Apache Durdur", CreateStopIcon(),
            (_, _) => TrayRunAsync(() => apache.StopAsync()));
        _apacheRestartItem = new SWF.ToolStripMenuItem("Apache Yeniden Ba\u015flat", CreateRestartIcon(),
            (_, _) => TrayRunAsync(() => apache.RestartAsync()));

        menu.Items.Add(_apacheStartItem);
        menu.Items.Add(_apacheStopItem);
        menu.Items.Add(_apacheRestartItem);
        menu.Items.Add(new SWF.ToolStripSeparator());

        // ── MariaDB ─────────────────────────────────
        _mariaStartItem = new SWF.ToolStripMenuItem("MariaDB Ba\u015flat", CreatePlayIcon(),
            (_, _) => TrayRunAsync(() => mariaDb.StartAsync()));
        _mariaStopItem = new SWF.ToolStripMenuItem("MariaDB Durdur", CreateStopIcon(),
            (_, _) => TrayRunAsync(() => mariaDb.StopAsync()));

        menu.Items.Add(_mariaStartItem);
        menu.Items.Add(_mariaStopItem);
        menu.Items.Add(new SWF.ToolStripSeparator());

        // ── T\u00fcm Servisler ──────────────────────────
        menu.Items.Add(new SWF.ToolStripMenuItem("T\u00fcm\u00fcn\u00fc Ba\u015flat", CreateStartAllIcon(),
            (_, _) => TrayRunAsync(() => orchestrator.StartAllAsync())));
        menu.Items.Add(new SWF.ToolStripMenuItem("T\u00fcm\u00fcn\u00fc Durdur", CreateStopAllIcon(),
            (_, _) => TrayRunAsync(() => orchestrator.StopAllAsync())));
        menu.Items.Add(new SWF.ToolStripSeparator());

        // ── H\u0131zl\u0131 Ba\u011flant\u0131lar ────────────────────────
        menu.Items.Add(new SWF.ToolStripMenuItem("localhost", CreateGlobeIcon(),
            async (_, _) =>
            {
                try
                {
                    var config = await configManager.LoadAsync();
                    OpenUrl($"http://localhost:{config.ApachePort}");
                }
                catch { }
            }));
        menu.Items.Add(new SWF.ToolStripMenuItem("phpMyAdmin", CreateDatabaseIcon(),
            async (_, _) =>
            {
                try
                {
                    var config = await configManager.LoadAsync();
                    OpenUrl($"http://localhost:{config.ApachePort}/phpmyadmin");
                }
                catch { }
            }));
        menu.Items.Add(new SWF.ToolStripMenuItem("Root Dizini", CreateFolderIcon(),
            (_, _) =>
            {
                try
                {
                    var rootPath = Path.GetFullPath(Path.Combine(_basePath, Defaults.DocumentRoot));
                    if (Directory.Exists(rootPath))
                        Process.Start(new ProcessStartInfo("explorer.exe", rootPath))?.Dispose();
                }
                catch { }
            }));
        menu.Items.Add(new SWF.ToolStripMenuItem("Terminal", CreateTerminalIcon(),
            async (_, _) =>
            {
                try
                {
                    var rootPath = Path.GetFullPath(Path.Combine(_basePath, Defaults.DocumentRoot));
                    if (!Directory.Exists(rootPath)) rootPath = _basePath;

                    var config = await configManager.LoadAsync();
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/K \"color 0A && title ZaraGON Terminal\"",
                        WorkingDirectory = rootPath,
                        UseShellExecute = false,
                        CreateNoWindow = false
                    };

                    var extraDirs = new List<string>();
                    if (!string.IsNullOrEmpty(config.ActivePhpVersion))
                    {
                        var phpDir = Path.GetFullPath(Path.Combine(_basePath, Defaults.BinDir, "php", config.ActivePhpVersion));
                        if (Directory.Exists(phpDir)) extraDirs.Add(phpDir);
                    }
                    if (!string.IsNullOrEmpty(config.ActiveMariaDbVersion))
                    {
                        var mariaDir = Path.GetFullPath(Path.Combine(_basePath, Defaults.BinDir, "mariadb", config.ActiveMariaDbVersion, "bin"));
                        if (Directory.Exists(mariaDir)) extraDirs.Add(mariaDir);
                    }
                    var composerDir = Path.GetFullPath(Path.Combine(_basePath, Defaults.ComposerDir));
                    if (Directory.Exists(composerDir)) extraDirs.Add(composerDir);

                    if (extraDirs.Count > 0)
                    {
                        var existingPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                        psi.Environment["PATH"] = string.Join(";", extraDirs) + ";" + existingPath;
                    }

                    Process.Start(psi)?.Dispose();
                }
                catch { }
            }));
        menu.Items.Add(new SWF.ToolStripSeparator());

        // ── Uygulama ────────────────────────────────
        menu.Items.Add(new SWF.ToolStripMenuItem("G\u00f6ster", CreateWindowIcon(),
            (_, _) => ShowMainWindow(mainWindow)));
        menu.Items.Add(new SWF.ToolStripMenuItem("\u00c7\u0131k\u0131\u015f", CreateCloseIcon(),
            (_, _) => ExitApplication()));

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow(mainWindow);

        // Subscribe to service status changes for tray icon indicator
        apache.StatusChanged += (_, _) => dispatcher.BeginInvoke(() => UpdateTrayIconStatus(apache, mariaDb));
        mariaDb.StatusChanged += (_, _) => dispatcher.BeginInvoke(() => UpdateTrayIconStatus(apache, mariaDb));

        UpdateTrayIconStatus(apache, mariaDb);
    }

    private void TrayRunAsync(Func<Task> action)
    {
        _ = Current.Dispatcher.InvokeAsync(async () =>
        {
            try { await action(); }
            catch (Exception ex)
            {
                _serviceProvider?.GetService<ToastService>()?.ShowError(ex.Message);
            }
        });
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose(); }
        catch { }
    }

    private void UpdateTrayIconStatus(IServiceController apache, IServiceController mariaDb)
    {
        if (_notifyIcon == null || _cachedBaseBitmap == null) return;

        var apacheRunning = apache.Status == ServiceStatus.Running;
        var mariaRunning = mariaDb.Status == ServiceStatus.Running;

        // Update tray menu enabled states
        if (_apacheStartItem != null) _apacheStartItem.Enabled = !apacheRunning;
        if (_apacheStopItem != null) _apacheStopItem.Enabled = apacheRunning;
        if (_apacheRestartItem != null) _apacheRestartItem.Enabled = apacheRunning;
        if (_mariaStartItem != null) _mariaStartItem.Enabled = !mariaRunning;
        if (_mariaStopItem != null) _mariaStopItem.Enabled = mariaRunning;

        SD.Color dotColor;
        string statusText;

        if (apacheRunning && mariaRunning)
        {
            dotColor = SD.Color.FromArgb(255, 76, 175, 80);
            statusText = "ZaraGON - Apache: \u00c7al\u0131\u015f\u0131yor, MariaDB: \u00c7al\u0131\u015f\u0131yor";
        }
        else if (!apacheRunning && !mariaRunning)
        {
            dotColor = SD.Color.FromArgb(255, 244, 67, 54);
            statusText = "ZaraGON - Apache: Durdu, MariaDB: Durdu";
        }
        else
        {
            dotColor = SD.Color.FromArgb(255, 255, 152, 0);
            var aText = apacheRunning ? "\u00c7al\u0131\u015f\u0131yor" : "Durdu";
            var mText = mariaRunning ? "\u00c7al\u0131\u015f\u0131yor" : "Durdu";
            statusText = $"ZaraGON - Apache: {aText}, MariaDB: {mText}";
        }

        try
        {
            // Clone cached base bitmap to draw status dot on it
            using var bitmap = new SD.Bitmap(_cachedBaseBitmap);
            using var g = SD.Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            const int dotSize = 12;
            var x = bitmap.Width - dotSize - 1;
            var y = bitmap.Height - dotSize - 1;

            using var borderBrush = new SD.SolidBrush(SD.Color.White);
            g.FillEllipse(borderBrush, x - 1, y - 1, dotSize + 2, dotSize + 2);

            using var dotBrush = new SD.SolidBrush(dotColor);
            g.FillEllipse(dotBrush, x, y, dotSize, dotSize);

            // Destroy previous native icon handle to prevent GDI leak
            var newHandle = bitmap.GetHicon();
            _notifyIcon.Icon = SD.Icon.FromHandle(newHandle);
            _notifyIcon.Text = statusText;

            if (_currentIconHandle != IntPtr.Zero)
                DestroyIcon(_currentIconHandle);
            _currentIconHandle = newHandle;
        }
        catch
        {
            _notifyIcon.Text = statusText;
        }
    }

    private static void ShowMainWindow(Window window)
    {
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private void ExitApplication()
    {
        // Allow MainWindow to actually close instead of hiding to tray
        if (MainWindow is MainWindow mw)
            mw.AllowClose();

        Current.Shutdown();
    }

    // ── Tray Menu Icon Bitmaps (16x16) ──────────────────

    private static SD.Bitmap CreateTrayBitmap(Action<SD.Graphics> draw)
    {
        var bmp = new SD.Bitmap(16, 16);
        using var g = SD.Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        draw(g);
        return bmp;
    }

    private static SD.Bitmap CreatePlayIcon() => CreateTrayBitmap(g =>
    {
        using var b = new SD.SolidBrush(SD.Color.FromArgb(76, 175, 80));
        g.FillPolygon(b, [new SD.Point(4, 2), new SD.Point(13, 8), new SD.Point(4, 14)]);
    });

    private static SD.Bitmap CreateStopIcon() => CreateTrayBitmap(g =>
    {
        using var b = new SD.SolidBrush(SD.Color.FromArgb(244, 67, 54));
        g.FillRectangle(b, 3, 3, 10, 10);
    });

    private static SD.Bitmap CreateRestartIcon() => CreateTrayBitmap(g =>
    {
        var c = SD.Color.FromArgb(33, 150, 243);
        using var pen = new SD.Pen(c, 2f);
        g.DrawArc(pen, 2, 2, 12, 12, -90, 270);
        using var b = new SD.SolidBrush(c);
        g.FillPolygon(b, [new SD.Point(5, 0), new SD.Point(11, 3), new SD.Point(5, 5)]);
    });

    private static SD.Bitmap CreateStartAllIcon() => CreateTrayBitmap(g =>
    {
        using var b = new SD.SolidBrush(SD.Color.FromArgb(76, 175, 80));
        g.FillPolygon(b, [new SD.Point(1, 2), new SD.Point(8, 8), new SD.Point(1, 14)]);
        g.FillPolygon(b, [new SD.Point(7, 2), new SD.Point(14, 8), new SD.Point(7, 14)]);
    });

    private static SD.Bitmap CreateStopAllIcon() => CreateTrayBitmap(g =>
    {
        using var b = new SD.SolidBrush(SD.Color.FromArgb(244, 67, 54));
        g.FillRectangle(b, 1, 3, 6, 10);
        g.FillRectangle(b, 9, 3, 6, 10);
    });

    private static SD.Bitmap CreateGlobeIcon() => CreateTrayBitmap(g =>
    {
        var c = SD.Color.FromArgb(33, 150, 243);
        using var pen = new SD.Pen(c, 1.4f);
        g.DrawEllipse(pen, 2, 2, 12, 12);
        g.DrawLine(pen, 8, 2, 8, 14);
        g.DrawArc(pen, 4, 2, 8, 12, -90, 180);
        g.DrawLine(pen, 2, 8, 14, 8);
    });

    private static SD.Bitmap CreateDatabaseIcon() => CreateTrayBitmap(g =>
    {
        var c = SD.Color.FromArgb(0, 150, 136);
        using var b = new SD.SolidBrush(c);
        using var pen = new SD.Pen(c, 1.4f);
        g.FillEllipse(b, 3, 1, 10, 5);
        g.DrawLine(pen, 3, 3, 3, 13);
        g.DrawLine(pen, 13, 3, 13, 13);
        g.DrawArc(pen, 3, 10, 10, 5, 0, 180);
    });

    private static SD.Bitmap CreateFolderIcon() => CreateTrayBitmap(g =>
    {
        using var b = new SD.SolidBrush(SD.Color.FromArgb(255, 152, 0));
        g.FillRectangle(b, 1, 3, 5, 2);
        g.FillRectangle(b, 1, 5, 14, 9);
    });

    private static SD.Bitmap CreateTerminalIcon() => CreateTrayBitmap(g =>
    {
        using var bg = new SD.SolidBrush(SD.Color.FromArgb(69, 90, 100));
        g.FillRectangle(bg, 1, 2, 14, 12);
        using var pen = new SD.Pen(SD.Color.White, 1.6f);
        g.DrawLines(pen, [new SD.Point(4, 5), new SD.Point(7, 8), new SD.Point(4, 11)]);
        g.DrawLine(pen, 9, 11, 12, 11);
    });

    private static SD.Bitmap CreateWindowIcon() => CreateTrayBitmap(g =>
    {
        var c = SD.Color.FromArgb(33, 150, 243);
        using var b = new SD.SolidBrush(c);
        using var pen = new SD.Pen(c, 1.4f);
        g.FillRectangle(b, 1, 2, 14, 4);
        g.DrawRectangle(pen, 1, 2, 14, 12);
    });

    private static SD.Bitmap CreateCloseIcon() => CreateTrayBitmap(g =>
    {
        using var pen = new SD.Pen(SD.Color.FromArgb(244, 67, 54), 2f);
        g.DrawLine(pen, 3, 3, 13, 13);
        g.DrawLine(pen, 13, 3, 3, 13);
    });

    // ── Single Instance ────────────────────────────────

    private static void ActivateExistingInstance()
    {
        var current = System.Diagnostics.Process.GetCurrentProcess();
        foreach (var process in System.Diagnostics.Process.GetProcessesByName(current.ProcessName))
        {
            if (process.Id != current.Id && process.MainWindowHandle != IntPtr.Zero)
            {
                ShowWindow(process.MainWindowHandle, SW_RESTORE);
                SetForegroundWindow(process.MainWindowHandle);
                process.Dispose();
                break;
            }
            process.Dispose();
        }
    }

    // ── App Icon ────────────────────────────────────────

    private static SD.Icon LoadAppIcon()
    {
        // Try file system first (dev/debug)
        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
        if (File.Exists(iconPath))
            return new SD.Icon(iconPath);

        // Try embedded resource (single-file publish)
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("app.ico", StringComparison.OrdinalIgnoreCase));
            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                    return new SD.Icon(stream);
            }
        }
        catch { }

        // Try extract from exe icon
        try
        {
            var exePath = Environment.ProcessPath;
            if (exePath != null)
            {
                var exeIcon = SD.Icon.ExtractAssociatedIcon(exePath);
                if (exeIcon != null) return exeIcon;
            }
        }
        catch { }

        return SD.SystemIcons.Application;
    }

    // ── DI Configuration ────────────────────────────────

    private static void ConfigureServices(IServiceCollection services, string basePath)
    {
        // Infrastructure
        services.AddSingleton<IFileSystem, WindowsFileSystem>();
        services.AddSingleton<IArchiveExtractor, ArchiveExtractor>();
        services.AddSingleton<IPrivilegeManager, WindowsPrivilegeManager>();
        services.AddSingleton<IProcessManager>(sp =>
        {
            var pm = new WindowsProcessManager();
            return pm;
        });

        // HttpClient
        services.AddHttpClient();
        services.AddSingleton(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) ZaraGON/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
        });

        // Configuration
        services.AddSingleton<IConfigurationManager>(sp =>
            new JsonConfigurationStore(sp.GetRequiredService<IFileSystem>(), basePath));

        // Version Providers
        services.AddSingleton<IVersionProvider>(sp =>
            new ApacheLoungeVersionProvider(sp.GetRequiredService<HttpClient>()));
        services.AddSingleton<IVersionProvider>(sp =>
            new PhpWindowsVersionProvider(sp.GetRequiredService<HttpClient>()));
        services.AddSingleton<IVersionProvider>(sp =>
            new MariaDbVersionProvider(sp.GetRequiredService<HttpClient>()));
        services.AddSingleton<IVersionProvider>(sp =>
            new PhpMyAdminVersionProvider(sp.GetRequiredService<HttpClient>()));

        // Download Manager
        services.AddSingleton<IDownloadManager>(sp =>
            new HttpDownloader(sp.GetRequiredService<HttpClient>(), sp.GetRequiredService<IArchiveExtractor>()));

        // Port Manager
        services.AddSingleton<IPortManager>(sp =>
            new PortScanner(sp.GetRequiredService<IProcessManager>()));

        // Log Watcher
        services.AddSingleton<ILogWatcher, FileLogWatcher>();

        // VC++ Redistributable Checker
        services.AddSingleton<IVcRedistChecker>(sp =>
            new VcRedistChecker(sp.GetRequiredService<HttpClient>()));

        // Version Manager
        services.AddSingleton<IVersionManager>(sp =>
            new VersionManagerService(
                sp.GetServices<IVersionProvider>(),
                sp.GetRequiredService<IDownloadManager>(),
                sp.GetRequiredService<IFileSystem>(),
                basePath));

        // Config Generators
        services.AddSingleton<ApacheConfigGenerator>();
        services.AddSingleton<PhpIniGenerator>();
        services.AddSingleton<MariaDbConfigGenerator>();

        // Apache Service Controller
        services.AddSingleton<IServiceController>(sp =>
            new ApacheService(
                sp.GetRequiredService<IVersionManager>(),
                sp.GetRequiredService<IProcessManager>(),
                sp.GetRequiredService<IPortManager>(),
                sp.GetRequiredService<IConfigurationManager>(),
                sp.GetRequiredService<IFileSystem>(),
                sp.GetRequiredService<ApacheConfigGenerator>(),
                basePath));

        // MariaDB Service Controller (registered as concrete type to distinguish from Apache)
        services.AddSingleton<MariaDbService>(sp =>
            new MariaDbService(
                sp.GetRequiredService<IVersionManager>(),
                sp.GetRequiredService<IProcessManager>(),
                sp.GetRequiredService<IPortManager>(),
                sp.GetRequiredService<IConfigurationManager>(),
                sp.GetRequiredService<IFileSystem>(),
                sp.GetRequiredService<MariaDbConfigGenerator>(),
                basePath));

        // PHP Service
        services.AddSingleton<PhpService>(sp =>
            new PhpService(
                sp.GetRequiredService<IVersionManager>(),
                sp.GetRequiredService<IFileSystem>(),
                sp.GetRequiredService<IConfigurationManager>(),
                sp.GetRequiredService<PhpIniGenerator>(),
                basePath));
        services.AddSingleton<IPhpExtensionManager>(sp => sp.GetRequiredService<PhpService>());

        // Hosts File Manager
        services.AddSingleton<IHostsFileManager>(sp =>
            new HostsFileService(
                sp.GetRequiredService<IFileSystem>(),
                sp.GetRequiredService<IPrivilegeManager>()));

        // Auto Virtual Hosts
        services.AddSingleton<IAutoVirtualHostManager>(sp =>
            new AutoVirtualHostService(
                sp.GetRequiredService<IFileSystem>(),
                sp.GetRequiredService<IHostsFileManager>(),
                sp.GetRequiredService<IConfigurationManager>(),
                basePath));

        // SSL Certificate Manager
        services.AddSingleton<ISslCertificateManager>(sp =>
            new SslCertificateService(sp.GetRequiredService<IFileSystem>(), basePath));

        // Health Checker
        services.AddSingleton<IHealthChecker>(sp =>
            new HealthCheckerService(
                sp.GetRequiredService<IServiceController>(),
                sp.GetRequiredService<MariaDbService>(),
                sp.GetRequiredService<IVersionManager>(),
                sp.GetRequiredService<IProcessManager>(),
                sp.GetRequiredService<IConfigurationManager>(),
                sp.GetRequiredService<IVcRedistChecker>()));

        // Orchestrator
        services.AddSingleton<OrchestratorService>(sp =>
            new OrchestratorService(
                sp.GetRequiredService<IServiceController>(),
                sp.GetRequiredService<MariaDbService>(),
                sp.GetRequiredService<IVersionManager>(),
                sp.GetRequiredService<IConfigurationManager>(),
                sp.GetRequiredService<ILogWatcher>(),
                sp.GetRequiredService<IHealthChecker>(),
                sp.GetRequiredService<IFileSystem>(),
                sp.GetRequiredService<PhpService>(),
                sp.GetRequiredService<IAutoVirtualHostManager>(),
                sp.GetRequiredService<IPortManager>(),
                sp.GetRequiredService<HttpClient>(),
                basePath));

        // UI Services
        services.AddSingleton<NavigationService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<ToastService>(new ToastService(basePath));

        // ViewModels
        services.AddSingleton<DashboardViewModel>(sp =>
            new DashboardViewModel(
                sp.GetRequiredService<OrchestratorService>(),
                sp.GetRequiredService<IServiceController>(),
                sp.GetRequiredService<MariaDbService>(),
                sp.GetRequiredService<IVersionManager>(),
                sp.GetRequiredService<IConfigurationManager>(),
                sp.GetRequiredService<IHealthChecker>(),
                sp.GetRequiredService<IVcRedistChecker>(),
                sp.GetRequiredService<IPortManager>(),
                sp.GetRequiredService<DialogService>(),
                sp.GetRequiredService<ToastService>(),
                sp.GetRequiredService<HttpClient>(),
                basePath));
        services.AddSingleton<ApacheViewModel>(sp =>
            new ApacheViewModel(
                sp.GetRequiredService<IServiceController>(),
                sp.GetRequiredService<IVersionManager>(),
                sp.GetRequiredService<IConfigurationManager>(),
                sp.GetRequiredService<IPortManager>(),
                sp.GetRequiredService<DialogService>(),
                basePath));
        services.AddTransient<PhpViewModel>(sp =>
            new PhpViewModel(
                sp.GetRequiredService<IVersionManager>(),
                sp.GetRequiredService<IPhpExtensionManager>(),
                sp.GetRequiredService<IConfigurationManager>(),
                sp.GetRequiredService<OrchestratorService>(),
                sp.GetRequiredService<IVcRedistChecker>(),
                sp.GetRequiredService<DialogService>(),
                sp.GetRequiredService<ToastService>(),
                basePath));
        services.AddTransient<SettingsViewModel>(sp =>
            new SettingsViewModel(
                sp.GetRequiredService<IConfigurationManager>(),
                sp.GetRequiredService<OrchestratorService>(),
                sp.GetRequiredService<ISslCertificateManager>(),
                sp.GetRequiredService<DialogService>(),
                sp.GetRequiredService<ToastService>(),
                basePath));
        services.AddTransient<HostsFileViewModel>();
        services.AddSingleton<LogViewModel>();
        services.AddTransient<UpdatesViewModel>(sp =>
            new UpdatesViewModel(
                sp.GetRequiredService<IVersionManager>(),
                sp.GetRequiredService<IConfigurationManager>(),
                sp.GetRequiredService<OrchestratorService>(),
                sp.GetRequiredService<IServiceController>(),
                sp.GetRequiredService<MariaDbService>(),
                sp.GetRequiredService<IDownloadManager>(),
                sp.GetRequiredService<IFileSystem>(),
                sp.GetRequiredService<DialogService>(),
                sp.GetRequiredService<HttpClient>(),
                basePath));

        // Main ViewModel
        services.AddSingleton<MainViewModel>(sp =>
        {
            var nav = sp.GetRequiredService<NavigationService>();

            nav.RegisterView("Dashboard", () => sp.GetRequiredService<DashboardViewModel>());
            nav.RegisterView("Apache", () => sp.GetRequiredService<ApacheViewModel>());
            nav.RegisterView("PHP", () => sp.GetRequiredService<PhpViewModel>());
            nav.RegisterView("Settings", () => sp.GetRequiredService<SettingsViewModel>());
            nav.RegisterView("HostsFile", () => sp.GetRequiredService<HostsFileViewModel>());
            nav.RegisterView("Logs", () => sp.GetRequiredService<LogViewModel>());
            nav.RegisterView("Updates", () => sp.GetRequiredService<UpdatesViewModel>());

            var vm = new MainViewModel(
                nav,
                sp.GetRequiredService<IPortManager>(),
                sp.GetRequiredService<IConfigurationManager>(),
                sp.GetRequiredService<IServiceController>(),
                sp.GetRequiredService<MariaDbService>(),
                sp.GetRequiredService<ToastService>());
            nav.NavigateTo("Dashboard");
            return vm;
        });

        // Main Window
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        _cachedBaseBitmap?.Dispose();

        if (_currentIconHandle != IntPtr.Zero)
            DestroyIcon(_currentIconHandle);

        // Kill tunnel process if running
        try { _serviceProvider?.GetService<DashboardViewModel>()?.StopTunnel(); } catch { }

        // Services keep running in background - only dispose DI container
        _serviceProvider?.Dispose();

        // Release single instance mutex
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }

    private static string? FindSolutionRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ZaraGON.sln")) ||
                File.Exists(Path.Combine(dir.FullName, "ZaraGON.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    private static readonly string InstallPathFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ZaraGON", "installpath.txt");

    /// <summary>
    /// 1) Dev mode: walk up to ZaraGON.sln
    /// 2) Saved install path from %LocalAppData%
    /// 3) First run: show path picker dialog
    /// </summary>
    private string? ResolveBasePath()
    {
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        var solutionRoot = FindSolutionRoot(exeDir);
        if (solutionRoot != null)
            return solutionRoot;

        var savedPath = LoadInstallPath();
        if (savedPath != null && Directory.Exists(savedPath))
            return savedPath;

        var dialog = new Views.InstallPathWindow();
        var result = dialog.ShowDialog();
        if (result != true)
            return null;

        SaveInstallPath(dialog.SelectedPath);
        return dialog.SelectedPath;
    }

    private static string? LoadInstallPath()
    {
        try
        {
            if (File.Exists(InstallPathFile))
            {
                var path = File.ReadAllText(InstallPathFile).Trim();
                if (!string.IsNullOrEmpty(path))
                    return path;
            }
        }
        catch { }
        return null;
    }

    private static void SaveInstallPath(string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(InstallPathFile)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(InstallPathFile, path);
        }
        catch { }
    }
}
