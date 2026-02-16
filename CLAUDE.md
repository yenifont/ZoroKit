# ZaraGON - Project Guide

## What is this?
ZaraGON is a Windows desktop application (like XAMPP/WampServer/Laragon) for managing local Apache, PHP, and MariaDB development environments. Built with WPF (.NET 9) using MVVM pattern.

## Architecture

```
src/
  ZaraGON.Core/           # Domain models, interfaces, enums, constants (no dependencies)
  ZaraGON.Application/    # Business logic: services, config generators (depends on Core)
  ZaraGON.Infrastructure/ # Implementations: file system, process, network, config store (depends on Core)
  ZaraGON.UI/             # WPF app: views, viewmodels, converters, DI wiring (depends on all)
```

Dependency flow: `UI -> Application + Infrastructure -> Core`

## Directory Layout (Runtime)

All runtime data lives directly under the app root (NO `data/` prefix):

```
C:\ZaraGON\                 (or project root when debugging)
  ZaraGON.exe
  bin/
    apache/2.4.66/          # Apache installations
    php/8.5.1/              # PHP installations
    mariadb/12.2.2/         # MariaDB installations
    composer/               # Composer binary
    cloudflared/            # Cloudflare Tunnel binary (auto-downloaded)
  config/
    apache/                 # httpd.conf, httpd-vhosts.conf
      sites-enabled/        # Virtual host configs
      alias/                # Apache alias configs
    php/                    # php.ini
    mariadb/                # my.ini
    ssl/                    # SSL certificates
    zoragon.json            # Main app config
    versions.json           # Installed version registry
  www/                      # Document root (index.php = ZaraGON dashboard)
  apps/
    phpmyadmin/             # phpMyAdmin
  logs/
    apache/                 # error.log, access.log
  mariadb/                  # MariaDB data directory
  backups/                  # Database backups
  temp/                     # Temporary downloads
```

Path constants defined in `Defaults.cs` — all relative (e.g. `"bin"`, `"config/apache"`, `"www"`).

## Build & Run

```bash
# Build
dotnet build src/ZaraGON.UI/ZaraGON.UI.csproj

# Run (requires killing existing instance first if running)
taskkill /IM ZaraGON.exe /F 2>nul
start "" src/ZaraGON.UI/bin/Debug/net9.0-windows/ZaraGON.exe

# Publish (self-contained single-file + VC++ download + installer)
powershell -ExecutionPolicy Bypass -File build.ps1
```

Target: `net9.0-windows`, C# 13, Nullable enabled, ImplicitUsings enabled.

## Packaging & Installer

- `build.ps1` — Full build pipeline: clean → publish (self-contained, single-file, win-x64) → download VC++ Redistributable → Inno Setup installer
- `installer/ZaraGON.iss` — Inno Setup 6 script: admin install to `C:\ZaraGON`, Turkish UI, wizard images, VC++ silent install, Add/Remove Programs entry
- `installer/deps/vc_redist.x64.exe` — Bundled VC++ Redistributable (auto-downloaded by build.ps1)
- `installer/wizard.bmp` / `wizard-small.bmp` — Generated from `app.png` via `installer/generate-images.ps1`
- `src/ZaraGON.UI/Properties/PublishProfiles/win-x64.pubxml` — Publish profile (self-contained, single-file)
- Output: `publish/ZaraGON.exe` (~174 MB), `installer/Output/ZaraGON-Setup-1.0.0.exe` (~72 MB, VC++ included)

### Installer Features
- VC++ Redistributable installed silently via `[Code]` section (not `[Run]`) — exit code 3010 handled, `NeedRestart()` returns False (no restart prompt)
- Uninstall: 3-layer process killing (InitializeUninstall → UninstallRun → CurUninstallStepChanged), hosts file cleanup, 3-layer directory deletion (DelTree → cmd rmdir → RunOnce fallback)
- Single instance: Mutex `ZaraGON_SingleInstance_E8F3A2B1`, second launch activates existing window via P/Invoke

## Key Patterns & Conventions

