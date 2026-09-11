namespace DoradoCloud.Modules.Identity;

/// <summary>
/// Security-hardening configuration for interactive accounts (M11): public base
/// URL for emailed links, token lifetimes, and whether login requires a verified
/// email (default off to avoid locking users out before an email provider exists).
/// </summary>
public sealed class IdentitySecurityOptions
{
    public string PublicBaseUrl { get; set; } = "http://localhost:5080";

    public int EmailVerificationTtlHours { get; set; } = 24;

    public int PasswordResetTtlHours { get; set; } = 2;

    public bool RequireEmailVerification { get; set; }
}
