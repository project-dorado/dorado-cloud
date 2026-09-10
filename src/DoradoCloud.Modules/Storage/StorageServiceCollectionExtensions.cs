using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules.Storage;

public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddDoradoStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection("Storage"));

        var options = new StorageOptions();
        configuration.GetSection("Storage").Bind(options);

        if (string.Equals(options.Provider, "s3", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IObjectStorage, S3ObjectStorage>();
        }
        else
        {
            services.AddSingleton<IObjectStorage, LocalFileSystemObjectStorage>();
        }

        return services;
    }
}
