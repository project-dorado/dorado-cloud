using System.Net;
using System.Net.Http.Json;
using DoradoCloud.Modules.Data;
using DoradoCloud.Modules.Recommendations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Tests.Legacy;

/// <summary>Unit tests for the similarity embedding store and cosine ranking.</summary>
public sealed class EmbeddingServiceTests : IDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), "dorado-embed-" + Guid.NewGuid().ToString("N") + ".db");

    private DoradoDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<DoradoDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        var db = new DoradoDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static EmbeddingService Service(DoradoDbContext db)
        => new(db, Options.Create(new EmbeddingOptions { Enabled = true }), NullLogger<EmbeddingService>.Instance);

    [Fact]
    public async Task Upserts_and_ranks_neighbours_by_cosine()
    {
        using var db = NewContext();
        var service = Service(db);

        await service.UpsertAsync("a", "A", new[] { 1f, 0f, 0f }, CancellationToken.None);
        await service.UpsertAsync("b", "B", new[] { 0f, 1f, 0f }, CancellationToken.None);
        await service.UpsertAsync("c", "C", new[] { 0.9f, 0.1f, 0f }, CancellationToken.None);

        Assert.Equal(new[] { 1f, 0f, 0f }, await service.GetVectorAsync("a", CancellationToken.None));

        var nearest = await service.NearestAsync(new[] { 1f, 0f, 0f }, 2, CancellationToken.None);

        Assert.Equal(2, nearest.Count);
        Assert.Equal("a", nearest[0].Mbid);
        Assert.Equal("c", nearest[1].Mbid);
        Assert.True(nearest[0].Score > nearest[1].Score);
    }

    [Fact]
    public async Task Upsert_replaces_an_existing_vector()
    {
        using var db = NewContext();
        var service = Service(db);

        await service.UpsertAsync("a", "A", new[] { 1f, 0f }, CancellationToken.None);
        await service.UpsertAsync("a", "A2", new[] { 0f, 1f }, CancellationToken.None);

        Assert.Equal(1, await service.CountAsync(CancellationToken.None));
        Assert.Equal(new[] { 0f, 1f }, await service.GetVectorAsync("a", CancellationToken.None));
    }

    [Theory]
    [InlineData(new[] { 1f, 0f }, new[] { 1f, 0f }, 1.0)]
    [InlineData(new[] { 1f, 0f }, new[] { 0f, 1f }, 0.0)]
    [InlineData(new[] { 1f, 0f }, new[] { -1f, 0f }, -1.0)]
    public void Cosine_is_computed(float[] a, float[] b, double expected)
        => Assert.Equal(expected, EmbeddingService.Cosine(a, b), 6);

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

/// <summary>HTTP wiring for embedding ingestion.</summary>
public sealed class EmbeddingEndpointTests(CloudApiFactory factory) : IClassFixture<CloudApiFactory>
{
    [Fact]
    public async Task Ingest_requires_admin()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/v1/recs/embeddings", new { mbid = "abc", vector = new[] { 1f, 0f } });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
