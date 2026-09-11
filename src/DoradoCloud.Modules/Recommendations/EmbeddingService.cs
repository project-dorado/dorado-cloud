using System.Data.Common;
using System.Text.Json;
using DoradoCloud.Modules.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Recommendations;

/// <summary>A stored embedding's metadata (not the vector itself).</summary>
public sealed record EmbeddingRecord(string Mbid, string Title, int Dimensions, DateTimeOffset UpdatedAt);

/// <summary>A neighbour and its cosine score in <c>[-1, 1]</c>.</summary>
public sealed record EmbeddingNeighbour(string Mbid, string Title, double Score);

/// <summary>
/// Stores per-MBID similarity embeddings and answers nearest-neighbour queries.
/// The vector is stored as JSON so the same store works on SQLite and
/// PostgreSQL; a pgvector-accelerated path is used when enabled.
/// </summary>
public sealed class EmbeddingService(
    DoradoDbContext db,
    IOptions<EmbeddingOptions> options,
    ILogger<EmbeddingService> logger)
{
    public bool Enabled => options.Value.Enabled;

    public async Task<EmbeddingRecord> UpsertAsync(
        string mbid,
        string title,
        float[] vector,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mbid))
        {
            throw new ArgumentException("MBID is required.", nameof(mbid));
        }

        if (vector.Length == 0)
        {
            throw new ArgumentException("Vector must not be empty.", nameof(vector));
        }

        var existing = await db.TrackEmbeddings.FirstOrDefaultAsync(e => e.Mbid == mbid, cancellationToken);
        if (existing is null)
        {
            existing = new TrackEmbedding { Mbid = mbid };
            db.TrackEmbeddings.Add(existing);
        }

        existing.Title = title ?? string.Empty;
        existing.EmbeddingJson = JsonSerializer.Serialize(vector);
        existing.Dimensions = vector.Length;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        if (options.Value.UsePgvector)
        {
            await TryWritePgVectorAsync(mbid, vector, cancellationToken);
        }

        return new EmbeddingRecord(existing.Mbid, existing.Title, existing.Dimensions, existing.UpdatedAt);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken)
        => await db.TrackEmbeddings.CountAsync(cancellationToken);

    /// <summary>The stored vector for an MBID, or null.</summary>
    public async Task<float[]?> GetVectorAsync(string mbid, CancellationToken cancellationToken)
    {
        var row = await db.TrackEmbeddings.AsNoTracking().FirstOrDefaultAsync(e => e.Mbid == mbid, cancellationToken);
        return row is null ? null : Deserialize(row.EmbeddingJson);
    }

    public async Task<IReadOnlyList<EmbeddingNeighbour>> NearestAsync(
        float[] query,
        int k,
        CancellationToken cancellationToken)
    {
        if (query.Length == 0)
        {
            return [];
        }

        k = Math.Clamp(k, 1, 100);

        if (options.Value.UsePgvector)
        {
            var accelerated = await TryPgVectorAsync(query, k, cancellationToken);
            if (accelerated is not null)
            {
                return accelerated;
            }
        }

        var all = await db.TrackEmbeddings.AsNoTracking().ToListAsync(cancellationToken);
        return all
            .Select(e => (e.Mbid, e.Title, Vector: Deserialize(e.EmbeddingJson)))
            .Where(x => x.Vector.Length == query.Length)
            .Select(x => new EmbeddingNeighbour(x.Mbid, x.Title, Cosine(query, x.Vector)))
            .OrderByDescending(n => n.Score)
            .ThenBy(n => n.Mbid, StringComparer.Ordinal)
            .Take(k)
            .ToList();
    }

    /// <summary>Cosine similarity of two equal-length vectors.</summary>
    public static double Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0)
        {
            return 0;
        }

        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
        {
            return 0;
        }

        return dot / Math.Sqrt(normA * normB);
    }

    private static float[] Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<float[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task TryWritePgVectorAsync(string mbid, float[] vector, CancellationToken cancellationToken)
    {
        try
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                "UPDATE \"TrackEmbeddings\" SET \"Embedding\" = CAST(@vector AS vector) WHERE \"Mbid\" = @mbid";
            AddParameter(command, "@vector", PgVectorLiteral(vector));
            AddParameter(command, "@mbid", mbid);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            logger.LogDebug(ex, "pgvector write unavailable; the JSON store remains the source of truth.");
        }
    }

    private async Task<IReadOnlyList<EmbeddingNeighbour>?> TryPgVectorAsync(
        float[] query,
        int k,
        CancellationToken cancellationToken)
    {
        try
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT \"Mbid\", \"Title\", 1 - (\"Embedding\" <=> CAST(@vector AS vector)) AS score " +
                "FROM \"TrackEmbeddings\" WHERE \"Embedding\" IS NOT NULL " +
                "ORDER BY \"Embedding\" <=> CAST(@vector AS vector) LIMIT @k";
            AddParameter(command, "@vector", PgVectorLiteral(query));
            AddParameter(command, "@k", k);

            var results = new List<EmbeddingNeighbour>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new EmbeddingNeighbour(
                    reader.GetString(0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    reader.IsDBNull(2) ? 0 : Convert.ToDouble(reader.GetValue(2))));
            }

            return results;
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            logger.LogDebug(ex, "pgvector query unavailable; falling back to the in-memory scan.");
            return null;
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string PgVectorLiteral(float[] vector)
        => "[" + string.Join(",", vector.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";
}