### MVVM
- **CommunityToolkit.Mvvm** for `[ObservableProperty]`, `[RelayCommand]`, `ObservableObject`
- Backing fields: `_camelCase` with `[ObservableProperty]` generates `PascalCase` property
- ViewModels registered in `App.xaml.cs` DI container
- View-ViewModel mapping via `DataTemplate` in `MainWindow.xaml`

### DI (Microsoft.Extensions.DependencyInjection)
- All wiring in `App.xaml.cs` `ConfigureServices()`
- Services: Singleton. ViewModels: Singleton (DashboardVM, ApacheVM, LogVM) with `basePath`
- `basePath` = solution root (found by walking up to `ZaraGON.sln`)
- `HttpClient` registered as singleton, injected into OrchestratorService and DashboardViewModel

### Navigation
- `NavigationService` with `RegisterView("key", factory)` pattern + ViewModel caching
- Views registered: Dashboard, Apache, PHP, Settings, HostsFile, Logs, Updates

### Config Storage
- `JsonConfigurationStore` implements `IConfigurationManager`
- Config path: `{basePath}/config/zoragon.json`
- `AppConfiguration` model persisted as JSON
- Load: `await _configManager.LoadAsync()` / Save: `await _configManager.SaveAsync(config)`

### Config Generation
- `PhpIniGenerator.Generate(extensionDir, extensions, tempDir?, PhpSettings?)` -> INI string
- `MariaDbConfigGenerator.Generate(baseDir, dataDir, port, MariaDbSettings?)` -> CNF string
- Settings objects are optional; defaults from model constructors when null
- OPcache block in php.ini is conditional — only written if `php_opcache.dll` exists in extension dir

### Service Controllers
- `IServiceController` (Apache) and `MariaDbService` (concrete, registered separately)
- Status tracking: `ServiceStatus` enum (Stopped, Starting, Running, Stopping, Error, NotInstalled)
- Events: `StatusChanged` event fires on state transitions
- **Apache crash detection**: Poll loop checks `IsProcessRunningAsync` — if process died, reads error log via `ReadLastErrorLogLineAsync` (two-pass: VCRUNTIME priority, then emerg/crit)

### VC++ Runtime Handling (XAMPP/Laragon approach)
- **Installer**: Bundles `vc_redist.x64.exe`, installs silently during setup (no restart)
- **DLL copy**: `ApacheService.CopyRuntimeDlls()` copies vcruntime140.dll + msvcp140.dll from PHP dir to Apache bin dir BEFORE config validation — ensures `httpd -t` uses correct DLLs
- **No pre-start dialogs**: VC++ check removed from StartApache/StartAll — DLL copy handles it silently
- **Health check**: `HealthCheckerService` includes VC++ compatibility check (8th parallel check)
- **Scan for issues**: Dashboard "Sorunlari Tara" detects VC++ issues, QuickFix installs VC++ via `IVcRedistChecker.InstallAsync()`
- `IVcRedistChecker` interface: `GetInstalledVersionAsync`, `CheckCompatibilityAsync`, `TestPhpBinaryAsync`, `InstallAsync`
- `VcRedistChecker` implementation: Registry reading, DLL fallback, `php -v` binary test, silent UAC install
- Uses `SysDiag = System.Diagnostics` alias to avoid namespace conflict with `ZaraGON.Infrastructure.Process`

### Auto-Start
- `AutoStartApache` / `AutoStartMariaDb` flags in `AppConfiguration`
- Checked in `App.xaml.cs` `OnStartup` after `InitializeAsync()`, before showing `MainWindow`
- Best-effort: wrapped in try-catch, silently ignored on failure. Both run in parallel via `Task.WhenAll`

### Toast Notifications
- `ToastService` singleton, thread-safe via `Dispatcher.BeginInvoke`
- Methods: `ShowSuccess()`, `ShowError()`, `ShowWarning()`, `ShowInfo()`
- Auto-dismiss after 3.5s, max 4 visible
- Timer handler properly unsubscribed after fire (named handler pattern)

