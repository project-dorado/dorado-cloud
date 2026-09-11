using DoradoCloud.Modules.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace DoradoCloud.Modules.Identity;

/// <summary>Account registration, credential validation, and the GDPR lifecycle (export/delete).</summary>
public sealed class AccountService(
    DoradoDbContext db,
    IPasswordHasher<Account> hasher,
    IOpenIddictTokenManager tokenManager,
    IOpenIddictAuthorizationManager authorizationManager)
{
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    public Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(email);
        return db.Accounts.FirstOrDefaultAsync(a => a.NormalizedEmail == normalized, cancellationToken);
    }

    public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<(Account? Account, string? Error)> RegisterAsync(
        string email,
        string password,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return (null, "A valid email address is required.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return (null, "Password must be at least 8 characters.");
        }

        if (await FindByEmailAsync(email, cancellationToken) is not null)
        {
            return (null, "An account with that email already exists.");
        }

        var account = new Account
        {
            Email = email.Trim(),
            NormalizedEmail = Normalize(email),
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? email.Split('@')[0]
                : displayName.Trim()
        };
        account.PasswordHash = hasher.HashPassword(account, password);

        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return (account, null);
    }

    public async Task<Account?> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var account = await FindByEmailAsync(email, cancellationToken);
        if (account is null || !account.IsActive)
        {
            return null;
        }

        var result = hasher.VerifyHashedPassword(account, account.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        account.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return account;
    }

    /// <summary>Assembles a machine-readable export of everything stored for an account (GDPR portability).</summary>
    public async Task<object> ExportAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        var devices = await db.Devices.AsNoTracking().Where(d => d.AccountId == accountId).ToListAsync(cancellationToken);
        var settings = await db.UserSettings.AsNoTracking().FirstOrDefaultAsync(s => s.AccountId == accountId, cancellationToken);
        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.AccountId == accountId, cancellationToken);
        var activities = await db.Activities.AsNoTracking().Where(a => a.AccountId == accountId).ToListAsync(cancellationToken);
        var badges = await db.Badges.AsNoTracking().Where(b => b.AccountId == accountId).ToListAsync(cancellationToken);
        var follows = await db.Follows.AsNoTracking()
            .Where(f => f.FollowerAccountId == accountId || f.FolloweeAccountId == accountId)
            .ToListAsync(cancellationToken);
        var messages = await db.InboxMessages.AsNoTracking()
            .Where(m => m.SenderAccountId == accountId)
            .ToListAsync(cancellationToken);

        return new
        {
            exportedAt = DateTimeOffset.UtcNow,
            account = account is null ? null : new
            {
                account.Id,
                account.Email,
                account.DisplayName,
                account.CreatedAt,
                account.LastLoginAt,
                account.IsActive,
            },
            devices = devices.Select(DeviceService.ToDto).ToList(),
            settings = settings is null ? null : new { settings.PayloadJson, settings.Version, settings.UpdatedAt },
            profile = profile is null ? null : new
            {
                profile.Handle,
                profile.DisplayName,
                profile.Bio,
                profile.CreatedAt,
                profile.UpdatedAt,
            },
            activities = activities.Select(a => new { a.Id, a.Kind, a.PayloadJson, a.CreatedAt }).ToList(),
            badges = badges.Select(b => new { b.Code, b.EarnedAt }).ToList(),
            follows = follows.Select(f => new { f.FollowerAccountId, f.FolloweeAccountId, f.CreatedAt }).ToList(),
            messages = messages.Select(m => new { m.Id, m.RecipientTag, m.Subject, m.Body, m.CreatedAt }).ToList(),
        };
    }

    /// <summary>Deletes an account and all associated data, revoking its OIDC tokens/authorizations (GDPR erasure).</summary>
    public async Task<bool> DeleteAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null)
        {
            return false;
        }

        // Revoke outstanding tokens/authorizations so refresh tokens stop working.
        var subject = accountId.ToString();
        await foreach (var token in tokenManager.FindBySubjectAsync(subject, cancellationToken))
        {
            await tokenManager.DeleteAsync(token, cancellationToken);
        }

        await foreach (var authorization in authorizationManager.FindBySubjectAsync(subject, cancellationToken))
        {
            await authorizationManager.DeleteAsync(authorization, cancellationToken);
        }

        db.Devices.RemoveRange(db.Devices.Where(d => d.AccountId == accountId));
        db.UserSettings.RemoveRange(db.UserSettings.Where(s => s.AccountId == accountId));
        db.Profiles.RemoveRange(db.Profiles.Where(p => p.AccountId == accountId));
        db.Activities.RemoveRange(db.Activities.Where(a => a.AccountId == accountId));
        db.Badges.RemoveRange(db.Badges.Where(b => b.AccountId == accountId));
        db.Follows.RemoveRange(db.Follows.Where(f => f.FollowerAccountId == accountId || f.FolloweeAccountId == accountId));
        db.Blocks.RemoveRange(db.Blocks.Where(b => b.BlockerAccountId == accountId || b.BlockedAccountId == accountId));
        db.Reports.RemoveRange(db.Reports.Where(r => r.ReporterAccountId == accountId));
        db.InboxMessages.RemoveRange(db.InboxMessages.Where(m => m.SenderAccountId == accountId));
        db.Accounts.Remove(account);

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
