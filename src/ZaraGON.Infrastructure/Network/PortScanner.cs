using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using ZaraGON.Core.Constants;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;

namespace ZaraGON.Infrastructure.Network;

public sealed class PortScanner : IPortManager
{
    private readonly IProcessManager _processManager;

    public PortScanner(IProcessManager processManager)
    {
        _processManager = processManager;
    }

    public Task<bool> IsPortAvailableAsync(int port, CancellationToken ct = default)
    {
        var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
        var isUsed = listeners.Any(ep => ep.Port == port);
        return Task.FromResult(!isUsed);
    }

    public async Task<PortConflict?> GetPortConflictAsync(int port, CancellationToken ct = default)
    {
        if (await IsPortAvailableAsync(port, ct))
            return null;

        // Use netstat to find PID
        var (stdout, _, exitCode) = await _processManager.RunCommandAsync(
            "netstat", $"-ano -p TCP", null, ct);

        if (exitCode != 0)
            return null;

        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.Contains("LISTENING"))
                continue;

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
                continue;

            var localAddress = parts[1];
            var colonIdx = localAddress.LastIndexOf(':');
            if (colonIdx < 0)
                continue;

            if (!int.TryParse(localAddress[(colonIdx + 1)..], out var listeningPort) || listeningPort != port)
                continue;

            if (!int.TryParse(parts[^1], out var pid))
                continue;

            string processName;
            string? processPath;
            bool isSystem;

            try
            {
                var process = System.Diagnostics.Process.GetProcessById(pid);
                processName = process.ProcessName;
                processPath = await _processManager.GetProcessPathAsync(pid, ct);
                isSystem = _processManager.IsSystemCriticalProcess(processName);
            }
            catch
            {
                processName = "Unknown";
                processPath = null;
                isSystem = false;
            }

            return new PortConflict
            {
                Port = port,
                ProcessId = pid,
                ProcessName = processName,
                ProcessPath = processPath,
                IsSystemCritical = isSystem
            };
        }

        return null;
    }

    public async Task<IReadOnlyList<PortBinding>> GetActiveBindingsAsync(CancellationToken ct = default)
    {
        var bindings = new List<PortBinding>();
        var (stdout, _, exitCode) = await _processManager.RunCommandAsync(
            "netstat", "-ano -p TCP", null, ct);

        if (exitCode != 0)
            return bindings;

        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.Contains("LISTENING"))
                continue;

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) continue;

            var localAddress = parts[1];
            var colonIdx = localAddress.LastIndexOf(':');
            if (colonIdx < 0) continue;

            if (!int.TryParse(localAddress[(colonIdx + 1)..], out var port)) continue;
            if (!int.TryParse(parts[^1], out var pid)) continue;

            string? processName = null;
            try
            {
                processName = System.Diagnostics.Process.GetProcessById(pid).ProcessName;
            }
            catch { /* process may have exited */ }

            bindings.Add(new PortBinding
            {
                Port = port,
                Address = localAddress[..colonIdx],
                ProcessId = pid,
                ProcessName = processName
            });
        }

        return bindings;
    }

    public async Task<bool> KillProcessOnPortAsync(int port, CancellationToken ct = default)
    {
        var conflict = await GetPortConflictAsync(port, ct);
        if (conflict == null)
            return true;

        if (conflict.IsSystemCritical)
            return false;

        await _processManager.KillProcessAsync(conflict.ProcessId, ct);
        return true;
    }

    public async Task<int?> FindAvailablePortAsync(int startPort, int maxPort = 65535, CancellationToken ct = default)
    {
        for (var port = startPort; port <= Math.Min(maxPort, 65535); port++)
        {
            if (await IsPortAvailableAsync(port, ct))
                return port;
        }
        return null;
    }
}
