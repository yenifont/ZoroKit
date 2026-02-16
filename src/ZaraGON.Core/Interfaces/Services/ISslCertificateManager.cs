namespace ZaraGON.Core.Interfaces.Services;

public interface ISslCertificateManager
{
    Task EnsureCaCertificateAsync(CancellationToken ct = default);
    Task<(string certPath, string keyPath)> EnsureDomainCertificateAsync(string domain, CancellationToken ct = default);
    bool HasCaCertificate();
    bool HasDomainCertificate(string domain);
    Task TrustCaCertificateAsync(CancellationToken ct = default);
}