### Dialogs
- `ModernDialog` custom borderless WPF window (replaces `MessageBox.Show`)
- Types: Info, Warning, Error, Confirm (via `DialogType` enum)
- `DialogService` wraps all dialog calls with thread-safe UI dispatch
- Turkish default titles: Onay, Bilgi, Hata, Uyari, Giris
- `DialogService.PromptInput()` uses `ModernDialog.CreateInput()` for text input

### Version Management
- `IVersionManager` handles install/uninstall/list for Apache, PHP, MariaDB, phpMyAdmin
- `ServiceVersion` model has `IsInstalled` property for UI state tracking
- `VersionPointer` model for installed versions with `IsActive` flag
- **Dynamic path resolution**: `LoadVersionsFileAsync` recomputes `InstallPath` from current `basePath` on every load — handles app directory relocation automatically, persists corrected paths back to `versions.json`
- Available versions: "Indir" button hidden when installed, "Yuklu" badge shown instead
- Installed versions: "Sil" button (hidden for active version), "Aktif" badge for active

### Converters
- `BoolToVisibilityConverter` (built-in WPF) for standard bool→Visible/Collapsed
- `InverseBoolToVisibilityConverter` (custom) for inverse: true→Collapsed, false→Visible
- `ServiceStatusToColorConverter` and `ToastTypeConverters` use static frozen brushes
- Both registered as global resources in `App.xaml`

### UI Styling
- **MaterialDesignThemes** for icons (`PackIcon Kind="..."`)
- Custom styles in resource dictionaries: `CardStyle`, `NavButtonStyle`, `SuccessButtonStyle`, `DangerButtonStyle`, `GhostButtonStyle`, `InputTextBoxStyle`, `ItemRowStyle`, `SoftButtonStyle`, `ActionButtonStyle`
- Brushes: `PrimaryBrush`, `SidebarBackgroundBrush`, `ContentBackgroundBrush`, `PrimaryTextBrush`, `SecondaryTextBrush`, `MutedTextBrush`, `BorderBrush`, `InputBackgroundBrush`
- All brushes in `Colors.xaml` use `po:Freeze="True"` for performance

### Port Monitor Widget
- Sidebar bottom panel showing Apache/MySQL/SSL port status in real-time
- `MainViewModel` owns `ObservableCollection<PortStatusItem>` + `DispatcherTimer` (5s refresh)
- `MainViewModel` also holds `IServiceController` + `MariaDbService` refs for status sync
- `PortStatusItem` model: `Port`, `Label`, `IsInUse`, `ProcessName`, `ProcessId`, `IsSystemCritical`, computed `CanKill`
- Uses `IPortManager.GetPortConflictAsync()` per port, config ports read from `IConfigurationManager`
- Kill button calls `IPortManager.KillProcessOnPortAsync()`, then notifies the matching service controller (`GetStatusAsync`) so dashboard toggles update immediately
- Green dot = port in use (process name shown), gray dot = port free ("bos")

### Tray Icon & Single Instance
- 3-tier icon loading: file system → embedded resource → ExtractAssociatedIcon (for single-file publish)
- `EmbeddedResource` in .csproj for `app.ico`
- Status dot: green=all running, red=all stopped, orange=partial
- Tray menu: per-service Start/Stop/Restart, Start All/Stop All, localhost, phpMyAdmin, Root, Terminal, Show, Exit
- Menu items dynamically enabled/disabled based on service status
- `DestroyIcon` P/Invoke prevents GDI handle leaks
- Mutex-based single instance with P/Invoke `SetForegroundWindow`/`ShowWindow`

### First Run
- `FirstRunWindow` shown when `bin/` directory doesn't exist (detected by `OrchestratorService.IsFirstRunAsync`)
- Downloads latest Apache, PHP, MariaDB in sequence with progress bars
- VC++ Runtime check included — installs if incompatible
- All UI text in Turkish, includes app logo
- Version fetches run in parallel via `Task.WhenAll`

