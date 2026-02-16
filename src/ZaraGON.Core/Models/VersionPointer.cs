using ZaraGON.Core.Enums;

namespace ZaraGON.Core.Models;

public sealed class VersionPointer
{
    public required ServiceType ServiceType { get; set; }
    public required string Version { get; set; }
    public required string InstallPath { get; set; }
    public string? VsVersion { get; set; }
    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; }
}
