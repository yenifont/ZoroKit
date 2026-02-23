using System.IO.Compression;
using ZaraGON.Core.Interfaces.Infrastructure;

namespace ZaraGON.Infrastructure.FileSystem;

public sealed class ArchiveExtractor : IArchiveExtractor
{
    public async Task ExtractZipAsync(string archivePath, string destinationDir, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var totalEntries = archive.Entries.Count;
            var processed = 0;

            if (!Directory.Exists(destinationDir))
                Directory.CreateDirectory(destinationDir);

            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();

                var destinationPath = Path.GetFullPath(Path.Combine(destinationDir, entry.FullName));

                // Prevent zip slip
                if (!destinationPath.StartsWith(Path.GetFullPath(destinationDir), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Zip entry '{entry.FullName}' would extract outside target directory.");

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                }
                else
                {
                    var entryDir = Path.GetDirectoryName(destinationPath);
                    if (entryDir != null && !Directory.Exists(entryDir))
                        Directory.CreateDirectory(entryDir);

                    ExtractEntryWithRetry(entry, destinationPath);
                }

                processed++;
                progress?.Report((double)processed / totalEntries * 100);
            }
        }, ct);
    }

    private static void ExtractEntryWithRetry(ZipArchiveEntry entry, string destinationPath, int maxRetries = 3)
    {
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                entry.ExtractToFile(destinationPath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < maxRetries)
            {
                // Access denied: antivirus veya baska process dosyayi kilitlemis olabilir; kisa bekle ve tekrar dene
                Thread.Sleep(800 * attempt);
            }
        }
    }
}
