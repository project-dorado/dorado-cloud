using System.Net.Http.Json;
using DoradoCloud.Shared.Contracts;

namespace DoradoCloud.Client;

/// <summary>
/// Typed client for the Dorado Cloud API, intended to be registered with
/// <see cref="ServiceCollectionExtensions.AddDoradoCloud"/> and injected into
/// the desktop/mobile provider implementations.
/// </summary>
public sealed class DoradoCloudClient(HttpClient http)
{
    /// <summary>Liveness probe for a single module (catalog, artwork, ...).</summary>
    public async Task<PingResponse?> PingModuleAsync(string module, CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<PingResponse>($"v1/{module}/ping", cancellationToken);

    /// <summary>Checks for a signed application update on the given channel.</summary>
    public async Task<UpdateCheckResponse?> CheckForUpdateAsync(string app, string channel = "stable", CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<UpdateCheckResponse>($"v1/updates/{app}/{channel}", cancellationToken);

    /// <summary>Returns the authenticated principal (requires a bearer token).</summary>
    public async Task<MeResponse?> GetMeAsync(CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<MeResponse>("v1/identity/me", cancellationToken);
}

public sealed record MeResponse(string? Subject, string? Name, string? Email);

public sealed record UpdateCheckResponse(string App, string Channel, bool Available, UpdateManifest? Manifest, string? Note);
