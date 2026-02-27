using ZoroKit.Application.ConfigGeneration;
using ZoroKit.Core.Models;

namespace ZoroKit.Application.Tests.ConfigGeneration;

public class PhpIniGeneratorTests
{
    private readonly PhpIniGenerator _generator = new();

    private const string ExtensionDir = @"C:\ZoroKit\bin\php\8.5.1\ext";

    [Fact]
    public void Generate_DefaultSettings_ContainsDefaultMemoryLimit()
    {
        var ini = _generator.Generate(ExtensionDir, []);
        Assert.Contains("memory_limit = 256M", ini);
    }

    [Fact]
    public void Generate_DefaultSettings_ContainsDefaultMaxExecutionTime()
    {
        var ini = _generator.Generate(ExtensionDir, []);
        Assert.Contains("max_execution_time = 300", ini);
    }

    [Fact]
    public void Generate_DefaultSettings_DisplayErrorsOn()
    {
        var ini = _generator.Generate(ExtensionDir, []);
        Assert.Contains("display_errors = On", ini);
    }

    [Fact]
    public void Generate_CustomSettings_AppliesMemoryLimit()
    {
        var settings = new PhpSettings { MemoryLimit = "512M" };
        var ini = _generator.Generate(ExtensionDir, [], settings: settings);
        Assert.Contains("memory_limit = 512M", ini);
    }

    [Fact]
    public void Generate_CustomSettings_AppliesMaxExecutionTime()
    {
        var settings = new PhpSettings { MaxExecutionTime = 60 };
        var ini = _generator.Generate(ExtensionDir, [], settings: settings);
        Assert.Contains("max_execution_time = 60", ini);
    }

    [Fact]
    public void Generate_CustomSettings_AppliesMaxInputTime()
    {
        var settings = new PhpSettings { MaxInputTime = 120 };
        var ini = _generator.Generate(ExtensionDir, [], settings: settings);
        Assert.Contains("max_input_time = 120", ini);
    }

    [Fact]
    public void Generate_CustomSettings_AppliesUploadMaxFilesize()
    {
        var settings = new PhpSettings { UploadMaxFilesize = "64M" };
        var ini = _generator.Generate(ExtensionDir, [], settings: settings);
        Assert.Contains("upload_max_filesize = 64M", ini);
    }

    [Fact]
    public void Generate_CustomSettings_AppliesPostMaxSize()
    {
        var settings = new PhpSettings { PostMaxSize = "64M" };
        var ini = _generator.Generate(ExtensionDir, [], settings: settings);
        Assert.Contains("post_max_size = 64M", ini);
    }

    [Fact]
    public void Generate_CustomSettings_AppliesMaxFileUploads()
    {
        var settings = new PhpSettings { MaxFileUploads = 50 };
        var ini = _generator.Generate(ExtensionDir, [], settings: settings);
        Assert.Contains("max_file_uploads = 50", ini);
    }

    [Fact]
    public void Generate_CustomSettings_AppliesMaxInputVars()
    {
        var settings = new PhpSettings { MaxInputVars = 5000 };
        var ini = _generator.Generate(ExtensionDir, [], settings: settings);
        Assert.Contains("max_input_vars = 5000", ini);
    }

    [Fact]
    public void Generate_DisplayErrorsOff_WritesOff()
    {
        var settings = new PhpSettings { DisplayErrors = false };
        var ini = _generator.Generate(ExtensionDir, [], settings: settings);
        Assert.Contains("display_errors = Off", ini);
        Assert.Contains("display_startup_errors = Off", ini);
    }

    [Fact]
    public void Generate_CustomTimezone_Applies()
    {
        var settings = new PhpSettings { DateTimezone = "Europe/Istanbul" };
        var ini = _generator.Generate(ExtensionDir, [], settings: settings);
        Assert.Contains("date.timezone = Europe/Istanbul", ini);
    }

    [Fact]
    public void Generate_CustomErrorReporting_Applies()
    {
        var settings = new PhpSettings { ErrorReporting = "E_ALL & ~E_NOTICE" };
        var ini = _generator.Generate(ExtensionDir, [], settings: settings);
        Assert.Contains("error_reporting = E_ALL & ~E_NOTICE", ini);
    }

