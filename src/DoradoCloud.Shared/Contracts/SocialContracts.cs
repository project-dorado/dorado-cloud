namespace DoradoCloud.Shared.Contracts;

// ---- Profiles & graph ---------------------------------------------------

public sealed record ProfileDto(
    Guid AccountId,
    string Handle,
    string DisplayName,
    string Bio,
    int Followers,
    int Following,
    int Activities,
    bool IsFollowing,
    bool IsBlocked,
    DateTimeOffset CreatedAt);

public sealed record UpsertProfileRequest(string Handle, string DisplayName, string? Bio);

// ---- Activity feed ------------------------------------------------------

public sealed record ActivityDto(
    Guid Id,
    string Handle,
    string Kind,
    string PayloadJson,
    DateTimeOffset CreatedAt);

public sealed record PostActivityRequest(string Kind, string? PayloadJson);

// ---- Badges / Zune Card -------------------------------------------------

public sealed record BadgeDefinitionDto(string Code, string Name, string Description);

public sealed record BadgeDto(string Code, string Name, string Description, DateTimeOffset? EarnedAt);

public sealed record ZuneCardDto(
    string Handle,
    string DisplayName,
    string Bio,
    int Followers,
    int Following,
    int Activities,
    IReadOnlyList<BadgeDto> Badges,
    IReadOnlyList<ActivityDto> Recent);

// ---- Inbox --------------------------------------------------------------

/// <summary>
/// A message in the signed-in user's inbox. Backed by the same store the legacy
/// <c>inbox.zune.net</c> module writes to, exposed here as JSON so modern
/// clients do not need the legacy host.
/// </summary>
public sealed record InboxMessageDto(
    Guid Id,
    string SenderTag,
    string RecipientTag,
    string Subject,
    string Body,
    DateTimeOffset CreatedAt);

// ---- Moderation ---------------------------------------------------------

public sealed record ReportRequest(Guid SubjectAccountId, Guid? ActivityId, string Reason);

public sealed record ReportDto(
    Guid Id,
    Guid ReporterAccountId,
    Guid SubjectAccountId,
    Guid? ActivityId,
    string Reason,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record ResolveReportRequest(string Status);
