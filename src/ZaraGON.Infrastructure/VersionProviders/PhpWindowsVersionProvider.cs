using System.Text.RegularExpressions;
using ZaraGON.Core.Enums;
using ZaraGON.Core.Interfaces.Providers;
using ZaraGON.Core.Models;

namespace ZaraGON.Infrastructure.VersionProviders;

public sealed partial class PhpWindowsVersionProvider : IVersionProvider
{
    private readonly HttpClient _httpClient;
    private const string DownloadPageUrl = "https://windows.php.net/download/";
    private const string BaseDownloadUrl = "https://windows.php.net/downloads/releases/";
    private const string ArchivePageUrl = "https://windows.php.net/downloads/releases/archives/";

    public ServiceType ServiceType => ServiceType.Php;

    public PhpWindowsVersionProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ServiceVersion>> FetchAvailableVersionsAsync(CancellationToken ct = default)
    {
        // Fetch current releases and archives in parallel
        var currentTask = FetchFromPageAsync(DownloadPageUrl, BaseDownloadUrl, ct);
        var archiveTask = FetchFromPageAsync(ArchivePageUrl, ArchivePageUrl, ct);

        await Task.WhenAll(currentTask, archiveTask);

        var currentVersions = await currentTask;
        var archiveVersions = await archiveTask;

        // Start with current versions (newest first, as they appear on the page)
        var versions = new List<ServiceVersion>(currentVersions);

        // From archives: group by major.minor, keep only the latest patch per series
        var currentMajorMinors = new HashSet<string>(
            currentVersions.Select(v => GetMajorMinor(v.Version)));

        var latestArchiveByMinor = archiveVersions
            .GroupBy(v => GetMajorMinor(v.Version))
            .Where(g => !currentMajorMinors.Contains(g.Key))
            .Select(g => g.OrderByDescending(v => ParseVersion(v.Version)).First())
            .OrderByDescending(v => ParseVersion(v.Version));

        versions.AddRange(latestArchiveByMinor);

        return versions;
    }

    private async Task<List<ServiceVersion>> FetchFromPageAsync(string pageUrl, string baseDownloadUrl, CancellationToken ct)
    {
        var versions = new List<ServiceVersion>();

        try
        {
            var html = await _httpClient.GetStringAsync(pageUrl, ct);

            // Match patterns like: php-8.3.29-Win32-vs16-x64.zip or php-7.0.0-Win32-VC14-x64.zip
            var matches = PhpTsZipRegex().Matches(html);

            foreach (Match match in matches)
            {
                var version = match.Groups["version"].Value;
                var vsVersion = match.Groups["vs"].Value;
                var fileName = match.Value;

                var downloadUrl = $"{baseDownloadUrl}{fileName}";

                // Avoid duplicates
                if (versions.Any(v => v.Version == version))
                    continue;

                versions.Add(new ServiceVersion
                {
                    ServiceType = ServiceType.Php,
                    Version = version,
                    DownloadUrl = downloadUrl,
                    FileName = fileName,
                    VsVersion = vsVersion,
                    IsThreadSafe = true,
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

    private static string GetMajorMinor(string version)
    {
        var parts = version.Split('.');
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : version;
    }

    private static Version ParseVersion(string version)
    {
        return System.Version.TryParse(version, out var v) ? v : new Version(0, 0);
    }

    // Matches both modern (vs16) and legacy (VC14, VC11) compiler tags
    [GeneratedRegex(@"php-(?<version>[\d.]+)-Win32-(?<vs>v[cs]\d+)-x64\.zip", RegexOptions.IgnoreCase)]
    private static partial Regex PhpTsZipRegex();
}
