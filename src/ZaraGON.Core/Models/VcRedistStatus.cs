namespace ZaraGON.Core.Models;

public sealed class VcRedistStatus
{
    public bool IsCompatible { get; init; }
    public Version? InstalledVersion { get; init; }
    public Version? RequiredMinimumVersion { get; init; }
    public string? VsVersion { get; init; }
    public string? DownloadUrl { get; init; }
}