### Default Index Page
- `DefaultIndexPage.cs` contains the professional dashboard HTML (dark theme, ambient gradients)
- Created by `OrchestratorService.InitializeAsync()` if `www/index.php` doesn't exist
- Shows: Apache/PHP/MariaDB/System status cards, quick links (phpMyAdmin, phpinfo, docs), PHP extensions grid, server info table
- `/?phpinfo=1` opens phpinfo() as overlay with close button

## UI Language
Application UI text is in **Turkish**. Use Turkish for all user-facing strings (button labels, toast messages, status text, dialog messages, StatusMessage assignments).

## Important Files

| File | Purpose |
|------|---------|
| `App.xaml.cs` | DI container, startup, auto-start services, tray icon with status dot, single instance |
| `MainWindow.xaml` | Shell: sidebar nav + port monitor widget + content area + toast overlay |
| `DashboardViewModel.cs` | Main page: start/stop services, config shortcuts, MariaDB settings, quick fixes, phpMyAdmin install, DB import, Cloudflare Tunnel |
| `PhpViewModel.cs` | PHP page: version management (download/delete/switch), extensions, 10 PHP settings |
| `ApacheViewModel.cs` | Apache page: start/stop, port config, version management (download/delete/switch) |
| `UpdatesViewModel.cs` | Updates page: check all components for updates, download & apply updates |
| `SettingsViewModel.cs` | Settings page: ports, document root, auto-start, virtual hosts, SSL |
| `HostsFileViewModel.cs` | Hosts file page: add/remove managed host entries |
| `LogViewModel.cs` | Logs page: view Apache/MariaDB/PHP log files |
| `AppConfiguration.cs` | All persisted settings (ports, versions, PHP/MariaDB config values, auto-start flags) |
| `Defaults.cs` | All path constants (bin, config, www, apps, logs, etc.) — NO `data/` prefix |
| `DefaultIndexPage.cs` | Professional dashboard HTML for www/index.php |
| `PhpIniGenerator.cs` | Generates php.ini from PhpSettings model |
| `MariaDbConfigGenerator.cs` | Generates my.ini from MariaDbSettings model |
| `OrchestratorService.cs` | Coordinates service start/stop/restart across Apache+PHP+MariaDB |
| `ApacheService.cs` | Apache controller: start/stop, crash detection, DLL copy, config validation |
| `FirstRunWindow.xaml/.cs` | First-run setup: downloads Apache, PHP, MariaDB with progress UI |
| `ModernDialog.xaml/.cs` | Custom borderless dialog window (Info/Warning/Error/Confirm types) |
| `DialogService.cs` | Thread-safe dialog display using ModernDialog |
| `MainViewModel.cs` | Sidebar port monitor: timer, refresh, kill command with service status sync |
| `PortStatusItem.cs` | Port status model for sidebar widget |
| `NavigationService.cs` | Page navigation with ViewModel caching |
| `ToastService.cs` | Toast notifications with proper timer cleanup |
| `HealthCheckerService.cs` | Parallel health checks (8 checks via Task.WhenAll, includes VC++) |
| `FileLogWatcher.cs` | Log file watcher with debounce (150ms) and Span-based parsing |
| `VcRedistChecker.cs` | VC++ Runtime detection (registry + DLL), binary test, silent install |
| `VcRedistStatus.cs` | Model: IsCompatible, InstalledVersion, RequiredMinimumVersion |
| `VersionManagerService.cs` | Version install/uninstall/list, dynamic InstallPath recomputation on load |
| `build.ps1` | Build + publish + VC++ download + Inno Setup installer pipeline |
| `installer/ZaraGON.iss` | Inno Setup installer script (Turkish, admin, VC++ bundled, no restart) |

### Database Import
- `ImportDatabaseAsync()` in DashboardViewModel — OpenFileDialog for `.sql` files
- `PromptInput` asks for target DB name (empty = import to all databases)
- Runs `cmd.exe /C "mysql.exe -u root -P {port} [dbName] < file.sql"` (pipe via cmd)
- Database Tools section: 3-column layout — Oluştur | İçe Aktar | Yedekle

