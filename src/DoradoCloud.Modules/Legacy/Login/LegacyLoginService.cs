using DoradoCloud.Modules.Identity;
using DoradoCloud.Modules.Legacy.Session;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Legacy.Login;

/// <summary>
/// Validates legacy WS-Trust credentials against the Dorado account store and
/// issues an opaque session ticket (persisted as a hash). Gated by
/// <see cref="LegacyLoginOptions.Enabled"/>.
/// </summary>
public sealed class LegacyLoginService(
    AccountService accounts,
    LegacySessionService sessions,
    IOptions<LegacyLoginOptions> options,
    ILogger<LegacyLoginService> logger)
{
    public bool Enabled => options.Value.Enabled;

    public async Task<LegacyLoginResult> AuthenticateAsync(
        string? username,
        string? password,
        CancellationToken cancellationToken)
    {
        if (!Enabled)
        {
            return LegacyLoginResult.Disabled;
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new LegacyLoginResult(true, false, null, null, null, "missing_credentials");
        }

        var account = await accounts.ValidateCredentialsAsync(username, password, cancellationToken);
        if (account is null)
        {
            logger.LogWarning("Legacy login rejected for {User}", username);
            return new LegacyLoginResult(true, false, null, null, null, "invalid_credentials");
        }

        var token = await sessions.IssueAsync(account.Id, cancellationToken);
        logger.LogInformation("Legacy login succeeded for account {AccountId}", account.Id);
        return new LegacyLoginResult(true, true, account.Id, username.Trim(), token, null);
    }
}

/// <summary>Outcome of a legacy WS-Trust authentication attempt.</summary>
public sealed record LegacyLoginResult(
    bool Enabled,
    bool Success,
    Guid? AccountId,
    string? MemberName,
    string? Token,
    string? Error)
{
    public static readonly LegacyLoginResult Disabled = new(false, false, null, null, null, "login_bridge_disabled");
}
