namespace DoradoCloud.Modules.Recommendations;

/// <summary>
/// Configuration for similarity embeddings (M10). Ingestion is opt-in; vector
/// search uses an in-memory cosine scan by default and a pgvector-accelerated
/// query when <see cref="UsePgvector"/> is enabled (requires the extension and
/// the schema from <c>deploy/pgvector/init.sql</c>).
/// </summary>
public sealed class EmbeddingOptions
{
    /// <summary>Enable embedding ingestion (admin endpoint).</summary>
    public bool Enabled { get; set; }

    /// <summary>Use the pgvector <c>&lt;=&gt;</c> operator instead of an in-memory scan.</summary>
    public bool UsePgvector { get; set; }

    /// <summary>Expected vector dimensions (informational).</summary>
    public int Dimensions { get; set; } = 1536;
}
