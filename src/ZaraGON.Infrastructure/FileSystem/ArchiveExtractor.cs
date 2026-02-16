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

                    entry.ExtractToFile(destinationPath, overwrite: true);
                }

                processed++;
                progress?.Report((double)processed / totalEntries * 100);
            }
        }, ct);
    }
}
