using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules.Legacy.Resources;

public static class ResourcesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <c>resources.zune.net</c> compatibility service. Firmware
    /// CABs are read from <c>Resources:CorpusRoot</c> (external, untracked).
    /// </summary>
    public static IServiceCollection AddDoradoLegacyResources(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ResourcesOptions>(configuration.GetSection("Resources"));
        services.AddSingleton<FirmwareCatalog>();
        return services;
    }
}
