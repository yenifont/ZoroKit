using ZaraGON.Core.Enums;

namespace ZaraGON.Core.Models;

public sealed class DownloadProgress
{
    public required DownloadState State { get; init; }
    public long BytesReceived { get; init; }
    public long TotalBytes { get; init; }
    public double ProgressPercent => TotalBytes > 0 ? (double)BytesReceived / TotalBytes * 100 : 0;
    public string? FileName { get; init; }
    public string? ErrorMessage { get; init; }
    public double SpeedBytesPerSecond { get; init; }
}
