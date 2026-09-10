using DoradoCloud.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Validation.AspNetCore;

namespace DoradoCloud.Modules.Identity;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddDoradoIdentity(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var postgres = configuration.GetConnectionString("Postgres");
        var sqlitePath = configuration["Auth:SqlitePath"] ?? "dorado-cloud-auth.db";

        services.AddDbContext<AuthDbContext>(options =>
        {
            if (!string.IsNullOrWhiteSpace(postgres))
            {
                options.UseNpgsql(postgres);
            }
            else
            {
                options.UseSqlite($"Data Source={sqlitePath}");
            }
        });

        var issuer = configuration["Auth:Issuer"] ?? "http://localhost:5080/";
        var disableTransportSecurity = configuration.GetValue("Auth:DisableTransportSecurity", environment.IsDevelopment());

        services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AuthDbContext>())
            .AddServer(options =>
            {
                options.SetIssuer(new Uri(issuer));
                options.SetAuthorizationEndpointUris("connect/authorize")
                       .SetTokenEndpointUris("connect/token")
                       .SetLogoutEndpointUris("connect/logout");

                options.AllowAuthorizationCodeFlow()
                       .AllowRefreshTokenFlow()
                       .AllowClientCredentialsFlow();

                options.RegisterScopes(
                    OpenIddictConstants.Scopes.OpenId,
                    OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Scopes.Email,
                    OpenIddictConstants.Scopes.OfflineAccess,
                    DoradoCloudInfo.ApiScope);

                options.AddDevelopmentEncryptionCertificate()
                       .AddDevelopmentSigningCertificate();

                var aspNetCore = options.UseAspNetCore()
                       .EnableAuthorizationEndpointPassthrough()
                       .EnableTokenEndpointPassthrough();

                if (disableTransportSecurity)
                {
                    aspNetCore.DisableTransportSecurityRequirement();
                }
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });
        services.AddAuthorization();

        return services;
    }

    /// <summary>Creates the auth schema (dev) and seeds the known clients/scopes.</summary>
    public static async Task UseDoradoIdentityAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DoradoCloud.Identity");

        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await db.Database.EnsureCreatedAsync();

        await IdentitySeeder.SeedAsync(scope.ServiceProvider);
        logger.LogInformation("Dorado identity ready (issuer {Issuer}).", app.Configuration["Auth:Issuer"]);
    }
}
