using System.Security.Cryptography;
using System.Text;
using DoradoCloud.Shared.Contracts;

namespace DoradoCloud.Client.Tests;

/// <summary>
/// Generates a fresh RSA key pair for each test, exporting the public half in
/// PEM subjectPublicKeyInfo format and exposing the private half as an
/// <see cref="RSA"/> instance for signing.
/// </summary>
internal static class TestKeys
{
    public sealed record KeyPair(string Public, RSA Private);

    public static KeyPair GenerateRsaPem(int keySize = 2048)
    {
        var rsa = RSA.Create(keySize);
        return new KeyPair(rsa.ExportSubjectPublicKeyInfoPem(), rsa);
    }

    public static string Sign(UpdateManifest manifest, RSA privateKey)
        => UpdateManifestCrypto.Sign(manifest, privateKey);
}
