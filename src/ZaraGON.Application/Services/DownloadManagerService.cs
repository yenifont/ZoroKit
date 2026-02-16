using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;

namespace ZaraGON.Application.Services;

public sealed class DownloadManagerService : IDownloadManager
{
    private readonly IDownloadManager _inner;

    public DownloadManagerService(IDownloadManager inner)
    {
        _inner = inner;
    }

    public Task<string> DownloadFileAsync(string url, string destinationDir, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
        => _inner.DownloadFileAsync(url, destinationDir, progress, ct);

    public Task ExtractArchiveAsync(string archivePath, string destinationDir, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
        => _inner.ExtractArchiveAsync(archivePath, destinationDir, progress, ct);
}
