using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules.Artwork;

public static class ArtworkServiceCollectionExtensions
{
    public static IServiceCollection AddDoradoArtwork(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ArtworkOptions>(configuration.GetSection("Artwork"));

        services.AddHttpClient<ArtworkService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        return services;
    }
}
