using System.Text.RegularExpressions;
using DoradoCloud.Modules.Data;
using DoradoCloud.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DoradoCloud.Modules.Social;

/// <summary>Profiles, follow graph, activity feed, Zune Card, badges and moderation.</summary>
public sealed partial class SocialService(DoradoDbContext db)
{
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{1,30}[a-z0-9])$")]
    private static partial Regex HandlePattern();

    public static bool IsValidHandle(string handle) => HandlePattern().IsMatch(handle ?? string.Empty);

    // ---- profiles --------------------------------------------------------

    public Task<Profile?> GetByHandleAsync(string handle, CancellationToken cancellationToken)
    {
        var normalized = (handle ?? string.Empty).Trim().ToLowerInvariant();
        return db.Profiles.FirstOrDefaultAsync(p => p.Handle == normalized, cancellationToken);
    }

    public Task<Profile?> GetByAccountAsync(Guid accountId, CancellationToken cancellationToken)
        => db.Profiles.FirstOrDefaultAsync(p => p.AccountId == accountId, cancellationToken);

    /// <summary>Case-insensitive handle/display-name search (legacy member search).</summary>
    public Task<List<Profile>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var normalized = (query ?? string.Empty).Trim().ToLowerInvariant();
        var take = Math.Clamp(limit, 1, 50);

        return db.Profiles
            .Where(p => p.Handle.ToLower().Contains(normalized) || p.DisplayName.ToLower().Contains(normalized))
            .OrderBy(p => p.Handle)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<(Profile? Profile, string? Error)> UpsertProfileAsync(
        Guid accountId,
        UpsertProfileRequest request,
        CancellationToken cancellationToken)
    {
        var handle = (request.Handle ?? string.Empty).Trim().ToLowerInvariant();

        if (!IsValidHandle(handle))
        {
            return (null, "Handle must be 3-32 lowercase letters, digits or hyphens.");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return (null, "Display name is required.");
        }

        var taken = await db.Profiles.AnyAsync(
            p => p.Handle == handle && p.AccountId != accountId, cancellationToken);
        if (taken)
        {
            return (null, "That handle is already taken.");
        }

        var profile = await db.Profiles.FindAsync([accountId], cancellationToken);
        if (profile is null)
        {
            profile = new Profile
            {
                AccountId = accountId,
                Handle = handle,
                DisplayName = request.DisplayName.Trim(),
                Bio = request.Bio ?? string.Empty
            };
            db.Profiles.Add(profile);
        }
        else
        {
            profile.Handle = handle;
            profile.DisplayName = request.DisplayName.Trim();
            profile.Bio = request.Bio ?? string.Empty;
            profile.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return (profile, null);
    }

    // ---- graph -----------------------------------------------------------

    public async Task<bool> FollowAsync(Guid follower, Guid followee, CancellationToken cancellationToken)
    {
        if (follower == followee)
        {
            return false;
        }

        var exists = await db.Follows.AnyAsync(
            f => f.FollowerAccountId == follower && f.FolloweeAccountId == followee, cancellationToken);
        if (!exists)
        {
            db.Follows.Add(new Follow { FollowerAccountId = follower, FolloweeAccountId = followee });
            await db.SaveChangesAsync(cancellationToken);
            await GrantBadgeAsync(follower, "connector", cancellationToken);
        }

        return true;
    }

    public async Task UnfollowAsync(Guid follower, Guid followee, CancellationToken cancellationToken)
    {
        var edge = await db.Follows.FirstOrDefaultAsync(
            f => f.FollowerAccountId == follower && f.FolloweeAccountId == followee, cancellationToken);
        if (edge is not null)
        {
            db.Follows.Remove(edge);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<List<Profile>> FollowersAsync(Guid accountId, CancellationToken cancellationToken)
        => (from f in db.Follows
            join p in db.Profiles on f.FollowerAccountId equals p.AccountId
            where f.FolloweeAccountId == accountId
            orderby f.CreatedAt descending
            select p).ToListAsync(cancellationToken);

    public Task<List<Profile>> FollowingAsync(Guid accountId, CancellationToken cancellationToken)
        => (from f in db.Follows
            join p in db.Profiles on f.FolloweeAccountId equals p.AccountId
            where f.FollowerAccountId == accountId
            orderby f.CreatedAt descending
            select p).ToListAsync(cancellationToken);

    // ---- activity --------------------------------------------------------

    public async Task<Activity> PostActivityAsync(
        Guid accountId,
        string kind,
        string? payloadJson,
        CancellationToken cancellationToken)
    {
        var activity = new Activity
        {
            AccountId = accountId,
            Kind = string.IsNullOrWhiteSpace(kind) ? "activity" : kind.Trim().ToLowerInvariant(),
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson
        };

        db.Activities.Add(activity);
        await db.SaveChangesAsync(cancellationToken);
        await GrantBadgeAsync(accountId, "first-post", cancellationToken);
        return activity;
    }

    public async Task<List<ActivityDto>> FeedAsync(
        Guid accountId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var followees = db.Follows
            .Where(f => f.FollowerAccountId == accountId)
            .Select(f => f.FolloweeAccountId);

        var blockedByMe = db.Blocks
            .Where(b => b.BlockerAccountId == accountId)
            .Select(b => b.BlockedAccountId);
        var blockingMe = db.Blocks
            .Where(b => b.BlockedAccountId == accountId)
            .Select(b => b.BlockerAccountId);

        var activities = await db.Activities
            .Where(a => a.AccountId == accountId || followees.Contains(a.AccountId))
            .Where(a => !blockedByMe.Contains(a.AccountId) && !blockingMe.Contains(a.AccountId))
            .OrderByDescending(a => a.CreatedAt)
            .Skip(Math.Max(offset, 0))
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);

        var handles = await HandlesForAsync(activities.Select(a => a.AccountId), cancellationToken);

        return activities
            .Select(a => new ActivityDto(a.Id, handles.GetValueOrDefault(a.AccountId, string.Empty), a.Kind, a.PayloadJson, a.CreatedAt))
            .ToList();
    }

    public async Task<List<ActivityDto>> RecentForAccountAsync(Guid accountId, int limit, CancellationToken cancellationToken)
    {
        var activities = await db.Activities
            .Where(a => a.AccountId == accountId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync(cancellationToken);

        var handles = await HandlesForAsync(activities.Select(a => a.AccountId), cancellationToken);
        return activities
            .Select(a => new ActivityDto(a.Id, handles.GetValueOrDefault(a.AccountId, string.Empty), a.Kind, a.PayloadJson, a.CreatedAt))
            .ToList();
    }

    // ---- badges ----------------------------------------------------------

    public async Task<bool> GrantBadgeAsync(Guid accountId, string code, CancellationToken cancellationToken)
    {
        var definition = BadgeCatalog.Find(code);
        if (definition is null)
        {
            return false;
        }

        var exists = await db.Badges.AnyAsync(
            b => b.AccountId == accountId && b.Code == definition.Code, cancellationToken);
        if (!exists)
        {
            db.Badges.Add(new Badge { AccountId = accountId, Code = definition.Code });
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public Task<List<Badge>> BadgesAsync(Guid accountId, CancellationToken cancellationToken)
        => db.Badges.Where(b => b.AccountId == accountId)
            .OrderBy(b => b.EarnedAt)
            .ToListAsync(cancellationToken);

    // ---- moderation ------------------------------------------------------

    public async Task BlockAsync(Guid blocker, Guid blocked, CancellationToken cancellationToken)
    {
        if (blocker == blocked)
        {
            return;
        }

        var exists = await db.Blocks.AnyAsync(
            b => b.BlockerAccountId == blocker && b.BlockedAccountId == blocked, cancellationToken);
        if (!exists)
        {
            db.Blocks.Add(new Block { BlockerAccountId = blocker, BlockedAccountId = blocked });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UnblockAsync(Guid blocker, Guid blocked, CancellationToken cancellationToken)
    {
        var edge = await db.Blocks.FirstOrDefaultAsync(
            b => b.BlockerAccountId == blocker && b.BlockedAccountId == blocked, cancellationToken);
        if (edge is not null)
        {
            db.Blocks.Remove(edge);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<List<Profile>> BlocksAsync(Guid accountId, CancellationToken cancellationToken)
        => (from b in db.Blocks
            join p in db.Profiles on b.BlockedAccountId equals p.AccountId
            where b.BlockerAccountId == accountId
            orderby b.CreatedAt descending
            select p).ToListAsync(cancellationToken);

    public async Task<Report> ReportAsync(
        Guid reporter,
        Guid subject,
        Guid? activityId,
        string reason,
        CancellationToken cancellationToken)
    {
        var report = new Report
        {
            ReporterAccountId = reporter,
            SubjectAccountId = subject,
            ActivityId = activityId,
            Reason = reason.Trim()
        };
        db.Reports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
        return report;
    }

    public async Task<List<Report>> ReportsAsync(string? status, CancellationToken cancellationToken)
        => await db.Reports
            .Where(r => status == null || r.Status == status)
            .OrderByDescending(r => r.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

    public async Task<Report?> ResolveReportAsync(Guid id, string status, CancellationToken cancellationToken)
    {
        var report = await db.Reports.FindAsync([id], cancellationToken);
        if (report is null)
        {
            return null;
        }

        report.Status = string.IsNullOrWhiteSpace(status) ? "resolved" : status.Trim().ToLowerInvariant();
        await db.SaveChangesAsync(cancellationToken);
        return report;
    }

    // ---- projections -----------------------------------------------------

    public async Task<ProfileDto> ToDtoAsync(Profile profile, Guid? viewerId, CancellationToken cancellationToken)
    {
        var followers = await db.Follows.CountAsync(f => f.FolloweeAccountId == profile.AccountId, cancellationToken);
        var following = await db.Follows.CountAsync(f => f.FollowerAccountId == profile.AccountId, cancellationToken);
        var activities = await db.Activities.CountAsync(a => a.AccountId == profile.AccountId, cancellationToken);

        var isFollowing = viewerId is Guid viewer && await db.Follows.AnyAsync(
            f => f.FollowerAccountId == viewer && f.FolloweeAccountId == profile.AccountId, cancellationToken);

        var isBlocked = false;
        if (viewerId is Guid v)
        {
            isBlocked = await db.Blocks.AnyAsync(
                b => (b.BlockerAccountId == v && b.BlockedAccountId == profile.AccountId) ||
                     (b.BlockerAccountId == profile.AccountId && b.BlockedAccountId == v),
                cancellationToken);
        }

        return new ProfileDto(
            profile.AccountId, profile.Handle, profile.DisplayName, profile.Bio,
            followers, following, activities, isFollowing, isBlocked, profile.CreatedAt);
    }

    public async Task<ZuneCardDto> ZuneCardAsync(Profile profile, CancellationToken cancellationToken)
    {
        var dto = await ToDtoAsync(profile, null, cancellationToken);
        var earned = await BadgesAsync(profile.AccountId, cancellationToken);
        var recent = await RecentForAccountAsync(profile.AccountId, 10, cancellationToken);

        var badges = BadgeCatalog.All
            .Select(definition => new BadgeDto(
                definition.Code,
                definition.Name,
                definition.Description,
                earned.FirstOrDefault(b => b.Code == definition.Code)?.EarnedAt))
            .ToList();

        return new ZuneCardDto(
            profile.Handle, profile.DisplayName, profile.Bio,
            dto.Followers, dto.Following, dto.Activities, badges, recent);
    }

    // ---- inbox -----------------------------------------------------------

    /// <summary>
    /// The signed-in account's inbox, keyed by their profile handle. Reads the
    /// same <c>InboxMessage</c> store the legacy module writes, so modern
    /// clients get the messages without the legacy host. Empty when the account
    /// has no profile yet.
    /// </summary>
    public async Task<List<InboxMessageDto>> InboxAsync(
        Guid accountId,
        int limit,
        CancellationToken cancellationToken)
    {
        var profile = await GetByAccountAsync(accountId, cancellationToken);
        if (profile is null)
        {
            return [];
        }

        var recipient = profile.Handle.Trim().ToLowerInvariant();
        var take = Math.Clamp(limit, 1, 100);

        return await db.InboxMessages
            .Where(message => message.RecipientTag == recipient)
            .OrderByDescending(message => message.CreatedAt)
            .Take(take)
            .Select(message => new InboxMessageDto(
                message.Id,
                message.SenderTag,
                message.RecipientTag,
                message.Subject,
                message.Body,
                message.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, string>> HandlesForAsync(
        IEnumerable<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        var ids = accountIds.Distinct().ToList();
        return await db.Profiles
            .Where(p => ids.Contains(p.AccountId))
            .ToDictionaryAsync(p => p.AccountId, p => p.Handle, cancellationToken);
    }
}
