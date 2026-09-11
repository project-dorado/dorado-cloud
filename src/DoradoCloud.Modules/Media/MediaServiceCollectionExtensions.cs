using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules.Media;

public static class MediaServiceCollectionExtensions
{
    /// <summary>
    /// Registers the DRM-free (PD/CC) media service. Disabled unless
    /// <c>Media:Enabled=true</c> (M6 is legal-gated).
    /// </summary>
    public static IServiceCollection AddDoradoMedia(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MediaOptions>(configuration.GetSection("Media"));
        services.AddHttpClient<MediaService>(client => client.Timeout = TimeSpan.FromSeconds(30));
        return services;
    }
}
