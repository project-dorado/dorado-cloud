namespace DoradoCloud.Modules.Directory;

public sealed class DirectoryOptions
{
    public PodcastIndexOptions PodcastIndex { get; set; } = new();
    public RadioBrowserOptions RadioBrowser { get; set; } = new();

    /// <summary>How long provider responses are cached (seconds).</summary>
    public int CacheSeconds { get; set; } = 300;

    public const string AttributionPodcast = "Podcast data from Podcast Index (podcastindex.org)";
    public const string AttributionRadio = "Radio stations from Radio-Browser (radio-browser.info)";
}

public sealed class PodcastIndexOptions
{
    public string BaseUrl { get; set; } = "https://api.podcastindex.org/";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string UserAgent { get; set; } = "DoradoCloud/0.1 (+https://github.com/project-dorado/dorado-cloud)";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);
}

public sealed class RadioBrowserOptions
{
    public string BaseUrl { get; set; } = "https://de1.api.radio-browser.info/";
    public string UserAgent { get; set; } = "DoradoCloud/0.1 (+https://github.com/project-dorado/dorado-cloud)";
    public bool Enabled { get; set; } = true;
}
