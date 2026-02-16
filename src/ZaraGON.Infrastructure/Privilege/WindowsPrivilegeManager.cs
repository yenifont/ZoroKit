using System.Diagnostics;
using System.Security.Principal;
using ZaraGON.Core.Exceptions;
using ZaraGON.Core.Interfaces.Infrastructure;

namespace ZaraGON.Infrastructure.Privilege;

public sealed class WindowsPrivilegeManager : IPrivilegeManager
{
    public bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public Task<bool> RequestElevationAsync(string reason, CancellationToken ct = default)
    {
        if (IsRunningAsAdmin())
            return Task.FromResult(true);

        // Re-launch self as admin
        var exePath = Environment.ProcessPath ?? throw new PrivilegeException("Cannot determine executable path.");
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Verb = "runas",
            UseShellExecute = true,
            Arguments = "--elevated"
        };

        try
        {
            System.Diagnostics.Process.Start(psi);
            return Task.FromResult(true);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return Task.FromResult(false);
        }
    }

    public async Task RunElevatedAsync(string executablePath, string arguments, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            Verb = "runas",
            UseShellExecute = true,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new PrivilegeException("Failed to start elevated process.");

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new PrivilegeException($"Elevated process exited with code {process.ExitCode}.");
    }
}
