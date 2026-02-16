using System.Text.RegularExpressions;
using ZaraGON.Core.Enums;
using ZaraGON.Core.Interfaces.Providers;
using ZaraGON.Core.Models;

namespace ZaraGON.Infrastructure.VersionProviders;

public sealed partial class ApacheLoungeVersionProvider : IVersionProvider
{
    private readonly HttpClient _httpClient;
    private const string DownloadPageUrl = "https://www.apachelounge.com/download/";

    public ServiceType ServiceType => ServiceType.Apache;

    public ApacheLoungeVersionProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ServiceVersion>> FetchAvailableVersionsAsync(CancellationToken ct = default)
    {
        var versions = new List<ServiceVersion>();

        try
        {
            var html = await _httpClient.GetStringAsync(DownloadPageUrl, ct);

            // Match patterns like: httpd-2.4.62-250207-win64-VS17.zip
            var matches = ApacheZipRegex().Matches(html);

            foreach (Match match in matches)
            {
                var version = match.Groups["version"].Value;
                var build = match.Groups["build"].Value;
                var vsVersion = match.Groups["vs"].Value;
                var fileName = match.Value;

                // Build full download URL
                var downloadUrl = fileName.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? fileName
                    : $"{DownloadPageUrl}{fileName}";

                // Extract href if this was inside an href attribute
                var hrefMatch = HrefRegex().Match(html, Math.Max(0, match.Index - 200));
                if (hrefMatch.Success && hrefMatch.Index < match.Index)
                {
                    var href = hrefMatch.Groups["url"].Value;
                    if (href.Contains(fileName))
                        downloadUrl = href.StartsWith("http") ? href : $"https://www.apachelounge.com{href}";
                }

                versions.Add(new ServiceVersion
                {
                    ServiceType = ServiceType.Apache,
                    Version = version,
                    DownloadUrl = downloadUrl,
                    FileName = fileName,
                    VsVersion = vsVersion,
                    Architecture = "x64"
                });
            }
        }
        catch (HttpRequestException)
        {
            // Network error - return empty list
        }

        return versions;
    }

    [GeneratedRegex(@"httpd-(?<version>[\d.]+)-(?<build>\d+)-win64-(?<vs>VS\d+)\.zip", RegexOptions.IgnoreCase)]
    private static partial Regex ApacheZipRegex();

    [GeneratedRegex(@"href=""(?<url>[^""]*?)""", RegexOptions.IgnoreCase)]
    private static partial Regex HrefRegex();
}
