using System.Security.Cryptography;
using System.Text;
using DoradoCloud.Modules.Data;
using Microsoft.EntityFrameworkCore;

namespace DoradoCloud.Modules.Identity;

/// <summary>
/// Issues and consumes single-use account tokens (email verification, password
/// reset). Only the SHA-256 hash is stored.
/// </summary>
public sealed class AccountTokenService(DoradoDbContext db)
{
    public const string PurposeEmailVerification = "email_verification";
    public const string PurposePasswordReset = "password_reset";

    public async Task<string> IssueAsync(
        Guid accountId,
        string purpose,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        db.AccountTokens.Add(new AccountToken
        {
            AccountId = accountId,
            Purpose = purpose,
            TokenHash = Hash(token),
            ExpiresAt = DateTimeOffset.UtcNow.Add(ttl)
        });

        await db.SaveChangesAsync(cancellationToken);
        return token;
    }

    /// <summary>Consumes a token and returns its account id, or null if invalid.</summary>
    public async Task<Guid?> ConsumeAsync(string? token, string purpose, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = Hash(token);
        var row = await db.AccountTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose, cancellationToken);

        if (row is null || row.ConsumedAt is not null || row.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        row.ConsumedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return row.AccountId;
    }

    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
