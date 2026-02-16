using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ZaraGON.Core.Constants;
using ZaraGON.Core.Interfaces.Infrastructure;
using ZaraGON.Core.Interfaces.Services;

namespace ZaraGON.Application.Services;

public sealed class SslCertificateService : ISslCertificateManager
{
    private readonly IFileSystem _fileSystem;
    private readonly string _basePath;
    private readonly string _sslDir;

    private const string CaCertFile = "zoragon-ca.crt";
    private const string CaKeyFile = "zoragon-ca.key";

    public SslCertificateService(IFileSystem fileSystem, string basePath)
    {
        _fileSystem = fileSystem;
        _basePath = basePath;
        _sslDir = Path.Combine(basePath, Defaults.SslDir);
    }

    public bool HasCaCertificate() =>
        _fileSystem.FileExists(Path.Combine(_sslDir, CaCertFile)) &&
        _fileSystem.FileExists(Path.Combine(_sslDir, CaKeyFile));

    public bool HasDomainCertificate(string domain) =>
        _fileSystem.FileExists(Path.Combine(_sslDir, $"{domain}.crt")) &&
        _fileSystem.FileExists(Path.Combine(_sslDir, $"{domain}.key"));

    public async Task EnsureCaCertificateAsync(CancellationToken ct = default)
    {
        if (HasCaCertificate()) return;

        _fileSystem.CreateDirectory(_sslDir);

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=ZaraGON Local CA, O=ZaraGON, OU=Development",
            rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));

        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = DateTimeOffset.UtcNow.AddYears(10);

        using var cert = request.CreateSelfSigned(notBefore, notAfter);

        // Export PEM cert
        var certPem = cert.ExportCertificatePem();
        await _fileSystem.WriteAllTextAsync(
            Path.Combine(_sslDir, CaCertFile), certPem, ct);

        // Export PEM private key
        var keyPem = rsa.ExportRSAPrivateKeyPem();
        await _fileSystem.WriteAllTextAsync(
            Path.Combine(_sslDir, CaKeyFile), keyPem, ct);
    }

    public async Task<(string certPath, string keyPath)> EnsureDomainCertificateAsync(
        string domain, CancellationToken ct = default)
    {
        var certPath = Path.Combine(_sslDir, $"{domain}.crt");
        var keyPath = Path.Combine(_sslDir, $"{domain}.key");

        if (HasDomainCertificate(domain))
            return (certPath, keyPath);

        // Ensure CA exists first
        await EnsureCaCertificateAsync(ct);

        // Load CA cert and key
        var caCertPem = await File.ReadAllTextAsync(Path.Combine(_sslDir, CaCertFile), ct);
        var caKeyPem = await File.ReadAllTextAsync(Path.Combine(_sslDir, CaKeyFile), ct);

        using var caRsa = RSA.Create();
        caRsa.ImportFromPem(caKeyPem);

        using var caCert = X509Certificate2.CreateFromPem(caCertPem, caKeyPem);

        // Create domain cert
        using var domainRsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={domain}, O=ZaraGON, OU=Development",
            domainRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Subject Alternative Names
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(domain);
        sanBuilder.AddDnsName($"*.{domain}");
        sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
        request.CertificateExtensions.Add(sanBuilder.Build());

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [new Oid("1.3.6.1.5.5.7.3.1")], false)); // serverAuth

        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = DateTimeOffset.UtcNow.AddYears(3);

        var serial = new byte[16];
        RandomNumberGenerator.Fill(serial);

        using var domainCert = request.Create(caCert, notBefore, notAfter, serial);

        // Export PEM
        var domainCertPem = domainCert.ExportCertificatePem();
        await _fileSystem.WriteAllTextAsync(certPath, domainCertPem, ct);

        var domainKeyPem = domainRsa.ExportRSAPrivateKeyPem();
        await _fileSystem.WriteAllTextAsync(keyPath, domainKeyPem, ct);

        return (certPath, keyPath);
    }

    public Task TrustCaCertificateAsync(CancellationToken ct = default)
    {
        if (!HasCaCertificate()) return Task.CompletedTask;

        try
        {
            var caCertPath = Path.Combine(_sslDir, CaCertFile);
            var cert = X509CertificateLoader.LoadCertificateFromFile(caCertPath);

            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);

            // Check if already trusted
            var existing = store.Certificates.Find(
                X509FindType.FindBySubjectName, "ZaraGON Local CA", false);
            if (existing.Count == 0)
            {
                store.Add(cert);
            }
            store.Close();
        }
        catch
        {
            // May need admin privileges - don't crash
        }

        return Task.CompletedTask;
    }
}
