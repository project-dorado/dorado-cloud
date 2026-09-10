using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DoradoCloud.Shared.Contracts;

/// <summary>
/// Canonical serialization + RS256 detached signatures for update manifests.
/// Both the server (publishing) and the clients (verifying) use these helpers so
/// the signed bytes are identical on both sides.
/// </summary>
public static class UpdateManifestCrypto
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(UpdateManifest manifest) => JsonSerializer.Serialize(manifest, Options);

    public static string Sign(UpdateManifest manifest, RSA privateKey)
    {
        var data = Encoding.UTF8.GetBytes(Serialize(manifest));
        var signature = privateKey.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signature);
    }

    public static bool Verify(UpdateManifest manifest, string signatureBase64, string publicKeyPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);

        var data = Encoding.UTF8.GetBytes(Serialize(manifest));
        return rsa.VerifyData(
            data,
            Convert.FromBase64String(signatureBase64),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }
}
