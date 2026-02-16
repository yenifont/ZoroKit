namespace ZaraGON.Core.Models;

public sealed class HealthCheckResult
{
    public required string CheckName { get; init; }
    public required bool IsHealthy { get; init; }
    public required string Message { get; init; }
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
}
