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
