using DoradoCloud.Modules.Data;
using DoradoCloud.Modules.Legacy.Session;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Tests.Legacy;

/// <summary>Unit tests for the legacy session store (hashing, lookup, expiry).</summary>
public sealed class LegacySessionTests : IDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), "dorado-session-" + Guid.NewGuid().ToString("N") + ".db");

    private DoradoDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<DoradoDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        var db = new DoradoDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static LegacySessionService Service(DoradoDbContext db)
        => new(db, Options.Create(new LegacySessionOptions { TtlHours = 1 }));

    [Fact]
    public async Task Issued_ticket_resolves_and_is_stored_hashed()
    {
        var accountId = Guid.NewGuid();

        using var db = NewContext();
        var service = Service(db);
        var token = await service.IssueAsync(accountId, CancellationToken.None);

        Assert.Equal(accountId, await service.ResolveAsync(token, CancellationToken.None));
        Assert.Null(await service.ResolveAsync("not-a-ticket", CancellationToken.None));

        var stored = await db.LegacySessions.SingleAsync();
        Assert.NotEqual(token, stored.TokenHash);
        Assert.Equal(LegacySessionService.Hash(token), stored.TokenHash);
    }

    [Fact]
    public async Task Expired_ticket_does_not_resolve()
    {
        var accountId = Guid.NewGuid();
        string token;

        using (var db = NewContext())
        {
            token = await Service(db).IssueAsync(accountId, CancellationToken.None);
        }

        using (var db = NewContext())
        {
            var stored = await db.LegacySessions.SingleAsync();
            stored.ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();
        }

        using (var db = NewContext())
        {
            Assert.Null(await Service(db).ResolveAsync(token, CancellationToken.None));
        }
    }

    public void Dispose()
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            try
            {
                var path = _dbPath + suffix;
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best effort.
            }
        }
    }
}
