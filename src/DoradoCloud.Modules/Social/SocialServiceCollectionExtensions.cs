using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules.Social;

public static class SocialServiceCollectionExtensions
{
    public static IServiceCollection AddDoradoSocial(this IServiceCollection services)
    {
        services.AddScoped<SocialService>();
        return services;
    }
}
