using ZoroKit.Application.ConfigGeneration;

namespace ZoroKit.Application.Tests.ConfigGeneration;

public class ApacheConfigGeneratorTests
{
    private readonly ApacheConfigGenerator _generator = new();

    private const string ServerRoot = @"C:\ZoroKit\bin\apache\2.4.66";
    private const string DocumentRoot = @"C:\ZoroKit\www";
    private const string PhpModulePath = @"C:\ZoroKit\bin\php\8.5.1\php8apache2_4.dll";
    private const string PhpPath = @"C:\ZoroKit\bin\php\8.5.1";
    private const string LogDir = @"C:\ZoroKit\logs\apache";
    private const string VhostsConfPath = @"C:\ZoroKit\config\apache\httpd-vhosts.conf";

    private string GenerateMinimal(
        int port = 80,
        string? sitesEnabledDir = null,
        bool sslEnabled = false,
        int sslPort = 443,
        string? sslDir = null,
        string? aliasDir = null)
    {
        return _generator.Generate(
            ServerRoot, DocumentRoot, port, PhpModulePath, PhpPath,
            LogDir, VhostsConfPath, sitesEnabledDir, sslEnabled, sslPort, sslDir, aliasDir);
    }

    [Fact]
    public void Generate_MinimalConfig_ContainsServerRoot()
    {
        var config = GenerateMinimal();
        Assert.Contains("ServerRoot \"C:/ZoroKit/bin/apache/2.4.66\"", config);
    }

    [Fact]
    public void Generate_MinimalConfig_ContainsDocumentRoot()
    {
        var config = GenerateMinimal();
        Assert.Contains("DocumentRoot \"C:/ZoroKit/www\"", config);
    }

    [Fact]
    public void Generate_MinimalConfig_ListenBindsToLocalhost()
    {
        var config = GenerateMinimal(port: 8080);
        Assert.Contains("Listen 127.0.0.1:8080", config);
    }

    [Fact]
    public void Generate_MinimalConfig_ContainsServerName()
    {
        var config = GenerateMinimal(port: 80);
        Assert.Contains("ServerName localhost:80", config);
    }

    [Fact]
    public void Generate_MinimalConfig_DoesNotContainSslModule()
    {
        var config = GenerateMinimal(sslEnabled: false);
        Assert.DoesNotContain("LoadModule ssl_module", config);
    }

    [Fact]
    public void Generate_SslEnabled_ContainsSslModule()
    {
        var config = GenerateMinimal(sslEnabled: true, sslPort: 443, sslDir: @"C:\ZoroKit\config\ssl");
        Assert.Contains("LoadModule ssl_module modules/mod_ssl.so", config);
    }

    [Fact]
    public void Generate_SslEnabled_ListensSslOnLocalhost()
    {
        var config = GenerateMinimal(sslEnabled: true, sslPort: 8443, sslDir: @"C:\ZoroKit\config\ssl");
        Assert.Contains("Listen 127.0.0.1:8443", config);
    }

    [Fact]
    public void Generate_SslEnabledWithoutDir_DoesNotContainSslModule()
    {
        var config = GenerateMinimal(sslEnabled: true, sslPort: 443, sslDir: null);
        Assert.DoesNotContain("LoadModule ssl_module", config);
    }

    [Fact]
    public void Generate_WithSitesEnabledDir_ContainsIncludeOptional()
    {
        var config = GenerateMinimal(sitesEnabledDir: @"C:\ZoroKit\config\apache\sites-enabled");
        Assert.Contains("IncludeOptional \"C:/ZoroKit/config/apache/sites-enabled/*.conf\"", config);
    }

    [Fact]
    public void Generate_WithoutSitesEnabledDir_DoesNotContainSitesInclude()
    {
        var config = GenerateMinimal(sitesEnabledDir: null);
        Assert.DoesNotContain("sites-enabled", config);
    }

    [Fact]
    public void Generate_WithAliasDir_ContainsAliasIncludeOptional()
    {
        var config = GenerateMinimal(aliasDir: @"C:\ZoroKit\config\apache\alias");
        Assert.Contains("IncludeOptional \"C:/ZoroKit/config/apache/alias/*.conf\"", config);
    }

    [Fact]
    public void Generate_WithoutAliasDir_DoesNotContainAliasInclude()
    {
        var config = GenerateMinimal(aliasDir: null);
        Assert.DoesNotContain("Application Aliases", config);
    }

    [Fact]
    public void Generate_PathsConvertedToForwardSlash()
    {
        var config = GenerateMinimal();
        // All Windows backslash paths should be converted
        Assert.DoesNotContain(@"C:\ZoroKit", config);
        Assert.Contains("C:/ZoroKit", config);
    }

    [Fact]
    public void Generate_ContainsPhpModule()
    {
        var config = GenerateMinimal();
        Assert.Contains("LoadModule php_module \"C:/ZoroKit/bin/php/8.5.1/php8apache2_4.dll\"", config);
    }

    [Fact]
    public void Generate_ContainsPhpIniDir()
    {
        var config = GenerateMinimal();
        Assert.Contains("PHPIniDir \"C:/ZoroKit/bin/php/8.5.1\"", config);
    }

    [Fact]
    public void Generate_ContainsAcceptFilterHttp()
    {
        var config = GenerateMinimal();
        Assert.Contains("AcceptFilter http none", config);
    }

    [Fact]
    public void Generate_ContainsAcceptFilterHttps()
    {
        var config = GenerateMinimal();
        Assert.Contains("AcceptFilter https none", config);
    }

    [Fact]
    public void Generate_ContainsVhostsInclude()
    {
        var config = GenerateMinimal();
        Assert.Contains("Include \"C:/ZoroKit/config/apache/httpd-vhosts.conf\"", config);
    }

    [Fact]
    public void Generate_ContainsErrorLog()
    {
        var config = GenerateMinimal();
        Assert.Contains("ErrorLog \"C:/ZoroKit/logs/apache/error.log\"", config);
    }

    [Fact]
    public void Generate_ContainsDirectoryIndexPhp()
    {
        var config = GenerateMinimal();
        Assert.Contains("DirectoryIndex index.php index.html", config);
    }
}
