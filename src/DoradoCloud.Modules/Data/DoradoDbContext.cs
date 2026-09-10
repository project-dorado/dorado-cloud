using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DoradoCloud.Modules.Data;

/// <summary>
/// The Dorado Cloud database: OpenIddict applications/tokens plus accounts,
/// devices, per-account settings and the signed update feed.
/// </summary>
public sealed class DoradoDbContext(DbContextOptions<DoradoDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<UpdateRelease> UpdateReleases => Set<UpdateRelease>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // OpenIddict applications, authorizations, scopes and tokens.
        builder.UseOpenIddict();

        ConfigureSqliteDateTimeOffsets(builder);

        builder.Entity<Account>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.NormalizedEmail).IsUnique();
            entity.Property(a => a.Email).HasMaxLength(256).IsRequired();
            entity.Property(a => a.NormalizedEmail).HasMaxLength(256).IsRequired();
            entity.Property(a => a.DisplayName).HasMaxLength(128);
        });

        builder.Entity<Device>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.HasIndex(d => new { d.AccountId, d.Serial });
            entity.Property(d => d.Name).HasMaxLength(128).IsRequired();
            entity.Property(d => d.Platform).HasMaxLength(64).IsRequired();
        });

        builder.Entity<UserSettings>(entity =>
        {
            entity.HasKey(s => s.AccountId);
        });

        builder.Entity<UpdateRelease>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => new { r.App, r.Channel, r.PublishedAt });
            entity.Property(r => r.App).HasMaxLength(64).IsRequired();
            entity.Property(r => r.Channel).HasMaxLength(32).IsRequired();
            entity.Property(r => r.Version).HasMaxLength(64).IsRequired();
        });
    }

    /// <summary>
    /// SQLite cannot translate <see cref="DateTimeOffset"/> in ORDER BY clauses,
    /// so (for SQLite only) store them as sortable binary ticks. PostgreSQL keeps
    /// native timestamp types.
    /// </summary>
    private void ConfigureSqliteDateTimeOffsets(ModelBuilder builder)
    {
        if (Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) != true)
        {
            return;
        }

        var converter = new DateTimeOffsetToBinaryConverter();

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(converter);
                }
            }
        }
    }
}
