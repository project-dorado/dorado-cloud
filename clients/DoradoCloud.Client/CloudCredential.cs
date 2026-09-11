namespace DoradoCloud.Client;

/// <summary>
/// A bearer credential held by a Dorado Cloud client: the current access token,
/// the refresh token (when the server granted <c>offline_access</c>), and the
/// access token's expiry. <see cref="ExpiresAtUtc"/> may be null when the server
/// did not advertise a lifetime.
/// </summary>
public sealed record CloudCredential(
    string AccessToken,
    string? RefreshToken = null,
    DateTimeOffset? ExpiresAtUtc = null)
{
    public bool HasAccessToken => !string.IsNullOrWhiteSpace(AccessToken);

    public bool HasRefreshToken => !string.IsNullOrWhiteSpace(RefreshToken);

    /// <summary>True when the access token is expired (or about to expire within <paramref name="skew"/>).</summary>
    public bool IsExpired(DateTimeOffset now, TimeSpan skew)
        => ExpiresAtUtc is { } expiry && now >= expiry - skew;
}

/// <summary>
/// Persistence seam for a client's Dorado Cloud credential. Implementations may
/// be in-memory (tests), settings-backed (desktop), or platform key-store backed
/// (Android). The auth handler reads the credential on every request and writes
/// it back after a refresh, so implementations must be thread-safe.
/// </summary>
public interface ICloudCredentialStore
{
    ValueTask<CloudCredential?> GetAsync(CancellationToken cancellationToken = default);

    ValueTask StoreAsync(CloudCredential credential, CancellationToken cancellationToken = default);

    ValueTask ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>Simple process-local store; useful for tests and short-lived hosts.</summary>
public sealed class InMemoryCloudCredentialStore : ICloudCredentialStore
{
    private readonly object _gate = new();
    private CloudCredential? _current;

    public InMemoryCloudCredentialStore(CloudCredential? initial = null) => _current = initial;

    public ValueTask<CloudCredential?> GetAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(_current);
        }
    }

    public ValueTask StoreAsync(CloudCredential credential, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _current = credential;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _current = null;
        }

        return ValueTask.CompletedTask;
    }
}
