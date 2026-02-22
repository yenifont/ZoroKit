namespace ZaraGON.Core.Constants;

public static class Defaults
{
    public const int ApachePort = 80;
    public const int ApacheSslPort = 443;
    public const int MySqlPort = 3306;
    public const string DocumentRoot = "www";
    public const string ConfigDir = "config";
    public const string BinDir = "bin";
    public const string LogDir = "logs";
    public const string ApacheConfigDir = "config/apache";
    public const string SitesEnabledDir = "config/apache/sites-enabled";
    public const string SslDir = "config/ssl";
    public const string PhpConfigDir = "config/php";
    public const string MariaDbConfigDir = "config/mariadb";
    public const string MariaDbDataDir = "mariadb";
    public const string ApacheAliasDir = "config/apache/alias";
    public const string AppsDir = "apps";
    public const string ComposerDir = "bin/composer";
    public const string VirtualHostTld = ".test";
    /// <summary>İlk kurulumda otomatik eklenecek varsayılan hostname (ana www'ü açar).</summary>
    public const string DefaultZaragonHostname = "zaragon.test";
    public const string MainConfigFile = "config/zoragon.json";
    public const string VersionsFile = "config/versions.json";
    public const string DefaultPhpInfo = "<?php phpinfo(); ?>"; // Legacy fallback
    public static string DefaultIndexPhp => DefaultIndexPage.Content;
    public static readonly string[] SystemCriticalProcesses =
    [
        "svchost", "lsass", "csrss", "wininit", "services", "smss",
        "winlogon", "dwm", "explorer", "system", "taskmgr"
    ];

    public const string CloudflaredDir = "bin/cloudflared";
    public const string CloudflaredDownloadUrl = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe";

    public const string VcRedistDownloadUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe";
    public const string VcRedistFileName = "vc_redist.x64.exe";

    // App update (GitHub)
    public const string AppVersion = "1.0.6";
    public const string GitHubOwner = "yenifont";
    public const string GitHubRepo = "ZaraGON";
    public const string GitHubReleasesApi = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

    public static readonly string[] DefaultPhpExtensions =
    [
        "curl", "mbstring", "openssl", "pdo_mysql", "mysqli", "gd",
        "zip", "fileinfo", "intl", "sodium", "exif"
    ];
}
