using System.Text.Json;
using ZaraGON.Core.Enums;
using ZaraGON.Core.Interfaces.Providers;
using ZaraGON.Core.Models;

namespace ZaraGON.Infrastructure.VersionProviders;

public sealed class MariaDbVersionProvider : IVersionProvider
{
    private readonly HttpClient _httpClient;
    private const string ApiUrl = "https://downloads.mariadb.org/rest-api/mariadb/";

    public ServiceType ServiceType => ServiceType.MariaDb;

    public MariaDbVersionProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ServiceVersion>> FetchAvailableVersionsAsync(CancellationToken ct = default)
    {
        var versions = new List<ServiceVersion>();

        try
        {
            var json = await _httpClient.GetStringAsync(ApiUrl, ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("major_releases", out var releases))
                return versions;

            foreach (var release in releases.EnumerateArray())
            {
                if (!release.TryGetProperty("release_id", out var idProp))
                    continue;

                var majorVersion = idProp.GetString();
                if (string.IsNullOrEmpty(majorVersion))
                    continue;

                // Only include stable releases
                if (release.TryGetProperty("release_status", out var statusProp))
                {
                    var status = statusProp.GetString() ?? "";
                    if (!status.Contains("Stable", StringComparison.OrdinalIgnoreCase) &&
                        !status.Contains("Old Stable", StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                // Fetch the latest patch version for this major release
                var latestVersion = await GetLatestPatchVersionAsync(majorVersion, ct);
                if (string.IsNullOrEmpty(latestVersion))
                    continue;

                var downloadUrl = $"https://archive.mariadb.org/mariadb-{latestVersion}/winx64-packages/mariadb-{latestVersion}-winx64.zip";

                versions.Add(new ServiceVersion
                {
                    ServiceType = ServiceType.MariaDb,
                    Version = latestVersion,
                    DownloadUrl = downloadUrl,
                    FileName = $"mariadb-{latestVersion}-winx64.zip",
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

    private async Task<string?> GetLatestPatchVersionAsync(string majorVersion, CancellationToken ct)
    {
        try
        {
            var json = await _httpClient.GetStringAsync($"{ApiUrl}{majorVersion}/", ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("releases", out var releases))
                return null;

            // releases is an object keyed by version string; first key is the latest
            foreach (var release in releases.EnumerateObject())
            {
                var version = release.Name;
                // Skip RC and Preview releases
                var name = "";
                if (release.Value.TryGetProperty("release_name", out var nameProp))
                    name = nameProp.GetString() ?? "";

                if (name.Contains("RC", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Preview", StringComparison.OrdinalIgnoreCase))
                    continue;

                return version;
            }
        }
        catch (HttpRequestException)
        {
            // fallback - skip this major version
        }

        return null;
    }
}
