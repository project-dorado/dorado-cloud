using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules.Recommendations;

public static class RecommendationsServiceCollectionExtensions
{
    public static IServiceCollection AddDoradoRecommendations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmbeddingOptions>(configuration.GetSection("Embedding"));
        services.AddScoped<QuickMixService>();
        services.AddScoped<EmbeddingService>();
        return services;
    }
}
