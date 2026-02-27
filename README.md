<div align="center">

# ZoroKit

**Modern, lightweight local development environment for Windows.**

*Apache + PHP + MariaDB — one click, zero configuration.*

[![.NET 9](https://img.shields.io/badge/.NET_9-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Windows x64](https://img.shields.io/badge/Windows_x64-0078D4?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/yenifont/ZoroKit/releases/latest)
[![GitHub Release](https://img.shields.io/github/v/release/yenifont/ZoroKit?style=for-the-badge&logo=github&color=24292e)](https://github.com/yenifont/ZoroKit/releases/latest)
[![GitHub Downloads](https://img.shields.io/github/downloads/yenifont/ZoroKit/total?style=for-the-badge&logo=github&color=24292e)](https://github.com/yenifont/ZoroKit/releases)

[Download](https://github.com/yenifont/ZoroKit/releases/latest) · [Report Bug](https://github.com/yenifont/ZoroKit/issues) · [Request Feature](https://github.com/yenifont/ZoroKit/issues)

</div>

---

## About

ZoroKit is a native Windows application for managing a local web development stack. It replaces tools like XAMPP, WampServer, and Laragon with a modern WPF interface, automatic configuration, and multi-version support for all components.

No manual config editing. No terminal commands. Install, click Start, and begin coding.

## Features

### Core Stack
| Component | Capabilities |
|-----------|-------------|
| **Apache** | Start/stop, multi-version management, automatic httpd.conf generation, crash detection with error log parsing |
| **PHP** | Multi-version support, extension manager, php.ini generator, 10+ configurable settings (OPcache, upload limits, etc.) |
| **MariaDB** | Start/stop, multi-version management, database create/import/backup, automatic my.ini generation |
| **phpMyAdmin** | One-click install, auto-configured with current MariaDB port |
| **Composer** | Bundled binary, automatic PHP path resolution, composer.json validation |

### Developer Tools
- **Virtual Hosts** — Automatic `.test` domain creation from project folders (e.g., `myproject.test`)
- **Hosts File Manager** — Add/remove local domains with automatic vhost generation and Apache reload
- **SSL Certificates** — CA certificate bundle auto-downloaded for HTTPS support
- **Cloudflare Tunnel** — Expose localhost to the internet with one click (auto-downloads `cloudflared`)
- **Database Tools** — Create databases, import `.sql` files, schedule backups — all from the dashboard

### Monitoring & Diagnostics
- **Real-time Log Viewer** — Apache, MariaDB, and application logs with source filtering
- **Port Monitor** — Sidebar widget showing port status, conflict detection, and process kill
- **Health Checks** — 8 parallel diagnostic checks including VC++ runtime compatibility
- **Port Conflict Resolution** — Automatic detection and reassignment on startup

### Quality of Life
- **First Run Wizard** — Downloads and configures Apache, PHP, and MariaDB automatically
- **Auto Updates** — Checks GitHub Releases for new versions
- **System Tray** — Status indicator (green/orange/red), quick actions, minimize to tray
- **Auto-Start** — Optionally start services with Windows
- **Portable** — Move the entire directory to another location; paths auto-correct on next launch

## Installation

### Installer (Recommended)

1. Download **`ZoroKit-Setup.exe`** from the [latest release](https://github.com/yenifont/ZoroKit/releases/latest)
2. Run the installer — VC++ Redistributable is bundled and installed silently
3. Launch ZoroKit — the first run wizard downloads everything automatically

### Portable

1. Download **`ZoroKit.exe`** from the [latest release](https://github.com/yenifont/ZoroKit/releases/latest)
2. Place it in a dedicated folder (e.g., `C:\ZoroKit`)
3. Run — all binaries and configurations are created in the same directory

> **Note:** The portable EXE is a self-contained single-file publish (~174 MB) — no .NET runtime installation required.

## Build from Source

**Requirements:** [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0), Windows x64

```bash
# Clone the repository
git clone https://github.com/yenifont/ZoroKit.git
cd ZoroKit

# Build
dotnet build src/ZoroKit.UI/ZoroKit.UI.csproj

# Run
dotnet run --project src/ZoroKit.UI/ZoroKit.UI.csproj

# Run tests
dotnet test tests/zorokit.application.Tests/zorokit.application.Tests.csproj

# Publish self-contained EXE + create installer
powershell -ExecutionPolicy Bypass -File build.ps1
```

## Architecture

ZoroKit follows **Clean Architecture** with the MVVM pattern:

```
src/
  ZoroKit.Core/              # Domain models, interfaces, enums, constants
  zorokit.application/        # Business logic, config generators, services
  ZoroKit.Infrastructure/     # Platform implementations (file system, process, network)
  ZoroKit.UI/                 # WPF application (views, viewmodels, DI wiring)

tests/
  zorokit.application.Tests/  # xUnit tests (config generation, validation)
```

```
Dependency flow:  UI  →  Application + Infrastructure  →  Core
```

**Tech Stack:** C# 13 · .NET 9 · WPF · CommunityToolkit.Mvvm · MaterialDesignThemes · Inno Setup

## Runtime Directory Structure

```
C:\ZoroKit\
├── ZoroKit.exe
├── bin/
│   ├── apache/2.4.x/        # Apache installations (multi-version)
│   ├── php/8.x.x/           # PHP installations (multi-version)
│   ├── mariadb/11.x.x/      # MariaDB installations (multi-version)
│   ├── composer/             # Composer binary
│   └── cloudflared/          # Cloudflare Tunnel binary
├── config/
│   ├── apache/               # httpd.conf, httpd-vhosts.conf
│   │   ├── sites-enabled/    # Virtual host configs
│   │   └── alias/            # Apache alias configs
│   ├── php/                  # php.ini
│   ├── mariadb/              # my.ini
│   ├── ssl/                  # SSL certificates, CA bundle
│   ├── zoragon.json          # Application configuration
│   └── versions.json         # Installed version registry
├── www/                      # Document root (default index.php dashboard)
├── apps/
│   └── phpmyadmin/           # phpMyAdmin (auto-installed)
├── logs/                     # Apache, MariaDB, application logs
├── mariadb/                  # MariaDB data directory
├── backups/                  # Database backups
└── temp/                     # Temporary downloads
```

## Security

| Measure | Detail |
|---------|--------|
| **Localhost binding** | Apache listens on `127.0.0.1:80` — not accessible from the local network |
| **MariaDB binding** | `bind-address=127.0.0.1` — database not exposed to LAN |
| **No admin required** | Runs as standard user (`asInvoker`). Hosts file edits use UAC elevation fallback |
| **VC++ Runtime** | Handled silently via DLL copy — no pre-start dialogs or manual installs |
| **External access** | Only through Cloudflare Tunnel (authenticated, encrypted) |

## Contributing

Contributions are welcome! Here's how to get started:

1. **Fork** the repository
2. **Create** a feature branch (`git checkout -b feature/my-feature`)
3. **Commit** your changes with a descriptive message
4. **Push** to your branch (`git push origin feature/my-feature`)
5. **Open** a Pull Request

Please make sure tests pass before submitting:
```bash
dotnet test tests/zorokit.application.Tests/zorokit.application.Tests.csproj
```

## License

This project is open source. See the [LICENSE](LICENSE) file for details.

---

<div align="center">

**[Download ZoroKit](https://github.com/yenifont/ZoroKit/releases/latest)**

Made with care for the PHP developer community.

</div>
