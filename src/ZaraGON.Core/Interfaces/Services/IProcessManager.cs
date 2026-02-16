namespace ZaraGON.Core.Interfaces.Services;

public interface IProcessManager
{
    Task<int> StartProcessAsync(string executablePath, string arguments, string? workingDirectory = null, CancellationToken ct = default);
    Task<int> StartProcessAsync(string executablePath, string arguments, string? workingDirectory, Dictionary<string, string>? environmentVariables, CancellationToken ct = default);
    Task StopProcessAsync(int processId, CancellationToken ct = default);
    Task KillProcessAsync(int processId, CancellationToken ct = default);
    Task<bool> IsProcessRunningAsync(int processId, CancellationToken ct = default);
    Task<(string stdout, string stderr, int exitCode)> RunCommandAsync(string executablePath, string arguments, string? workingDirectory = null, CancellationToken ct = default);
    Task<string?> GetProcessPathAsync(int processId, CancellationToken ct = default);
    bool IsSystemCriticalProcess(string processName);
}
