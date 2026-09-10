using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules.Updates;

public static class UpdatesServiceCollectionExtensions
{
    public static IServiceCollection AddDoradoUpdates(this IServiceCollection services)
    {
        services.AddSingleton<UpdateSigningKeyProvider>();
        services.AddScoped<ReleaseService>();
        return services;
    }
}
