using System.Diagnostics;
using ZaraGON.Core.Constants;
using ZaraGON.Core.Interfaces.Services;

namespace ZaraGON.Infrastructure.Process;

public sealed class WindowsProcessManager : IProcessManager, IDisposable
{
    private readonly object _jobLock = new();
    private readonly Dictionary<int, System.Diagnostics.Process> _managedProcesses = [];

    public Task<int> StartProcessAsync(string executablePath, string arguments, string? workingDirectory = null, CancellationToken ct = default)
        => StartProcessAsync(executablePath, arguments, workingDirectory, null, ct);

    public async Task<int> StartProcessAsync(string executablePath, string arguments, string? workingDirectory, Dictionary<string, string>? environmentVariables, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath) ?? "",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (environmentVariables != null)
        {
            foreach (var (key, value) in environmentVariables)
            {
                psi.Environment[key] = value;
            }
        }

        var process = new System.Diagnostics.Process { StartInfo = psi };
        process.Start();

        lock (_jobLock)
        {
            _managedProcesses[process.Id] = process;
        }

        // Read output asynchronously to avoid deadlocks
        _ = process.StandardOutput.ReadToEndAsync(ct);
        _ = process.StandardError.ReadToEndAsync(ct);

        await Task.CompletedTask;
        return process.Id;
    }

    public async Task StopProcessAsync(int processId, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(processId);
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(5000))
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
            }
            catch (ArgumentException) { /* process already exited */ }
        }, ct);

        lock (_jobLock)
        {
            _managedProcesses.Remove(processId);
        }
    }

    public async Task KillProcessAsync(int processId, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(processId);
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
            }
            catch (ArgumentException) { /* already exited */ }
        }, ct);

        lock (_jobLock)
        {
            _managedProcesses.Remove(processId);
        }
    }

    public Task<bool> IsProcessRunningAsync(int processId, CancellationToken ct = default)
    {
        try
        {
            var process = System.Diagnostics.Process.GetProcessById(processId);
            return Task.FromResult(!process.HasExited);
        }
        catch (ArgumentException)
        {
            return Task.FromResult(false);
        }
    }

    public async Task<(string stdout, string stderr, int exitCode)> RunCommandAsync(
        string executablePath, string arguments, string? workingDirectory = null, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath) ?? "",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = new System.Diagnostics.Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (stdout, stderr, process.ExitCode);
    }

    public Task<string?> GetProcessPathAsync(int processId, CancellationToken ct = default)
    {
        try
        {
            var process = System.Diagnostics.Process.GetProcessById(processId);
            return Task.FromResult<string?>(process.MainModule?.FileName);
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    public bool IsSystemCriticalProcess(string processName)
    {
        return Defaults.SystemCriticalProcesses
            .Any(cp => string.Equals(cp, processName, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        lock (_jobLock)
        {
            // Only dispose handles, don't kill processes - they should keep running
            foreach (var process in _managedProcesses.Values)
            {
                try { process.Dispose(); }
                catch { /* best effort */ }
            }
            _managedProcesses.Clear();
        }
    }

}
