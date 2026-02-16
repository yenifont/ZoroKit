namespace ZaraGON.Core.Interfaces.Infrastructure;

public interface IArchiveExtractor
{
    Task ExtractZipAsync(string archivePath, string destinationDir, IProgress<double>? progress = null, CancellationToken ct = default);
}
