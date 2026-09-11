using DoradoCloud.Shared;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace DoradoCloud.Modules.Identity;

/// <summary>Idempotently registers the Dorado OAuth clients and API scope.</summary>
public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, bool includeDevSmokeClient = true)
    {
        var scopeManager = services.GetRequiredService<IOpenIddictScopeManager>();
        if (await scopeManager.FindByNameAsync(DoradoCloudInfo.ApiScope) is null)
        {
            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = DoradoCloudInfo.ApiScope,
                DisplayName = "Dorado API",
                Resources = { DoradoCloudInfo.ApiResource }
            });
        }

        var appManager = services.GetRequiredService<IOpenIddictApplicationManager>();

        await EnsurePublicClientAsync(appManager, "dorado-desktop", "Dorado Desktop", "http://127.0.0.1:7890/callback");
        await EnsurePublicClientAsync(appManager, "dorado-hd", "Dorado-HD (Android)", "doradohd://callback");

        // The confidential smoke client uses a well-known development secret and
        // must never be seeded outside development.
        if (includeDevSmokeClient)
        {
            await EnsureConfidentialClientAsync(appManager, "dorado-cloud-smoke", "Dorado Cloud Smoke", "dev-secret");
        }
    }

    private static readonly string[] CommonScopes =
    {
        OpenIddictConstants.Permissions.Scopes.Email,
        OpenIddictConstants.Permissions.Scopes.Profile,
        OpenIddictConstants.Permissions.Prefixes.Scope + DoradoCloudInfo.ApiScope,
        OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess
    };

    private static async Task EnsurePublicClientAsync(
        IOpenIddictApplicationManager manager,
        string clientId,
        string displayName,
        string redirectUri)
    {
        if (await manager.FindByClientIdAsync(clientId) is not null)
        {
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = displayName,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            RedirectUris = { new Uri(redirectUri) },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code
            },
            Requirements = { OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange }
        };

        foreach (var permission in CommonScopes)
        {
            descriptor.Permissions.Add(permission);
        }

        await manager.CreateAsync(descriptor);
    }

    private static async Task EnsureConfidentialClientAsync(
        IOpenIddictApplicationManager manager,
        string clientId,
        string displayName,
        string secret)
    {
        if (await manager.FindByClientIdAsync(clientId) is not null)
        {
            return;
        }

        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = displayName,
            ClientSecret = secret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.Prefixes.Scope + DoradoCloudInfo.ApiScope
            }
        });
    }
}