### Cloudflare Tunnel
- `Defaults.CloudflaredDir` = `"bin/cloudflared"`, `CloudflaredDownloadUrl` = GitHub latest release
- `ToggleTunnelAsync()` in DashboardViewModel — toggle command
- Auto-downloads `cloudflared.exe` on first use via `HttpClient`
- Runs `cloudflared.exe tunnel --url http://localhost:{ApachePort}`
- Parses `.trycloudflare.com` URL from stderr/stdout via async `DataReceived` events
- Copies URL to clipboard, shows toast
- `StopTunnel()` public method called from `App.xaml.cs` `OnExit` for cleanup
- `_tunnelProcess` field + `IsTunnelRunning` / `TunnelUrl` observable properties
- UI: toggle button in Hızlı İşlemler card, green dot + URL display when active

### Localhost Security Binding
- Apache: `Listen 127.0.0.1:{port}` (not `Listen {port}`) — prevents LAN access
- MariaDB: `bind-address=127.0.0.1` in `[mysqld]` section
- SSL port also bound to `127.0.0.1`
- External access only via Cloudflare Tunnel (safe, authenticated)

### Composer Install Guard
- `RunComposerInstallAsync` checks for `composer.json` in selected folder before running
- Shows toast warning if not found: "Seçilen klasörde composer.json bulunamadı"

## Coding Rules

1. **XAML bindings**: Always use `Mode=TwoWay, UpdateSourceTrigger=PropertyChanged` on editable TextBox/CheckBox
2. **Process.Start**: Always wrap in try-catch, show toast on failure. Always call `?.Dispose()` on returned Process
3. **Async commands**: Set `IsBusy = true` before try, `false` in finally
4. **Error reporting**: Always show toast or dialog in catch blocks, never silently swallow
5. **Config flow**: ViewModel -> AppConfiguration -> Save -> Generator uses settings -> Write file
6. **Tray icon**: Uses `DestroyIcon` P/Invoke to prevent GDI handle leaks; status dot: green=all running, red=all stopped, orange=partial
7. **No admin required**: `app.manifest` uses `asInvoker` (not `requireAdministrator`). Installer uses admin for C:\ install
8. **Turkish UI**: All user-facing strings must be in Turkish - StatusMessage, dialog titles/messages, toast text, button labels
9. **Version management**: After download/delete, refresh both InstalledVersions list and IsInstalled flags on AvailableVersions
10. **Dialogs**: Use `DialogService` methods (ShowError, ShowWarning, ShowInfo, Confirm, PromptInput) - never use MessageBox.Show directly
11. **HttpClient**: Use DI singleton `HttpClient` — never `new HttpClient()`. Use CancellationTokenSource for timeouts
12. **Brushes**: Use static frozen brushes in converters. Use `po:Freeze="True"` in XAML resource brushes
13. **Paths**: All runtime paths relative to basePath via `Defaults.cs` constants — NO `data/` prefix
14. **VC++ handling**: No pre-start dialogs — DLL copy in ApacheService handles it silently (XAMPP approach). Installer bundles VC++ Redistributable
15. **Namespace conflicts**: Use `SysDiag = System.Diagnostics` alias in Infrastructure project to avoid conflict with `ZaraGON.Infrastructure.Process`
16. **Port kill → status sync**: When killing a process on a port, always notify the matching service controller via `GetStatusAsync()` so dashboard toggles update
17. **Dynamic install paths**: Never assume `InstallPath` in `versions.json` is correct — `VersionManagerService.LoadVersionsFileAsync` recomputes from `basePath` on every load
18. **Conditional extensions**: Only enable PHP extensions/features (e.g., opcache) if the DLL file actually exists in the extension directory
19. **Localhost binding**: Apache and MariaDB must bind to `127.0.0.1` only — never listen on all interfaces for local dev

## Performance Optimizations Applied

