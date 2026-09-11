using System.Text;
using System.Xml.Linq;
using DoradoCloud.Legacy;
using DoradoCloud.Legacy.Atom;
using DoradoCloud.Modules.Abstractions;
using DoradoCloud.Modules.Social;
using DoradoCloud.Shared.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Legacy.Social;

/// <summary>
/// Recreates <c>socialapi.zune.net</c>: member profiles, friends and badges as
/// Atom feeds, backed by the shared social graph.
/// </summary>
public sealed class LegacySocialModule : LegacyModuleBase
{
    private const string Ns = LegacyConstants.SocialNamespace;

    public override string Name => "socialapi";

    public override IReadOnlyList<string> Hosts => new[] { "socialapi.zune.net", "socialapi-ssl.zune.net" };

    protected override void MapLegacy(RouteGroupBuilder group)
    {
        group.MapGet("/", () => Results.Text("Dorado Cloud social service.", LegacyConstants.TextPlain));

        group.MapGet("/members", Search);
        group.MapGet("/{locale}/members", Search);

        group.MapGet("/members/{zuneTag}", Member);
        group.MapGet("/{locale}/members/{zuneTag}", Member);

        group.MapGet("/members/{zuneTag}/friends", Friends);
        group.MapGet("/{locale}/members/{zuneTag}/friends", Friends);

        group.MapGet("/members/{zuneTag}/badges", Badges);
        group.MapGet("/{locale}/members/{zuneTag}/badges", Badges);
    }

    private static async Task<IResult> Search(string? q, SocialService social, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Results.BadRequest(new { error = "query_required" });
        }

        var feed = Feed("Members", "members", "/members").Author("Dorado Cloud");
        foreach (var profile in await social.SearchAsync(q, 25, ct))
        {
            AddMember(feed, await social.ZuneCardAsync(profile, ct));
        }

        return Xml(feed.Build());
    }

    private static async Task<IResult> Member(string zuneTag, SocialService social, CancellationToken ct)
    {
        var profile = await social.GetByHandleAsync(zuneTag, ct);
        if (profile is null)
        {
            return Results.NotFound();
        }

        var card = await social.ZuneCardAsync(profile, ct);
        var feed = Feed(card.Handle, card.Handle, $"/members/{card.Handle}");
        AddMember(feed, card);
        return Xml(feed.Build());
    }

    private static async Task<IResult> Friends(string zuneTag, SocialService social, CancellationToken ct)
    {
        var profile = await social.GetByHandleAsync(zuneTag, ct);
        if (profile is null)
        {
            return Results.NotFound();
        }

        var feed = Feed($"{profile.Handle} friends", profile.Handle, $"/members/{profile.Handle}/friends");
        foreach (var friend in await social.FollowingAsync(profile.AccountId, ct))
        {
            AddMember(feed, await social.ZuneCardAsync(friend, ct));
        }

        return Xml(feed.Build());
    }

    private static async Task<IResult> Badges(string zuneTag, SocialService social, CancellationToken ct)
    {
        var profile = await social.GetByHandleAsync(zuneTag, ct);
        if (profile is null)
        {
            return Results.NotFound();
        }

        var card = await social.ZuneCardAsync(profile, ct);
        var feed = Feed($"{card.Handle} badges", card.Handle, $"/members/{card.Handle}/badges");
        foreach (var badge in card.Badges)
        {
            feed.AddEntry(badge.Name, badge.Code, $"/members/{card.Handle}/badges", entry => entry
                .Element("code", badge.Code)
                .Element("name", badge.Name)
                .Element("description", badge.Description)
                .ElementIf("earnedAt", badge.EarnedAt?.UtcDateTime.ToString("O")));
        }

        return Xml(feed.Build());
    }

    private static void AddMember(AtomFeedBuilder feed, ZuneCardDto card)
    {
        feed.AddEntry(card.DisplayName, card.Handle, $"/members/{card.Handle}", entry => entry
            .Element("displayName", card.DisplayName)
            .Element("bio", card.Bio)
            .Element("followerCount", card.Followers.ToString())
            .Element("followingCount", card.Following.ToString())
            .Element("activityCount", card.Activities.ToString()));
    }

    private static AtomFeedBuilder Feed(string title, string id, string selfHref)
        => new(title, id, selfHref, Ns);

    private static IResult Xml(XElement element)
        => Results.Text(element.ToXml(), LegacyConstants.Xml, Encoding.UTF8);
}
