using ZaraGON.Core.Interfaces.Services;

namespace ZaraGON.Application.Services;

public sealed class ProcessManagerService : IProcessManager
{
    private readonly IProcessManager _inner;

    public ProcessManagerService(IProcessManager inner)
    {
        _inner = inner;
    }

    public Task<int> StartProcessAsync(string executablePath, string arguments, string? workingDirectory = null, CancellationToken ct = default)
        => _inner.StartProcessAsync(executablePath, arguments, workingDirectory, ct);

    public Task<int> StartProcessAsync(string executablePath, string arguments, string? workingDirectory, Dictionary<string, string>? environmentVariables, CancellationToken ct = default)
        => _inner.StartProcessAsync(executablePath, arguments, workingDirectory, environmentVariables, ct);

    public Task StopProcessAsync(int processId, CancellationToken ct = default)
        => _inner.StopProcessAsync(processId, ct);

    public Task KillProcessAsync(int processId, CancellationToken ct = default)
        => _inner.KillProcessAsync(processId, ct);

    public Task<bool> IsProcessRunningAsync(int processId, CancellationToken ct = default)
        => _inner.IsProcessRunningAsync(processId, ct);

    public Task<(string stdout, string stderr, int exitCode)> RunCommandAsync(string executablePath, string arguments, string? workingDirectory = null, CancellationToken ct = default)
        => _inner.RunCommandAsync(executablePath, arguments, workingDirectory, ct);

    public Task<string?> GetProcessPathAsync(int processId, CancellationToken ct = default)
        => _inner.GetProcessPathAsync(processId, ct);

    public bool IsSystemCriticalProcess(string processName)
        => _inner.IsSystemCriticalProcess(processName);
}
