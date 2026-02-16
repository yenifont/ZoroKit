using ZaraGON.Core.Enums;
using ZaraGON.Core.Exceptions;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;

namespace ZaraGON.Infrastructure.Network;

public sealed class HttpDownloader : IDownloadManager
{
    private readonly HttpClient _httpClient;
    private readonly Core.Interfaces.Infrastructure.IArchiveExtractor _archiveExtractor;

    public HttpDownloader(HttpClient httpClient, Core.Interfaces.Infrastructure.IArchiveExtractor archiveExtractor)
    {
        _httpClient = httpClient;
        _archiveExtractor = archiveExtractor;
    }

    public async Task<string> DownloadFileAsync(string url, string destinationDir, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        if (!Directory.Exists(destinationDir))
            Directory.CreateDirectory(destinationDir);

        var fileName = GetFileNameFromUrl(url);
        var filePath = Path.Combine(destinationDir, fileName);

        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;

            progress?.Report(new DownloadProgress
            {
                State = DownloadState.Downloading,
                BytesReceived = 0,
                TotalBytes = totalBytes,
                FileName = fileName
            });

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            var lastReport = DateTime.UtcNow;
            long lastReportBytes = 0;

            while (true)
            {
                var bytesRead = await contentStream.ReadAsync(buffer, ct);
                if (bytesRead == 0)
                    break;

                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;

                var now = DateTime.UtcNow;
                if ((now - lastReport).TotalMilliseconds >= 250)
                {
                    var elapsed = (now - lastReport).TotalSeconds;
                    var speed = elapsed > 0 ? (totalRead - lastReportBytes) / elapsed : 0;

                    progress?.Report(new DownloadProgress
                    {
                        State = DownloadState.Downloading,
                        BytesReceived = totalRead,
                        TotalBytes = totalBytes,
                        FileName = fileName,
                        SpeedBytesPerSecond = speed
                    });

                    lastReport = now;
                    lastReportBytes = totalRead;
                }
            }

            progress?.Report(new DownloadProgress
            {
                State = DownloadState.Completed,
                BytesReceived = totalRead,
                TotalBytes = totalRead,
                FileName = fileName
            });

            return filePath;
        }
        catch (OperationCanceledException)
        {
            CleanupPartialDownload(filePath);
            progress?.Report(new DownloadProgress
            {
                State = DownloadState.Cancelled,
                FileName = fileName
            });
            throw;
        }
        catch (Exception ex)
        {
            CleanupPartialDownload(filePath);
            progress?.Report(new DownloadProgress
            {
                State = DownloadState.Failed,
                FileName = fileName,
                ErrorMessage = ex.Message
            });
            throw new DownloadFailedException(url, $"Download failed: {ex.Message}", ex);
        }
    }

    public async Task ExtractArchiveAsync(string archivePath, string destinationDir, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        var fileName = Path.GetFileName(archivePath);

        progress?.Report(new DownloadProgress
        {
            State = DownloadState.Extracting,
            FileName = fileName
        });

        var extractProgress = new Progress<double>(pct =>
        {
            progress?.Report(new DownloadProgress
            {
                State = DownloadState.Extracting,
                BytesReceived = (long)pct,
                TotalBytes = 100,
                FileName = fileName
            });
        });

        await _archiveExtractor.ExtractZipAsync(archivePath, destinationDir, extractProgress, ct);

        progress?.Report(new DownloadProgress
        {
            State = DownloadState.Completed,
            BytesReceived = 100,
            TotalBytes = 100,
            FileName = fileName
        });
    }

    private static string GetFileNameFromUrl(string url)
    {
        var uri = new Uri(url);
        var fileName = Path.GetFileName(uri.LocalPath);
        return string.IsNullOrWhiteSpace(fileName) ? $"download_{Guid.NewGuid():N}.zip" : fileName;
    }

    private static void CleanupPartialDownload(string filePath)
    {
        try { if (File.Exists(filePath)) File.Delete(filePath); } catch { /* best effort */ }
    }
}