    [Fact]
    public void Generate_WithExtensions_WritesExtensionLines()
    {
        var extensions = new[] { "php_curl.dll", "php_mbstring.dll", "php_openssl.dll" };
        var ini = _generator.Generate(ExtensionDir, extensions);

        Assert.Contains("extension=php_curl.dll", ini);
        Assert.Contains("extension=php_mbstring.dll", ini);
        Assert.Contains("extension=php_openssl.dll", ini);
    }

    [Fact]
    public void Generate_Extensions_AreSortedAlphabetically()
    {
        var extensions = new[] { "php_zip.dll", "php_curl.dll", "php_mbstring.dll" };
        var ini = _generator.Generate(ExtensionDir, extensions);

        var curlIndex = ini.IndexOf("extension=php_curl.dll");
        var mbstringIndex = ini.IndexOf("extension=php_mbstring.dll");
        var zipIndex = ini.IndexOf("extension=php_zip.dll");

        Assert.True(curlIndex < mbstringIndex, "curl should come before mbstring");
        Assert.True(mbstringIndex < zipIndex, "mbstring should come before zip");
    }

    [Fact]
    public void Generate_EmptyExtensions_NoExtensionLines()
    {
        var ini = _generator.Generate(ExtensionDir, []);
        Assert.DoesNotContain("extension=", ini);
    }

    [Fact]
    public void Generate_WithCaCertPath_SetsCurlAndOpenssl()
    {
        // Create a temp file to pass the File.Exists check
        var tempFile = Path.GetTempFileName();
        try
        {
            var ini = _generator.Generate(ExtensionDir, [], caCertPath: tempFile);
            var normalizedPath = tempFile.Replace('\\', '/');
            Assert.Contains($"curl.cainfo = \"{normalizedPath}\"", ini);
            Assert.Contains($"openssl.cafile = \"{normalizedPath}\"", ini);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Generate_WithoutCaCertPath_EmptyCaFields()
    {
        var ini = _generator.Generate(ExtensionDir, [], caCertPath: null);
        Assert.Contains("curl.cainfo = \"\"", ini);
        Assert.Contains("openssl.cafile = \"\"", ini);
    }

    [Fact]
    public void Generate_ExtensionDirPathNormalized()
    {
        var ini = _generator.Generate(@"C:\ZoroKit\bin\php\8.5.1\ext", []);
        Assert.Contains("extension_dir = \"C:/ZoroKit/bin/php/8.5.1/ext\"", ini);
        Assert.DoesNotContain(@"extension_dir = ""C:\", ini);
    }

    [Fact]
    public void Generate_OpcacheNotAvailable_ShowsNotAvailableComment()
    {
        // ExtensionDir doesn't actually exist, so php_opcache.dll won't be found
        var ini = _generator.Generate(@"C:\nonexistent\ext", []);
        Assert.Contains("OPcache not available for this PHP build", ini);
        Assert.DoesNotContain("zend_extension=opcache", ini);
    }

    [Fact]
    public void Generate_OpcacheAvailable_WritesOpcacheBlock()
    {
        // Create a temp directory with php_opcache.dll to pass the File.Exists check
        var tempDir = Path.Combine(Path.GetTempPath(), "ZoroKit_test_ext_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var opcachePath = Path.Combine(tempDir, "php_opcache.dll");
        File.WriteAllText(opcachePath, "fake");
        try
        {
            var ini = _generator.Generate(tempDir, []);
            Assert.Contains("zend_extension=opcache", ini);
            Assert.Contains("opcache.enable=1", ini);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Generate_NullSettings_UsesDefaults()
    {
        var ini = _generator.Generate(ExtensionDir, [], settings: null);

        // Verify default PhpSettings values are used
        Assert.Contains("memory_limit = 256M", ini);
        Assert.Contains("max_execution_time = 300", ini);
        Assert.Contains("max_input_time = 300", ini);
        Assert.Contains("upload_max_filesize = 128M", ini);
        Assert.Contains("post_max_size = 128M", ini);
        Assert.Contains("max_file_uploads = 20", ini);
        Assert.Contains("max_input_vars = 1000", ini);
        Assert.Contains("display_errors = On", ini);
        Assert.Contains("error_reporting = E_ALL", ini);
        Assert.Contains("date.timezone = UTC", ini);
    }
}
