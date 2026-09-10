using DoradoCloud.Modules.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DoradoCloud.Tests;

public sealed class CertificateKeyProviderTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"dorado-cloud-keys-{Guid.NewGuid():N}");

    private CertificateKeyProvider CreateProvider() => new(
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Certificates:Path"] = _directory
            })
            .Build(),
        NullLogger<CertificateKeyProvider>.Instance);

    [Fact]
    public void Signing_and_encryption_certificates_are_persisted_and_reloaded()
    {
        var first = CreateProvider();
        var signing = first.GetSigningCertificate();
        var encryption = first.GetEncryptionCertificate();

        Assert.True(signing.HasPrivateKey);
        Assert.True(encryption.HasPrivateKey);
        Assert.NotEqual(signing.Thumbprint, encryption.Thumbprint);

        Assert.True(File.Exists(Path.Combine(_directory, "openiddict-signing.pfx")));
        Assert.True(File.Exists(Path.Combine(_directory, "openiddict-encryption.pfx")));

        // A second provider must load the same key material (stable across restarts/replicas).
        var second = CreateProvider();
        Assert.Equal(signing.Thumbprint, second.GetSigningCertificate().Thumbprint);
        Assert.Equal(encryption.Thumbprint, second.GetEncryptionCertificate().Thumbprint);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch
        {
            // Best effort.
        }
    }
}
