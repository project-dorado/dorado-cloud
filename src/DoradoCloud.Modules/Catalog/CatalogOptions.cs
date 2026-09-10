namespace DoradoCloud.Modules.Catalog;

public sealed class CatalogOptions
{
    public string MusicBrainzBaseUrl { get; set; } = "https://musicbrainz.org/";
    public string CoverArtBaseUrl { get; set; } = "https://coverartarchive.org/";
    public string UserAgent { get; set; } = "DoradoCloud/0.1 (+https://github.com/project-dorado/dorado-cloud)";

    /// <summary>Minimum spacing between MusicBrainz calls (their policy is ~1 req/s).</summary>
    public int MusicBrainzRateLimitMs { get; set; } = 1000;

    public int CacheSeconds { get; set; } = 3600;

    public const string Attribution = "Metadata from MusicBrainz (musicbrainz.org); artwork via Cover Art Archive";
}
