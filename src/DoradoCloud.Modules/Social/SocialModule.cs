using System.Security.Claims;
using DoradoCloud.Modules.Abstractions;
using DoradoCloud.Modules.Data;
using DoradoCloud.Modules.Identity;
using DoradoCloud.Shared.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Social;

/// <summary>
/// Profiles, follow graph, activity feed, Zune Card, badges and moderation.
/// Read endpoints are public; writes and the feed require a user token; the
/// moderation queue requires the <c>Admin</c> policy.
/// </summary>
public sealed class SocialModule : EndpointModuleBase
{
    public override string Name => "social";

    protected override void MapV1(RouteGroupBuilder group)
    {
        group.MapGet("/ping", () => Ping(Name));

        group.MapGet("/badges", () => Results.Ok(
            BadgeCatalog.All.Select(d => new BadgeDefinitionDto(d.Code, d.Name, d.Description))));

        // ---- profiles ----------------------------------------------------

        group.MapGet("/profiles/{handle}", async (
            string handle, ClaimsPrincipal user, SocialService social, CancellationToken cancellationToken) =>
        {
            var profile = await social.GetByHandleAsync(handle, cancellationToken);
            return profile is null
                ? Results.NotFound(new { error = "profile_not_found" })
                : Results.Ok(await social.ToDtoAsync(profile, user.GetAccountId(), cancellationToken));
        }).WithName("social_profile");

        group.MapGet("/profiles/{handle}/followers", async (
            string handle, ClaimsPrincipal user, SocialService social, CancellationToken cancellationToken) =>
        {
            var profile = await social.GetByHandleAsync(handle, cancellationToken);
            if (profile is null)
            {
                return Results.NotFound(new { error = "profile_not_found" });
            }

            var viewer = user.GetAccountId();
            var followers = await social.FollowersAsync(profile.AccountId, cancellationToken);
            return Results.Ok(await ToDtosAsync(social, followers, viewer, cancellationToken));
        }).WithName("social_followers");

        group.MapGet("/profiles/{handle}/following", async (
            string handle, ClaimsPrincipal user, SocialService social, CancellationToken cancellationToken) =>
        {
            var profile = await social.GetByHandleAsync(handle, cancellationToken);
            if (profile is null)
            {
                return Results.NotFound(new { error = "profile_not_found" });
            }

            var viewer = user.GetAccountId();
            var following = await social.FollowingAsync(profile.AccountId, cancellationToken);
            return Results.Ok(await ToDtosAsync(social, following, viewer, cancellationToken));
        }).WithName("social_following");

        group.MapGet("/profiles/{handle}/zunecard", async (
            string handle, SocialService social, CancellationToken cancellationToken) =>
        {
            var profile = await social.GetByHandleAsync(handle, cancellationToken);
            return profile is null
                ? Results.NotFound(new { error = "profile_not_found" })
                : Results.Ok(await social.ZuneCardAsync(profile, cancellationToken));
        }).WithName("social_zunecard");

        group.MapPut("/profiles/me", async (
            UpsertProfileRequest request, ClaimsPrincipal user, SocialService social, CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            var (profile, error) = await social.UpsertProfileAsync(accountId, request, cancellationToken);
            return profile is null
                ? Results.BadRequest(new { error })
                : Results.Ok(await social.ToDtoAsync(profile, accountId, cancellationToken));
        }).RequireAuthorization().WithName("social_profile_upsert");

        group.MapGet("/profiles/me", async (ClaimsPrincipal user, SocialService social, CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            var profile = await social.GetByAccountAsync(accountId, cancellationToken);
            return profile is null
                ? Results.NotFound(new { error = "profile_not_found" })
                : Results.Ok(await social.ToDtoAsync(profile, accountId, cancellationToken));
        }).RequireAuthorization().WithName("social_profile_me");

        // ---- graph -------------------------------------------------------

        group.MapPost("/profiles/{handle}/follow", async (
            string handle, ClaimsPrincipal user, SocialService social, CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            var target = await social.GetByHandleAsync(handle, cancellationToken);
            if (target is null)
            {
                return Results.NotFound(new { error = "profile_not_found" });
            }

            return await social.FollowAsync(accountId, target.AccountId, cancellationToken)
                ? Results.NoContent()
                : Results.BadRequest(new { error = "cannot_follow" });
        }).RequireAuthorization().WithName("social_follow");

        group.MapDelete("/profiles/{handle}/follow", async (
            string handle, ClaimsPrincipal user, SocialService social, CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            var target = await social.GetByHandleAsync(handle, cancellationToken);
            if (target is null)
            {
                return Results.NotFound(new { error = "profile_not_found" });
            }

            await social.UnfollowAsync(accountId, target.AccountId, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization().WithName("social_unfollow");

        // ---- activity ----------------------------------------------------

        group.MapGet("/me/feed", async (
            int? limit, int? offset, ClaimsPrincipal user, SocialService social, CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            return Results.Ok(await social.FeedAsync(accountId, limit ?? 30, offset ?? 0, cancellationToken));
        }).RequireAuthorization().WithName("social_feed");

        group.MapGet("/me/inbox", async (
            int? limit, ClaimsPrincipal user, SocialService social, CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            return Results.Ok(await social.InboxAsync(accountId, limit ?? 50, cancellationToken));
        }).RequireAuthorization().WithName("social_inbox");

        group.MapPost("/me/inbox/{id:guid}/read", async (
            Guid id, ClaimsPrincipal user, SocialService social, CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            return await social.MarkInboxReadAsync(accountId, id, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound(new { error = "message_not_found" });
        }).RequireAuthorization().WithName("social_inbox_mark_read");

        group.MapPost("/me/activities", async (
            PostActivityRequest request, ClaimsPrincipal user, SocialService social, CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            var activity = await social.PostActivityAsync(accountId, request.Kind, request.PayloadJson, cancellationToken);
            return Results.Created(
                $"/v1/social/activities/{activity.Id}",
                new ActivityDto(activity.Id, string.Empty, activity.Kind, activity.PayloadJson, activity.CreatedAt));
        }).RequireAuthorization().WithName("social_post_activity");

        group.MapPost("/me/badges/{code}", async (
            string code, ClaimsPrincipal user, SocialService social, CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            return await social.GrantBadgeAsync(accountId, code, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound(new { error = "unknown_badge" });
        }).RequireAuthorization().WithName("social_grant_badge");

        // ---- moderation --------------------------------------------------

        group.MapPost("/profiles/{handle}/block", async (
            string handle, ClaimsPrincipal user, SocialService social, CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            var target = await social.GetByHandleAsync(handle, cancellationToken);
            if (target is null)
            {
                return Results.NotFound(new { error = "profile_not_found" });
            }

            await social.BlockAsync(accountId, target.AccountId, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization().WithName("social_block");

        group.MapDelete("/profiles/{handle}/block", async (
            string handle, ClaimsPrincipal user, SocialService social, CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            var target = await social.GetByHandleAsync(handle, cancellationToken);
            if (target is null)
            {
                return Results.NotFound(new { error = "profile_not_found" });
            }

            await social.UnblockAsync(accountId, target.AccountId, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization().WithName("social_unblock");

        group.MapGet("/me/blocks", async (ClaimsPrincipal user, SocialService social, CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            var blocked = await social.BlocksAsync(accountId, cancellationToken);
            return Results.Ok(await ToDtosAsync(social, blocked, accountId, cancellationToken));
        }).RequireAuthorization().WithName("social_blocks");

        group.MapPost("/reports", async (
            ReportRequest request, ClaimsPrincipal user, SocialService social, CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            var report = await social.ReportAsync(
                accountId, request.SubjectAccountId, request.ActivityId, request.Reason, cancellationToken);
            return Results.Created($"/v1/social/admin/reports/{report.Id}", ToDto(report));
        }).RequireAuthorization().WithName("social_report");

        var admin = group.MapGroup("/admin").RequireAuthorization("Admin");

        admin.MapGet("/reports", async (string? status, SocialService social, CancellationToken cancellationToken) =>
            Results.Ok((await social.ReportsAsync(status, cancellationToken)).Select(ToDto)))
            .WithName("social_admin_reports");

        admin.MapPost("/reports/{id:guid}/resolve", async (
            Guid id, ResolveReportRequest request, SocialService social, CancellationToken cancellationToken) =>
        {
            var report = await social.ResolveReportAsync(id, request.Status, cancellationToken);
            return report is null ? Results.NotFound() : Results.Ok(ToDto(report));
        }).WithName("social_admin_resolve");
    }

    private static async Task<IReadOnlyList<ProfileDto>> ToDtosAsync(
        SocialService social,
        IEnumerable<Profile> profiles,
        Guid? viewerId,
        CancellationToken cancellationToken)
    {
        var result = new List<ProfileDto>();
        foreach (var profile in profiles)
        {
            result.Add(await social.ToDtoAsync(profile, viewerId, cancellationToken));
        }

        return result;
    }

    private static ReportDto ToDto(Report report)
        => new(report.Id, report.ReporterAccountId, report.SubjectAccountId,
            report.ActivityId, report.Reason, report.Status, report.CreatedAt);
}
