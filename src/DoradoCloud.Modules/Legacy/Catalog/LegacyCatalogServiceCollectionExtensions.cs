using DoradoCloud.Legacy;
using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules.Legacy.Catalog;

public static class LegacyCatalogServiceCollectionExtensions
{
    /// <summary>
    /// Registers the legacy <c>catalog.zune.net</c> and
    /// <c>image.catalog.zune.net</c> compatibility services, backed by the
    /// MusicBrainz client and artwork CDN.
    /// </summary>
    public static IServiceCollection AddDoradoLegacyCatalog(this IServiceCollection services)
    {
        services.AddSingleton<ILegacyIdMapper, LegacyIdMapper>();
        services.AddSingleton<ILegacyMusicCatalog, LegacyMusicCatalog>();
        services.AddSingleton<ILegacyPodcastCatalog, LegacyPodcastCatalog>();
        services.AddSingleton<ILegacyArtwork, LegacyArtwork>();

        // Keyless artist imagery: Wikidata P18 -> Wikimedia Commons.
        services.AddHttpClient<WikidataClient>(client =>
        {
            client.BaseAddress = new Uri("https://www.wikidata.org/");
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        // SSRF-guarded podcast RSS passthrough.
        services.AddHttpClient<RssProxyClient>(client => client.Timeout = TimeSpan.FromSeconds(15));

        return services;
    }
}
