using DoradoCloud.Modules.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules.Catalog;

public static class CatalogServiceCollectionExtensions
{
    public static IServiceCollection AddDoradoCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CatalogOptions>(configuration.GetSection("Catalog"));
        services.AddSingleton<HostRateLimiter>();

        var options = new CatalogOptions();
        configuration.GetSection("Catalog").Bind(options);

        services.AddHttpClient<MusicBrainzClient>(client =>
        {
            client.BaseAddress = new Uri(options.MusicBrainzBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        return services;
    }
}
