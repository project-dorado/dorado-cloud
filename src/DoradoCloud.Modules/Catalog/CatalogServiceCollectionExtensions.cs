using DoradoCloud.Modules.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules.Catalog;

public static class CatalogServiceCollectionExtensions
{
    public static IServiceCollection AddDoradoCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CatalogOptions>(configuration.GetSection("Catalog"));
        services.Configure<EnrichmentOptions>(configuration.GetSection("Providers"));
        services.AddSingleton<HostRateLimiter>();

        var options = new CatalogOptions();
        configuration.GetSection("Catalog").Bind(options);

        services.AddHttpClient<MusicBrainzClient>(client =>
        {
            client.BaseAddress = new Uri(options.MusicBrainzBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        // Optional enrichment providers (used only when a key is configured).
        services.AddHttpClient<FanartTvClient>(client =>
        {
            client.BaseAddress = new Uri("https://webservice.fanart.tv/");
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddHttpClient<TheAudioDbClient>(client =>
        {
            client.BaseAddress = new Uri("https://theaudiodb.com/");
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddHttpClient<DiscogsClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.discogs.com/");
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        return services;
    }
}
