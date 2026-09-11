namespace DoradoCloud.Modules.Media;

/// <summary>
/// Configuration for DRM-free media. M6 is legal-gated and therefore
/// <b>disabled by default</b>; only public-domain / Creative Commons content is
/// accepted, and only sources whose license is on the allowlist.
/// </summary>
public sealed class MediaOptions
{
    /// <summary>Enable media streaming/ingestion. Default <c>false</c> (501).</summary>
    public bool Enabled { get; set; }

    /// <summary>Licenses accepted for ingestion (exact, case-insensitive).</summary>
    public string[] AllowedLicenses { get; set; } =
    {
        "PD", "CC0", "CC-BY", "CC-BY-SA", "CC-BY-4.0", "CC-BY-SA-4.0"
    };

    /// <summary>Maximum source size accepted on ingestion.</summary>
    public long MaxBytes { get; set; } = 25 * 1024 * 1024;
}
