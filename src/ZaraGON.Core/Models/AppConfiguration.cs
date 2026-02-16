namespace ZaraGON.Core.Models;

public sealed class AppConfiguration
{
    public int ApachePort { get; set; } = 8080;
    public int ApacheSslPort { get; set; } = 8443;
    public int MySqlPort { get; set; } = 3306;
    public string DocumentRoot { get; set; } = "www";
    public string ActiveApacheVersion { get; set; } = string.Empty;
    public string ActivePhpVersion { get; set; } = string.Empty;
    public string ActiveMariaDbVersion { get; set; } = string.Empty;
    public bool AutoStartApache { get; set; }
    public bool AutoStartMariaDb { get; set; }
    public bool AutoVirtualHosts { get; set; } = true;
    public string VirtualHostTld { get; set; } = ".test";
    public bool SslEnabled { get; set; }
    public bool RunOnWindowsStartup { get; set; }
    public bool AddToSystemPath { get; set; }
    public string Theme { get; set; } = "Dark";
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    // PHP Settings
    public string PhpMemoryLimit { get; set; } = "256M";
    public string PhpUploadMaxFilesize { get; set; } = "128M";
    public string PhpPostMaxSize { get; set; } = "128M";
    public int PhpMaxExecutionTime { get; set; } = 300;
    public int PhpMaxInputTime { get; set; } = 300;
    public int PhpMaxFileUploads { get; set; } = 20;
    public int PhpMaxInputVars { get; set; } = 1000;
    public bool PhpDisplayErrors { get; set; } = true;
    public string PhpErrorReporting { get; set; } = "E_ALL";
    public string PhpDateTimezone { get; set; } = "UTC";

    // MariaDB Settings
    public string MariaDbInnodbBufferPoolSize { get; set; } = "128M";
    public int MariaDbMaxConnections { get; set; } = 151;
    public string MariaDbMaxAllowedPacket { get; set; } = "16M";
}
