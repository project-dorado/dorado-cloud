namespace DoradoCloud.Modules.Providers;

/// <summary>
/// Optional enrichment provider credentials (M12). Each provider is used only
/// when its key is configured; the shipped defaults require no keys.
/// </summary>
public sealed class EnrichmentOptions
{
    public FanartTvOptions FanartTv { get; set; } = new();
    public TheAudioDbOptions TheAudioDb { get; set; } = new();
    public DiscogsOptions Discogs { get; set; } = new();

    public string UserAgent { get; set; } = "DoradoCloud/0.1 (+https://github.com/project-dorado/dorado-cloud)";
}

public sealed class FanartTvOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

public sealed class TheAudioDbOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

public sealed class DiscogsOptions
{
    public string Token { get; set; } = string.Empty;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Token);
}
