using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DoradoCloud.Modules.Updates;

/// <summary>
/// Provides the RSA key used to sign update manifests. The key is persisted as
/// PKCS#8 PEM at <c>Updates:SigningKeyPath</c> (generated on first use) so that
/// signatures remain verifiable across restarts and replicas.
/// </summary>
public sealed class UpdateSigningKeyProvider
{
    private readonly string _path;
    private readonly ILogger<UpdateSigningKeyProvider> _logger;

    public UpdateSigningKeyProvider(IConfiguration configuration, ILogger<UpdateSigningKeyProvider> logger)
    {
        _logger = logger;
        _path = configuration["Updates:SigningKeyPath"] ?? "data/keys/updates-signing.pem";
    }

    public RSA GetPrivateKey()
    {
        var rsa = RSA.Create(2048);

        if (File.Exists(_path))
        {
            rsa.ImportFromPem(File.ReadAllText(_path));
            return rsa;
        }

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, rsa.ExportPkcs8PrivateKeyPem());
        _logger.LogWarning("Generated a new update signing key at {Path}. Back it up and share it across replicas.", _path);
        return rsa;
    }

    public string GetPublicKeyPem()
    {
        using var rsa = GetPrivateKey();
        return rsa.ExportSubjectPublicKeyInfoPem();
    }
}
