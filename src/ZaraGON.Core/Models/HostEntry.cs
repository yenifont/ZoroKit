namespace ZaraGON.Core.Models;

public sealed class HostEntry
{
    public required string IpAddress { get; set; }
    public required string Hostname { get; set; }
    public string? Comment { get; set; }
    public bool IsEnabled { get; set; } = true;
}
