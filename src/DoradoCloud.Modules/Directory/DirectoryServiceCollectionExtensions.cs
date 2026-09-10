using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules.Directory;

public static class DirectoryServiceCollectionExtensions
{
    public static IServiceCollection AddDoradoDirectory(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DirectoryOptions>(configuration.GetSection("Directory"));

        var options = new DirectoryOptions();
        configuration.GetSection("Directory").Bind(options);

        services.AddHttpClient<PodcastIndexClient>(client =>
        {
            client.BaseAddress = new Uri(options.PodcastIndex.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddHttpClient<RadioBrowserClient>(client =>
        {
            client.BaseAddress = new Uri(options.RadioBrowser.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}
