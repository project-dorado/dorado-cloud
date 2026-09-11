using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DoradoCloud.Modules.Legacy.Session;

/// <summary>
/// Resolves the legacy Zune session ticket the original client sends as
/// <c>Authorization: WLID1.0 &lt;ticket&gt;</c> and, when valid, sets the
/// request principal to the owning account. Runs after the modern
/// authentication middleware; it never overwrites an already-authenticated
/// principal unless a matching legacy session is found.
/// </summary>
public sealed class LegacySessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, LegacySessionService sessions)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(header))
        {
            var accountId = await sessions.ResolveAsync(ExtractTicket(header), context.RequestAborted);
            if (accountId is Guid id)
            {
                var identity = new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) },
                    authenticationType: "LegacyZune");
                context.User = new ClaimsPrincipal(identity);
            }
        }

        await next(context);
    }

    /// <summary>Strips a leading auth scheme ("WLID1.0 ", "Bearer ", …) if present.</summary>
    internal static string ExtractTicket(string header)
    {
        var space = header.IndexOf(' ');
        return space >= 0 ? header[(space + 1)..].Trim() : header.Trim();
    }
}

public static class LegacySessionMiddlewareExtensions
{
    public static IApplicationBuilder UseLegacyZuneSessions(this IApplicationBuilder app)
        => app.UseMiddleware<LegacySessionMiddleware>();
}
