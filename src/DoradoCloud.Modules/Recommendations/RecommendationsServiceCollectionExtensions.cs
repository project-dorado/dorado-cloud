using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules.Recommendations;

public static class RecommendationsServiceCollectionExtensions
{
    public static IServiceCollection AddDoradoRecommendations(this IServiceCollection services)
    {
        services.AddScoped<QuickMixService>();
        return services;
    }
}
