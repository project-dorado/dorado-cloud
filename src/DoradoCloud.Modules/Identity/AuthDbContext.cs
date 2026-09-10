using Microsoft.EntityFrameworkCore;

namespace DoradoCloud.Modules.Identity;

/// <summary>
/// Persistence for OpenIddict applications, authorizations, scopes and tokens.
/// M0 runs on SQLite by default and switches to PostgreSQL when a connection
/// string is configured.
/// </summary>
public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Registers the default OpenIddict entity set (applications,
        // authorizations, scopes, tokens) using string keys.
        builder.UseOpenIddict();
    }
}
