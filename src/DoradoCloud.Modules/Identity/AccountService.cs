using DoradoCloud.Modules.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DoradoCloud.Modules.Identity;

/// <summary>Account registration and credential validation (PBKDF2 via PasswordHasher).</summary>
public sealed class AccountService(DoradoDbContext db, IPasswordHasher<Account> hasher)
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
}
