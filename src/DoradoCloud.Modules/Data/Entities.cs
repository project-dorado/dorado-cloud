namespace DoradoCloud.Modules.Data;

/// <summary>A Dorado Cloud account (email + password).</summary>
public sealed class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A client device enrolled by an account (desktop or Android).</summary>
public sealed class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string? Serial { get; set; }
    public string? AppVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Per-account settings document with optimistic-concurrency versioning.</summary>
public sealed class UserSettings
{
    public Guid AccountId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public int Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>A published, signed application release for the update feed.</summary>
public sealed class UpdateRelease
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string App { get; set; } = string.Empty;      // dorado | dorado-hd
    public string Channel { get; set; } = "stable";      // stable | beta
    public string Version { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string Algorithm { get; set; } = "RS256";
    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;
}

// ---- Social -------------------------------------------------------------

/// <summary>A public social profile attached to an account.</summary>
public sealed class Profile
{
    public Guid AccountId { get; set; }
    public string Handle { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>A directed follow edge.</summary>
public sealed class Follow
{
    public Guid FollowerAccountId { get; set; }
    public Guid FolloweeAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>An entry in an account's activity feed.</summary>
public sealed class Activity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>A badge earned by an account.</summary>
public sealed class Badge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTimeOffset EarnedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>A block edge (blocker excludes blocked from feeds/interactions).</summary>
public sealed class Block
{
    public Guid BlockerAccountId { get; set; }
    public Guid BlockedAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>A moderation report against an account and/or activity.</summary>
public sealed class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReporterAccountId { get; set; }
    public Guid SubjectAccountId { get; set; }
    public Guid? ActivityId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "open";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>A legacy Zune inbox message (inbox.zune.net compatibility store).</summary>
public sealed class InboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? SenderAccountId { get; set; }
    public string SenderTag { get; set; } = string.Empty;
    public string RecipientTag { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A legacy Zune WS-Trust session. Only the SHA-256 of the opaque ticket is
/// stored (the raw ticket is returned once to the client and re-sent as
/// <c>Authorization: WLID1.0 &lt;ticket&gt;</c>, matching the original client).
/// </summary>
public sealed class LegacySession
{
    /// <summary>Uppercase SHA-256 hex of the opaque ticket; primary key.</summary>
    public string TokenHash { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}

/// <summary>
/// A DRM-free media asset (public-domain / Creative Commons only). Bytes live in
/// object storage keyed by <see cref="ContentHash"/>; this row carries the
/// license metadata required by the M6 legal gate. Never enabled by default.
/// </summary>
public sealed class MediaAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Creator { get; set; } = string.Empty;
    public string License { get; set; } = string.Empty;
    public string LicenseUrl { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string ContentHash { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A per-MBID similarity embedding (M10). Stored as JSON text so it works on
/// SQLite and PostgreSQL alike; when <c>Embedding:UsePgvector</c> is enabled an
/// operator adds a pgvector column/index (see <c>deploy/pgvector/init.sql</c>)
/// for accelerated search.
/// </summary>
public sealed class TrackEmbedding
{
    public string Mbid { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string EmbeddingJson { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
