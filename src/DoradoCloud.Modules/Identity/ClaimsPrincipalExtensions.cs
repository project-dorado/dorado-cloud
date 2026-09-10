using System.Security.Claims;
using OpenIddict.Abstractions;

namespace DoradoCloud.Modules.Identity;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Account id for a user token, or null for service (client-credentials) tokens.</summary>
    public static Guid? GetAccountId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirst(OpenIddictConstants.Claims.Subject)?.Value
                      ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(subject, out var id) ? id : null;
    }

    public static string? GetEmail(this ClaimsPrincipal principal)
        => principal.FindFirst(OpenIddictConstants.Claims.Email)?.Value
           ?? principal.FindFirst(ClaimTypes.Email)?.Value;
}
