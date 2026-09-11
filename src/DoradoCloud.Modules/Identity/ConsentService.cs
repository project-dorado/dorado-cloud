using DoradoCloud.Modules.Data;
using Microsoft.EntityFrameworkCore;

namespace DoradoCloud.Modules.Identity;

/// <summary>Records and checks OIDC consent grants (M11).</summary>
public sealed class ConsentService(DoradoDbContext db)
{
    public static string NormalizeScopes(IEnumerable<string> scopes)
        => string.Join(' ', scopes.Where(s => !string.IsNullOrWhiteSpace(s)).OrderBy(s => s, StringComparer.Ordinal));

    public async Task<bool> HasConsentAsync(
        Guid accountId,
        string clientId,
        IEnumerable<string> scopes,
        CancellationToken cancellationToken)
    {
        var desired = NormalizeScopes(scopes);
        var grant = await db.ConsentGrants.AsNoTracking()
            .FirstOrDefaultAsync(c => c.AccountId == accountId && c.ClientId == clientId, cancellationToken);
        return grant is not null && grant.Scopes == desired;
    }

    public async Task GrantAsync(
        Guid accountId,
        string clientId,
        IEnumerable<string> scopes,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeScopes(scopes);
        var grant = await db.ConsentGrants
            .FirstOrDefaultAsync(c => c.AccountId == accountId && c.ClientId == clientId, cancellationToken);

        if (grant is null)
        {
            grant = new ConsentGrant { AccountId = accountId, ClientId = clientId };
            db.ConsentGrants.Add(grant);
        }

        grant.Scopes = normalized;
        grant.GrantedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
