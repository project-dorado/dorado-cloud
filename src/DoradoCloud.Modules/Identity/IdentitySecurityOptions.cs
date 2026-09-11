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

    public EmailOptions Email { get; set; } = new();
}

/// <summary>Outbound email configuration. Default is the logging sender.</summary>
public sealed class EmailOptions
{
    /// <summary><c>log</c> (default) or <c>smtp</c>.</summary>
    public string Provider { get; set; } = "log";

    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = "no-reply@dorado.local";
    public string FromName { get; set; } = "Dorado Cloud";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}

