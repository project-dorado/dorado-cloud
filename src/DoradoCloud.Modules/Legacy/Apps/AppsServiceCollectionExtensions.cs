using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules.Legacy.Apps;

public static class AppsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the read-only Zune HD app catalog. Packages are read from
    /// <c>Apps:CorpusRoot</c> (external, untracked).
    /// </summary>
    public static IServiceCollection AddDoradoLegacyApps(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppsOptions>(configuration.GetSection("Apps"));
        services.AddSingleton<ILegacyAppsCatalog, LegacyAppsCatalog>();
        return services;
    }
}
