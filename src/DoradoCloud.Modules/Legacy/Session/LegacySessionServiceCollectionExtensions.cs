using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules.Legacy.Session;

public static class LegacySessionServiceCollectionExtensions
{
    /// <summary>Registers the legacy Zune session store (ticket hashing + lookup).</summary>
    public static IServiceCollection AddDoradoLegacySessions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LegacySessionOptions>(configuration.GetSection("Legacy:Session"));
        services.AddScoped<LegacySessionService>();
        return services;
    }
}
