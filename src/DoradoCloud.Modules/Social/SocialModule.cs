using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Social;

/// <summary>
/// Profiles, follows, activity feed, Zune Card and badges. M0 exposes the
/// authenticated contract; persistence, moderation and (optionally) federation
/// land in M4.
/// </summary>
public sealed class SocialModule : EndpointModuleBase
{
    public override string Name => "social";

    protected override void MapV1(RouteGroupBuilder group)
    {
        group.MapGet("/ping", () => Ping(Name));

        group.MapGet("/profiles/{handle}", (string handle) => Results.Ok(new
        {
            handle,
            displayName = handle,
            followers = 0,
            following = 0,
            note = "Social profiles are scheduled for milestone M4."
        })).WithName("social_profile");
    }
}
