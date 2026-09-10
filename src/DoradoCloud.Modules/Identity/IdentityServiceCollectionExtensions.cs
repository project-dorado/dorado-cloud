using DoradoCloud.Modules.Data;
using DoradoCloud.Shared;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
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

        services.AddDbContext<DoradoDbContext>(options =>
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
        var useDevelopmentCertificates = configuration.GetValue("Auth:UseDevelopmentCertificates", environment.IsDevelopment());

        services.AddSingleton<CertificateKeyProvider>();

        services.AddScoped<AccountService>();
        services.AddScoped<DeviceService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<IPasswordHasher<Account>, PasswordHasher<Account>>();

        services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<DoradoDbContext>())
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

                if (useDevelopmentCertificates)
                {
                    options.AddDevelopmentEncryptionCertificate()
                           .AddDevelopmentSigningCertificate();
                }
                else
                {
                    // Persistent, operator-managed key material (shared across replicas).
                    var keyProvider = new CertificateKeyProvider(
                        configuration, NullLogger<CertificateKeyProvider>.Instance);
                    options.AddSigningCertificate(keyProvider.GetSigningCertificate())
                           .AddEncryptionCertificate(keyProvider.GetEncryptionCertificate());
                }

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
        })
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.LoginPath = "/account/login";
            options.LogoutPath = "/account/logout";
            options.Cookie.Name = "dorado.sid";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = disableTransportSecurity
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("UpdatesAdmin", policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context =>
                {
                    var admins = configuration.GetSection("Updates:Admins").Get<string[]>();
                    if (admins is null || admins.Length == 0)
                    {
                        return true; // no allowlist configured (single-operator/dev default)
                    }

                    var email = context.User.GetEmail();
                    var subject = context.User.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
                    return (email is not null && admins.Contains(email, StringComparer.OrdinalIgnoreCase))
                           || (subject is not null && admins.Contains(subject, StringComparer.OrdinalIgnoreCase));
                }));
        });

        return services;
    }

    /// <summary>Creates the schema (dev) and seeds the known clients/scopes.</summary>
    public static async Task UseDoradoIdentityAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DoradoCloud.Identity");

        var db = scope.ServiceProvider.GetRequiredService<DoradoDbContext>();
        await db.Database.EnsureCreatedAsync();

        await IdentitySeeder.SeedAsync(scope.ServiceProvider);
        logger.LogInformation("Dorado identity ready (issuer {Issuer}).", app.Configuration["Auth:Issuer"]);
    }
}
