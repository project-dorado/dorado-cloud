namespace DoradoCloud.Modules.Providers;

/// <summary>
/// Serializes outbound calls per host to respect provider rate limits
/// (e.g. MusicBrainz's ~1 req/s policy).
/// </summary>
public sealed class HostRateLimiter
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _gates = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> _lastCall = new();

    public async Task WaitAsync(string host, TimeSpan minimumInterval, CancellationToken cancellationToken)
    {
        if (minimumInterval <= TimeSpan.Zero)
        {
            return;
        }

        var gate = _gates.GetOrAdd(host, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            if (_lastCall.TryGetValue(host, out var last))
            {
                var remaining = minimumInterval - (DateTimeOffset.UtcNow - last);
                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining, cancellationToken);
                }
            }

            _lastCall[host] = DateTimeOffset.UtcNow;
        }
        finally
        {
            gate.Release();
        }
    }
}
