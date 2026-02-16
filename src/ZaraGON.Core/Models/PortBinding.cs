namespace ZaraGON.Core.Models;

public sealed class PortBinding
{
    public required int Port { get; init; }
    public required string Address { get; init; }
    public int ProcessId { get; init; }
    public string? ProcessName { get; init; }
}
