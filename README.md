# ZaraGON

Modern, lightweight local development environment for Windows — like XAMPP, but built with a native WPF interface and modern tooling.

![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![Windows](https://img.shields.io/badge/Platform-Windows%20x64-0078D4?logo=windows)
![License](https://img.shields.io/github/license/yenifont/ZaraGON)

## Features

- **Apache HTTP Server** — Start/stop, version management, config generation, crash detection
- **PHP** — Multi-version support, extension manager, php.ini generator, 10+ configurable settings
- **MariaDB** — Start/stop, version management, database create/backup/import
- **phpMyAdmin** — Auto-installed and configured
- **Composer** — Bundled with automatic PHP path resolution
- **Virtual Hosts** — Auto virtual host management with `.test` TLD
- **SSL Support** — CA certificate bundle auto-downloaded
- **Hosts File Manager** — Add/remove local domain entries
- **Cloudflare Tunnel** — Expose localhost to the internet with one click
- **Log Viewer** — Real-time log monitoring (Apache, MariaDB, app logs)
- **Port Monitor** — Sidebar widget showing port status, conflict detection, process kill
- **Health Checks** — 8 parallel checks including VC++ runtime compatibility
- **Auto Updates** — Check for new versions via GitHub Releases
- **System Tray** — Status indicator, quick actions, minimize to tray
- **First Run Wizard** — Downloads and configures everything automatically

## Screenshots

<!-- Add screenshots here -->

## Installation

### Installer (Recommended)

1. Download `ZaraGON-Setup-1.0.0.exe` from [Releases](https://github.com/yenifont/ZaraGON/releases/latest)
2. Run the installer (includes VC++ Redistributable)
3. Launch ZaraGON — first run wizard will download Apache, PHP, and MariaDB

### Portable

1. Download `ZaraGON.exe` from [Releases](https://github.com/yenifont/ZaraGON/releases/latest)
2. Place it in a dedicated folder (e.g., `C:\ZaraGON`)
3. Run — all binaries and configs are created in the same directory

## Build from Source

**Requirements:** .NET 9 SDK, Windows x64

```bash
# Clone
git clone https://github.com/yenifont/ZaraGON.git
cd ZaraGON

# Build
dotnet build src/ZaraGON.UI/ZaraGON.UI.csproj

# Run
dotnet run --project src/ZaraGON.UI/ZaraGON.UI.csproj

# Publish (self-contained single-file + installer)
powershell -ExecutionPolicy Bypass -File build.ps1
```

## Architecture

```
src/
  ZaraGON.Core/           # Domain models, interfaces, constants
  ZaraGON.Application/    # Business logic, config generators
  ZaraGON.Infrastructure/ # File system, process, network implementations
  ZaraGON.UI/             # WPF app, views, viewmodels, DI wiring
```

**Stack:** C# 13, .NET 9, WPF, CommunityToolkit.Mvvm, MaterialDesignThemes

## Runtime Directory Layout

```
ZaraGON/
  bin/
    apache/    # Apache installations (multi-version)
    php/       # PHP installations (multi-version)
    mariadb/   # MariaDB installations (multi-version)
    composer/  # Composer binary
    cloudflared/  # Cloudflare Tunnel binary
  config/
    apache/    # httpd.conf, virtual hosts, aliases
    php/       # php.ini
    mariadb/   # my.ini
    ssl/       # SSL certificates, CA bundle
    zoragon.json  # App configuration
  www/         # Document root
  apps/
    phpmyadmin/   # phpMyAdmin (auto-installed)
  logs/        # Apache, MariaDB, app logs
  mariadb/     # MariaDB data directory
  backups/     # Database backups
```

## Security

- All services bind to `127.0.0.1` only (not accessible from network)
- No admin privileges required to run (installer uses admin for `C:\` install only)
- VC++ Runtime handled silently via DLL copy (no pre-start dialogs)

## Language

The application UI is in **Turkish**.

## License

This project is open source. See [LICENSE](LICENSE) for details.

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes
4. Push to the branch
5. Open a Pull Request
