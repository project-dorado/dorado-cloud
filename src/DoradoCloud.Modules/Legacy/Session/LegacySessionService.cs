using System.Security.Cryptography;
using System.Text;
using DoradoCloud.Modules.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Legacy.Session;

/// <summary>
/// Issues and resolves legacy Zune session tickets. The raw ticket is never
/// persisted: only its SHA-256 hash is stored, so a database leak cannot be
/// replayed against the service.
/// </summary>
public sealed class LegacySessionService(DoradoDbContext db, IOptions<LegacySessionOptions> options)
{
    public async Task<string> IssueAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var ttlHours = options.Value.TtlHours > 0 ? options.Value.TtlHours : 720;

        db.LegacySessions.Add(new LegacySession
        {
            TokenHash = Hash(token),
            AccountId = accountId,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(ttlHours)
        });

        await db.SaveChangesAsync(cancellationToken);
        return token;
    }

    /// <summary>Returns the account id for a non-expired ticket, or null.</summary>
    public async Task<Guid?> ResolveAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = Hash(token);
        var session = await db.LegacySessions.FirstOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);
        if (session is null || session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        session.LastUsedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return session.AccountId;
    }

    /// <summary>Uppercase SHA-256 hex of a ticket (the stored form).</summary>
    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
