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
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<Block> Blocks => Set<Block>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<LegacySession> LegacySessions => Set<LegacySession>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

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

        builder.Entity<Profile>(entity =>
        {
            entity.HasKey(p => p.AccountId);
            entity.HasIndex(p => p.Handle).IsUnique();
            entity.Property(p => p.Handle).HasMaxLength(32).IsRequired();
            entity.Property(p => p.DisplayName).HasMaxLength(128).IsRequired();
            entity.Property(p => p.Bio).HasMaxLength(512);
        });

        builder.Entity<Follow>(entity =>
        {
            entity.HasKey(f => new { f.FollowerAccountId, f.FolloweeAccountId });
            entity.HasIndex(f => f.FolloweeAccountId);
        });

        builder.Entity<Activity>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => new { a.AccountId, a.CreatedAt });
            entity.Property(a => a.Kind).HasMaxLength(48).IsRequired();
        });

        builder.Entity<Badge>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.HasIndex(b => new { b.AccountId, b.Code }).IsUnique();
            entity.Property(b => b.Code).HasMaxLength(48).IsRequired();
        });

        builder.Entity<Block>(entity =>
        {
            entity.HasKey(b => new { b.BlockerAccountId, b.BlockedAccountId });
        });

        builder.Entity<Report>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.Status);
            entity.Property(r => r.Reason).HasMaxLength(512).IsRequired();
            entity.Property(r => r.Status).HasMaxLength(24).IsRequired();
        });

        builder.Entity<InboxMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => new { m.RecipientTag, m.CreatedAt });
            entity.Property(m => m.SenderTag).HasMaxLength(32);
            entity.Property(m => m.RecipientTag).HasMaxLength(32).IsRequired();
            entity.Property(m => m.Subject).HasMaxLength(256);
            entity.Property(m => m.Body).HasMaxLength(4096);
        });

        builder.Entity<LegacySession>(entity =>
        {
            entity.HasKey(s => s.TokenHash);
            entity.HasIndex(s => s.AccountId);
            entity.Property(s => s.TokenHash).HasMaxLength(64).IsRequired();
        });

        builder.Entity<MediaAsset>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.ContentHash);
            entity.Property(a => a.Title).HasMaxLength(256).IsRequired();
            entity.Property(a => a.Creator).HasMaxLength(256);
            entity.Property(a => a.License).HasMaxLength(64).IsRequired();
            entity.Property(a => a.LicenseUrl).HasMaxLength(512);
            entity.Property(a => a.SourceUrl).HasMaxLength(1024).IsRequired();
            entity.Property(a => a.Provider).HasMaxLength(64);
            entity.Property(a => a.ContentType).HasMaxLength(128);
            entity.Property(a => a.ContentHash).HasMaxLength(64).IsRequired();
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
