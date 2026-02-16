using ZaraGON.Core.Models;

namespace ZaraGON.Core.Interfaces.Services;

public interface IDownloadManager
{
    Task<string> DownloadFileAsync(string url, string destinationDir, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default);
    Task ExtractArchiveAsync(string archivePath, string destinationDir, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default);
}
