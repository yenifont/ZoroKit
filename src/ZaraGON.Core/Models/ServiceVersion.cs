using ZaraGON.Core.Enums;

namespace ZaraGON.Core.Models;

public sealed class ServiceVersion
{
    public required ServiceType ServiceType { get; init; }
    public required string Version { get; init; }
    public required string DownloadUrl { get; init; }
    public string? FileName { get; init; }
    public long? FileSize { get; init; }
    public string? VsVersion { get; init; }
    public bool IsThreadSafe { get; init; }
    public string? Architecture { get; init; }
    public bool IsInstalled { get; set; }

    public override string ToString() => $"{ServiceType} {Version}";
}
