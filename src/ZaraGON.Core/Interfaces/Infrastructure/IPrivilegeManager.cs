namespace ZaraGON.Core.Interfaces.Infrastructure;

public interface IPrivilegeManager
{
    bool IsRunningAsAdmin();
    Task<bool> RequestElevationAsync(string reason, CancellationToken ct = default);
    Task RunElevatedAsync(string executablePath, string arguments, CancellationToken ct = default);
}
