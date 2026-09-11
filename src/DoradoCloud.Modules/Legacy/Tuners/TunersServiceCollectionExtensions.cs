using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules.Legacy.Tuners;

public static class TunersServiceCollectionExtensions
{
    /// <summary>Registers the <c>tuners.zune.net</c> PC-client resource service.</summary>
    public static IServiceCollection AddDoradoLegacyTuners(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TunersOptions>(configuration.GetSection("Tuners"));
        services.AddSingleton<ClientFileStore>();
        return services;
    }
}
