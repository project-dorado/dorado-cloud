namespace DoradoCloud.Modules.Artwork;

public sealed class ArtworkOptions
{
    public string CoverArtBaseUrl { get; set; } = "https://coverartarchive.org/";

    public string UserAgent { get; set; } = "DoradoCloud/0.1 (+https://github.com/project-dorado/dorado-cloud)";

    /// <summary>Hosts the proxy is permitted to fetch from (SSRF protection).</summary>
    public string[] AllowedHosts { get; set; } =
    [
        "coverartarchive.org",
        "archive.org",
        "fanart.tv",
        "theaudiodb.com",
        "wikimedia.org",
        "wikipedia.org"
    ];

    public int RateLimitMs { get; set; } = 200;

    /// <summary>Maximum accepted object size (bytes).</summary>
    public long MaxBytes { get; set; } = 12 * 1024 * 1024;
}

/// <summary>Thrown when a proxy request targets a host outside the allowlist.</summary>
public sealed class ArtworkHostNotAllowedException(string host)
    : Exception($"Host '{host}' is not an allowed artwork provider.")
{
    public string Host { get; } = host;
}
