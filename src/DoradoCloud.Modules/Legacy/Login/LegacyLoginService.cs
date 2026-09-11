using System.Security.Cryptography;
using DoradoCloud.Modules.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Legacy.Login;

/// <summary>
/// Validates legacy WS-Trust credentials against the Dorado account store and
/// mints an opaque session token. Gated by <see cref="LegacyLoginOptions.Enabled"/>.
/// </summary>
public sealed class LegacyLoginService(
    AccountService accounts,
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
            return new LegacyLoginResult(true, false, null, null, "missing_credentials");
        }

        var account = await accounts.ValidateCredentialsAsync(username, password, cancellationToken);
        if (account is null)
        {
            logger.LogWarning("Legacy login rejected for {User}", username);
            return new LegacyLoginResult(true, false, null, null, "invalid_credentials");
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        logger.LogInformation("Legacy login succeeded for account {AccountId}", account.Id);
        return new LegacyLoginResult(true, true, account.Id, token, null);
    }
}

/// <summary>Outcome of a legacy WS-Trust authentication attempt.</summary>
public sealed record LegacyLoginResult(bool Enabled, bool Success, Guid? AccountId, string? Token, string? Error)
{
    public static readonly LegacyLoginResult Disabled = new(false, false, null, null, "login_bridge_disabled");
}
