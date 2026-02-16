using System.Text.Json;
using ZaraGON.Core.Enums;
using ZaraGON.Core.Interfaces.Providers;
using ZaraGON.Core.Models;

namespace ZaraGON.Infrastructure.VersionProviders;

public sealed class PhpMyAdminVersionProvider : IVersionProvider
{
    private readonly HttpClient _httpClient;
    private const string VersionUrl = "https://www.phpmyadmin.net/home_page/version.json";

    public ServiceType ServiceType => ServiceType.PhpMyAdmin;

    public PhpMyAdminVersionProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ServiceVersion>> FetchAvailableVersionsAsync(CancellationToken ct = default)
    {
        var versions = new List<ServiceVersion>();

        try
        {
            var json = await _httpClient.GetStringAsync(VersionUrl, ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("version", out var versionProp))
                return versions;

            var version = versionProp.GetString();
            if (string.IsNullOrEmpty(version))
                return versions;

            var downloadUrl = $"https://files.phpmyadmin.net/phpMyAdmin/{version}/phpMyAdmin-{version}-all-languages.zip";

            versions.Add(new ServiceVersion
            {
                ServiceType = ServiceType.PhpMyAdmin,
                Version = version,
                DownloadUrl = downloadUrl,
                FileName = $"phpMyAdmin-{version}-all-languages.zip"
            });
        }
        catch (HttpRequestException)
        {
            // Network error - return empty list
        }

        return versions;
    }
}
