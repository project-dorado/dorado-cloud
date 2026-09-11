using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Client;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a typed <see cref="DoradoCloudClient"/> pointed at the given
    /// base address. Works with either the official instance or a self-hosted
    /// deployment. Add <see cref="AddDoradoCloudAuth"/> to attach bearer tokens
    /// and refresh them automatically.
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

    /// <summary>
    /// Adds the centralized bearer-token pipeline to the typed
    /// <see cref="DoradoCloudClient"/>: attaches the credential from the
    /// registered <see cref="ICloudCredentialStore"/> and refreshes it via the
    /// OIDC <c>refresh_token</c> grant when expired (or once after a 401).
    ///
    /// <paramref name="baseAddress"/> must match the client's base address — it
    /// is the issuer the refresh request is sent to. <paramref name="clientId"/>
    /// defaults to the desktop public client (<c>dorado-desktop</c>).
    /// </summary>
    public static IServiceCollection AddDoradoCloudAuth(
        this IServiceCollection services,
        Uri baseAddress,
        string clientId = "dorado-desktop",
        TimeSpan? expirySkew = null,
        Func<IServiceProvider, ICloudCredentialStore>? credentialStoreFactory = null)
    {
        if (credentialStoreFactory is not null)
        {
            services.AddSingleton(credentialStoreFactory);
        }
        else if (!services.Any(d => d.ServiceType == typeof(ICloudCredentialStore)))
        {
            services.AddSingleton<ICloudCredentialStore, InMemoryCloudCredentialStore>();
        }

        services.AddTransient(sp => new DoradoCloudAuthHandler(
            sp.GetRequiredService<ICloudCredentialStore>(),
            baseAddress,
            clientId,
            expirySkew));

        services.AddHttpClient<DoradoCloudClient>()
            .AddHttpMessageHandler<DoradoCloudAuthHandler>();

        return services;
    }
}
