using ZoroKit.Application.ConfigGeneration;
using ZoroKit.Core.Models;

namespace ZoroKit.Application.Tests.ConfigGeneration;

public class MariaDbConfigGeneratorTests
{
    private readonly MariaDbConfigGenerator _generator = new();

    private const string BaseDir = @"C:\ZoroKit\bin\mariadb\12.2.2";
    private const string DataDir = @"C:\ZoroKit\mariadb";
    private const string LogDir = @"C:\ZoroKit\logs";

    private string GenerateDefault(int port = 3306, MariaDbSettings? settings = null)
    {
        return _generator.Generate(BaseDir, DataDir, port, LogDir, settings);
    }

    [Fact]
    public void Generate_DefaultSettings_ContainsPort()
    {
        var config = GenerateDefault(port: 3306);
        Assert.Contains("port=3306", config);
    }

    [Fact]
    public void Generate_CustomPort_AppliesInMysqldSection()
    {
        var config = GenerateDefault(port: 3307);
        // Should appear in [mysqld] section
        Assert.Contains("port=3307", config);
    }

    [Fact]
    public void Generate_PortInClientSection()
    {
        var config = GenerateDefault(port: 3308);
        // Port should appear twice — once in [mysqld], once in [client]
        var firstIndex = config.IndexOf("port=3308");
        var secondIndex = config.IndexOf("port=3308", firstIndex + 1);
        Assert.True(secondIndex > firstIndex, "Port should appear in both [mysqld] and [client] sections");
    }

    [Fact]
    public void Generate_BindAddressAlwaysLocalhost()
    {
        var config = GenerateDefault();
        Assert.Contains("bind-address=127.0.0.1", config);
    }

    [Fact]
    public void Generate_DefaultSettings_ContainsDefaultInnodbBufferPoolSize()
    {
        var config = GenerateDefault();
        Assert.Contains("innodb_buffer_pool_size=128M", config);
    }

    [Fact]
    public void Generate_DefaultSettings_ContainsDefaultMaxConnections()
    {
        var config = GenerateDefault();
        Assert.Contains("max_connections=151", config);
    }

    [Fact]
    public void Generate_DefaultSettings_ContainsDefaultMaxAllowedPacket()
    {
        var config = GenerateDefault();
        Assert.Contains("max_allowed_packet=16M", config);
    }

    [Fact]
    public void Generate_CustomSettings_AppliesInnodbBufferPoolSize()
    {
        var settings = new MariaDbSettings { InnodbBufferPoolSize = "256M" };
        var config = GenerateDefault(settings: settings);
        Assert.Contains("innodb_buffer_pool_size=256M", config);
    }

    [Fact]
    public void Generate_CustomSettings_AppliesMaxConnections()
    {
        var settings = new MariaDbSettings { MaxConnections = 300 };
        var config = GenerateDefault(settings: settings);
        Assert.Contains("max_connections=300", config);
    }

    [Fact]
    public void Generate_CustomSettings_AppliesMaxAllowedPacket()
    {
        var settings = new MariaDbSettings { MaxAllowedPacket = "64M" };
        var config = GenerateDefault(settings: settings);
        Assert.Contains("max_allowed_packet=64M", config);
    }

    [Fact]
    public void Generate_ErrorLogPathIsAbsoluteAndForwardSlash()
    {
        var config = GenerateDefault();
        Assert.Contains("log_error=\"C:/ZoroKit/logs/mariadb-error.log\"", config);
    }

    [Fact]
    public void Generate_PathsConvertedToForwardSlash()
    {
        var config = GenerateDefault();
        Assert.Contains("basedir=\"C:/ZoroKit/bin/mariadb/12.2.2\"", config);
        Assert.Contains("datadir=\"C:/ZoroKit/mariadb\"", config);
        Assert.DoesNotContain(@"C:\ZoroKit", config);
    }

    [Fact]
    public void Generate_ContainsUtf8mb4CharacterSet()
    {
        var config = GenerateDefault();
        Assert.Contains("character-set-server=utf8mb4", config);
        Assert.Contains("collation-server=utf8mb4_unicode_ci", config);
    }

    [Fact]
    public void Generate_ContainsSkipNameResolve()
    {
        var config = GenerateDefault();
        Assert.Contains("skip-name-resolve", config);
    }

    [Fact]
    public void Generate_NullSettings_UsesDefaults()
    {
        var config = _generator.Generate(BaseDir, DataDir, 3306, LogDir, settings: null);

        Assert.Contains("innodb_buffer_pool_size=128M", config);
        Assert.Contains("max_connections=151", config);
        Assert.Contains("max_allowed_packet=16M", config);
    }

    [Fact]
    public void Generate_ContainsMysqldAndClientSections()
    {
        var config = GenerateDefault();
        Assert.Contains("[mysqld]", config);
        Assert.Contains("[client]", config);
    }
}
