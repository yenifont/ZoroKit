using System.Text.RegularExpressions;
using ZoroKit.Core.Constants;
using ZoroKit.Core.Interfaces.Infrastructure;
using ZoroKit.Core.Interfaces.Services;

namespace ZoroKit.Application.Services;

public sealed class LogRotationService : ILogRotationService
{
    private readonly IFileSystem _fileSystem;
    private readonly string _basePath;

    public LogRotationService(IFileSystem fileSystem, string basePath)
    {
        _fileSystem = fileSystem;
        _basePath = basePath;
    }

    public Task RotateIfNeededAsync(string filePath, CancellationToken ct = default)
    {
        try
        {
            if (!_fileSystem.FileExists(filePath))
                return Task.CompletedTask;

            var size = _fileSystem.GetFileSize(filePath);
            if (size < Defaults.LogRotationMaxBytes)
                return Task.CompletedTask;

            var dir = Path.GetDirectoryName(filePath)!;
            var baseName = Path.GetFileNameWithoutExtension(filePath);
            var ext = Path.GetExtension(filePath);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            var rotatedPath = Path.Combine(dir, $"{baseName}.{timestamp}{ext}");

            _fileSystem.MoveFile(filePath, rotatedPath);
        }
        catch
        {
            // best effort — file may be locked
        }

        return Task.CompletedTask;
    }

    public async Task RotateAllAsync(bool apacheRunning, bool mariaDbRunning, CancellationToken ct = default)
    {
        // zoragon.log — open-write-close pattern, her zaman rotate edilebilir
        var appLog = Path.Combine(_basePath, Defaults.LogDir, "zoragon.log");
        await RotateIfNeededAsync(appLog, ct);

        // Apache logları — sadece servis çalışmıyorsa
        if (!apacheRunning)
        {
            var apacheErrorLog = Path.Combine(_basePath, Defaults.LogDir, "apache", "error.log");
            var apacheAccessLog = Path.Combine(_basePath, Defaults.LogDir, "apache", "access.log");
            await RotateIfNeededAsync(apacheErrorLog, ct);
            await RotateIfNeededAsync(apacheAccessLog, ct);
        }

        // MariaDB log — sadece servis çalışmıyorsa
        if (!mariaDbRunning)
        {
            var mariaDbErrorLog = Path.Combine(_basePath, Defaults.LogDir, "mariadb-error.log");
            await RotateIfNeededAsync(mariaDbErrorLog, ct);
        }
    }

    public Task CleanupOldLogsAsync(CancellationToken ct = default)
    {
        try
        {
            CleanupDirectory(Path.Combine(_basePath, Defaults.LogDir));
            CleanupDirectory(Path.Combine(_basePath, Defaults.LogDir, "apache"));
        }
        catch
        {
            // best effort
        }

        return Task.CompletedTask;
    }

    private static readonly Regex RotatedFilePattern = new(
        @"\.\d{4}-\d{2}-\d{2}_\d{6}\.",
        RegexOptions.Compiled);

    private void CleanupDirectory(string dir)
    {
        if (!_fileSystem.DirectoryExists(dir))
            return;

        var allFiles = _fileSystem.GetFiles(dir);
        var rotatedFiles = allFiles
            .Where(f => RotatedFilePattern.IsMatch(Path.GetFileName(f)))
            .ToArray();

        if (rotatedFiles.Length == 0)
            return;

        // Rotated dosyaları base name'e göre grupla
        var groups = rotatedFiles
            .GroupBy(f => GetOriginalBaseName(f))
            .ToList();

        var cutoffDate = DateTime.Now.AddDays(-Defaults.LogRotationMaxAgeDays);

        foreach (var group in groups)
        {
            var sorted = group.OrderByDescending(f => f).ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                var shouldDelete = false;

                // MaxFiles aşımı
                if (i >= Defaults.LogRotationMaxFiles)
                    shouldDelete = true;

                // MaxAgeDays aşımı
                if (!shouldDelete)
                {
                    var timestamp = ExtractTimestamp(sorted[i]);
                    if (timestamp.HasValue && timestamp.Value < cutoffDate)
                        shouldDelete = true;
                }

                if (shouldDelete)
                {
                    try { _fileSystem.DeleteFile(sorted[i]); }
                    catch { /* best effort */ }
                }
            }
        }
    }

    /// <summary>
    /// error.2026-03-10_143022.log → error
    /// </summary>
    private static string GetOriginalBaseName(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var match = Regex.Match(fileName, @"^(.+?)\.\d{4}-\d{2}-\d{2}_\d{6}\.");
        return match.Success ? match.Groups[1].Value : fileName;
    }

    /// <summary>
    /// Dosya adından timestamp çıkarır.
    /// </summary>
    private static DateTime? ExtractTimestamp(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var match = Regex.Match(fileName, @"\.(\d{4}-\d{2}-\d{2})_(\d{6})\.");
        if (!match.Success)
            return null;

        var dateStr = match.Groups[1].Value;
        var timeStr = match.Groups[2].Value;
        var combined = $"{dateStr} {timeStr[..2]}:{timeStr[2..4]}:{timeStr[4..6]}";

        return DateTime.TryParse(combined, out var result) ? result : null;
    }
}
