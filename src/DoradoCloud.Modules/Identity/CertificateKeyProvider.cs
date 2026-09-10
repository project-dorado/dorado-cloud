using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DoradoCloud.Modules.Identity;

/// <summary>
/// Loads (or, in development, creates) the X509 certificates OpenIddict uses to
/// sign and encrypt tokens. In production the certificates are read from a
/// configurable directory so key material survives restarts and is shared across
/// replicas; development uses OpenIddict's ephemeral certificates instead.
/// </summary>
public sealed class CertificateKeyProvider
{
    private readonly string _directory;
    private readonly string? _password;
    private readonly ILogger<CertificateKeyProvider> _logger;

    public CertificateKeyProvider(IConfiguration configuration, ILogger<CertificateKeyProvider> logger)
    {
        _logger = logger;
        _directory = configuration["Auth:Certificates:Path"] ?? "data/keys";
        _password = configuration["Auth:Certificates:Password"];
    }

    public X509Certificate2 GetSigningCertificate() => GetOrCreate("openiddict-signing.pfx");
    public X509Certificate2 GetEncryptionCertificate() => GetOrCreate("openiddict-encryption.pfx");

    private X509Certificate2 GetOrCreate(string fileName)
    {
        System.IO.Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, fileName);

        if (File.Exists(path))
        {
            return new X509Certificate2(path, _password, X509KeyStorageFlags.Exportable);
        }

        _logger.LogWarning("Generating a new OpenIddict certificate at {Path}. Back this up and share it across replicas.", path);

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN=Dorado Cloud ({Path.GetFileNameWithoutExtension(fileName)})",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: false));

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(5));

        var export = _password is null
            ? certificate.Export(X509ContentType.Pfx)
            : certificate.Export(X509ContentType.Pfx, _password);

        File.WriteAllBytes(path, export);
        TryRestrictPermissions(path);

        // Reload from disk so the returned certificate is backed by the persisted key.
        return new X509Certificate2(path, _password, X509KeyStorageFlags.Exportable);
    }

    private static void TryRestrictPermissions(string path)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch
        {
            // Best effort only.
        }
    }
}
