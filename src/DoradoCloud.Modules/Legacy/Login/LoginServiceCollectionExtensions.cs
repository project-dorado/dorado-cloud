using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules.Legacy.Login;

public static class LoginServiceCollectionExtensions
{
    /// <summary>
    /// Registers the gated <c>login.zune.net</c> WS-Trust bridge. Disabled
    /// unless <c>Legacy:Login:Enabled=true</c>.
    /// </summary>
    public static IServiceCollection AddDoradoLegacyLogin(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LegacyLoginOptions>(configuration.GetSection("Legacy:Login"));
        services.AddScoped<LegacyLoginService>();
        return services;
    }
}
