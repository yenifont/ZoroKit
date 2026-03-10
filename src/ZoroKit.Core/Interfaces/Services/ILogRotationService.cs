namespace ZoroKit.Core.Interfaces.Services;

public interface ILogRotationService
{
    /// <summary>Boyut eşiğini aşmışsa dosyayı rotate eder.</summary>
    Task RotateIfNeededAsync(string filePath, CancellationToken ct = default);

    /// <summary>Tüm log dosyalarını rotate eder. Servis çalışıyorsa ilgili loglar atlanır.</summary>
    Task RotateAllAsync(bool apacheRunning, bool mariaDbRunning, CancellationToken ct = default);

    /// <summary>Eski rotate dosyalarını siler (MaxFiles ve MaxAgeDays).</summary>
    Task CleanupOldLogsAsync(CancellationToken ct = default);
}
