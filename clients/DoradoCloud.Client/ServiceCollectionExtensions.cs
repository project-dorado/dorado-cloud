using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Client;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a typed <see cref="DoradoCloudClient"/> pointed at the given
    /// base address. Works with either the official instance or a self-hosted
    /// deployment.
    /// </summary>
    public static IServiceCollection AddDoradoCloud(this IServiceCollection services, Uri baseAddress)
    {
        services.AddHttpClient<DoradoCloudClient>(client =>
        {
            client.BaseAddress = baseAddress;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "DoradoCloudClient/0.1 (+https://github.com/project-dorado/dorado-cloud)");
        });

        return services;
    }
}
