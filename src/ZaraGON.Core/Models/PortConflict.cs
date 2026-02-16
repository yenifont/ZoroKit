namespace ZaraGON.Core.Models;

public sealed class PortConflict
{
    public required int Port { get; init; }
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public string? ProcessPath { get; init; }
    public bool IsSystemCritical { get; init; }
}
