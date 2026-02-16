using ZaraGON.Core.Models;

namespace ZaraGON.Core.Interfaces.Services;

public interface IPhpExtensionManager
{
    Task<IReadOnlyList<PhpExtension>> GetExtensionsAsync(string phpVersion, CancellationToken ct = default);
    Task EnableExtensionAsync(string phpVersion, string extensionName, CancellationToken ct = default);
    Task DisableExtensionAsync(string phpVersion, string extensionName, CancellationToken ct = default);
    Task SetExtensionsAsync(string phpVersion, IEnumerable<string> extensionNames, CancellationToken ct = default);
}