- **Frozen brushes**: All converter brushes are static+frozen; all Colors.xaml brushes use `po:Freeze="True"`
- **Process disposal**: All `Process.Start()` return values disposed (14 locations)
- **ListView virtualization**: LogView uses `VirtualizingPanel.IsVirtualizing` + `Recycling` mode
- **Singleton ViewModels**: DashboardVM, ApacheVM, LogVM registered as singletons (prevent handler accumulation)
- **HttpClient singleton**: Shared instance via DI instead of per-request creation
- **Parallel startup**: Auto-start services, health checks, version fetches all use `Task.WhenAll`
- **NavigationService caching**: ViewModels cached after first creation
- **FileLogWatcher debounce**: 150ms debounce on FileSystemWatcher events, 3s timer interval, Span-based parsing

## Known Design Decisions

- `async void OnStartup` - WPF override, can't be changed to Task
- `.GetAwaiter().GetResult()` in `OnExit` - WPF OnExit is sync, no better option
- `_ = LoadAsync()` in ViewModel constructors - fire-and-forget initialization, exceptions caught in method
- `ObservableCollection` modifications happen on UI thread via dispatcher marshaling
- ViewModel event subscriptions to singleton services are not unsubscribed (acceptable for app-lifetime singletons)
- `ModernDialog.xaml.cs` uses using aliases (`WpfButton`, `WpfBrushes`, etc.) to resolve System.Drawing / System.Windows.Media namespace conflicts (both needed: System.Drawing for tray icon, WPF for UI)
- Snapshots/Profiles features removed - navigation entries no longer registered
- First-run detection checks for `bin/` directory existence (was `data/` before migration)
- Self-contained publish bundles .NET runtime (~174 MB exe) — no .NET install required on target
- VC++ Redistributable bundled in installer — installed silently, `NeedRestart()` returns False
- DLL copy from PHP dir to Apache bin dir handles VCRUNTIME incompatibility without user dialogs
- `DefaultIndexPage.cs` stores index.php content as raw string literal — only created if file doesn't exist
- `versions.json` `InstallPath` is auto-corrected on load — app can be moved to any directory without manual config edits
- Tunnel process killed via `Kill(entireProcessTree: true)` to ensure cloudflared child processes are also terminated
- OPcache conditionally enabled — PHP 8.5.x dev builds may not ship `php_opcache.dll`

## GitHub & Release Management

- **Repository**: `github.com/yenifont/ZaraGON` — public repo, source code NOT pushed (only README + .gitignore)
- **Distribution**: EXE published via GitHub Releases only (not source code)
- **Release flow**: `dotnet publish` → `gh release create vX.Y.Z ZaraGON.exe` with Turkish release notes
- **Update system**: `UpdatesViewModel` checks `https://api.github.com/repos/yenifont/ZaraGON/releases/latest` (unauthenticated)
  - Requires repo to be **public** for API to work
  - Parses `tag_name` for version, `browser_download_url` for .exe asset
  - App update opens browser to download URL (no in-app auto-update)
- **Git setup**: Orphan branch with clean history (no source code in git history). `.gitignore` ignores everything except `README.md` and `.gitignore`
- **gh CLI**: Downloaded to `D:\Zoragon\gh.exe` for release management, authenticated as `yenifont`
- **Version constant**: `Defaults.AppVersion` in `Defaults.cs` — must be bumped before each release
- **Tag format**: `v1.0.0` (semver with `v` prefix)

### Publishing a New Release
```bash
# 1. Bump version in Defaults.cs (AppVersion)
# 2. Build & publish
taskkill /IM ZaraGON.exe /F 2>nul
dotnet publish src/ZaraGON.UI/ZaraGON.UI.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
# 3. Create GitHub release
gh.exe release create vX.Y.Z publish/ZaraGON.exe --repo yenifont/ZaraGON --title "ZaraGON vX.Y.Z" --notes "Release notes..."
```

### Application Logging
- `ToastService` writes all errors and warnings to `logs/zoragon.log` with timestamps
- `OrchestratorService` starts a `FileLogWatcher` for `zoragon.log` (source: "ZaraGON")
- `LogViewModel` includes "ZaraGON" in source filter dropdown
- All application errors are visible in Log Viewer under "ZaraGON" source
