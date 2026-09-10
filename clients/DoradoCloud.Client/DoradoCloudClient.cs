using System.Net.Http.Json;
using System.Text.Json;
using DoradoCloud.Shared.Contracts;

namespace DoradoCloud.Client;

/// <summary>
/// Typed client for the Dorado Cloud API. The methods here map 1:1 onto the
/// server's <c>/v1/{module}/{verb}</c> surface (see <c>DoradoCloud.Api/Program.cs</c>
/// and the per-module <c>*Module.cs</c> files in <c>DoradoCloud.Modules</c>).
///
/// Every record round-trips through <see cref="JsonSerializerDefaults.Web"/>,
/// matching the server's contract serializer, so a server-side contract change
/// will surface as a deserialization failure rather than silent drift.
/// </summary>
public sealed class DoradoCloudClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ---- liveness --------------------------------------------------------

    /// <summary>Liveness probe for a single module (catalog, artwork, ...).</summary>
    public async Task<PingResponse?> PingModuleAsync(string module, CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<PingResponse>($"v1/{module}/ping", JsonOptions, cancellationToken);

    // ---- catalog ---------------------------------------------------------

    /// <summary>MusicBrainz-backed catalog search. <paramref name="type"/> is one of <c>artist</c>, <c>release-group</c>, <c>recording</c>.</summary>
    public async Task<CatalogSearchResponse?> CatalogSearchAsync(
        string query,
        string type = "release-group",
        int? limit = null,
        CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<CatalogSearchResponse>(
            $"v1/catalog/search?q={Uri.EscapeDataString(query)}&type={Uri.EscapeDataString(type)}&limit={(limit ?? 20).ToString()}",
            JsonOptions, cancellationToken);

    /// <summary>Artist detail by MusicBrainz id.</summary>
    public async Task<CatalogArtistDetail?> CatalogArtistAsync(string mbid, CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<CatalogArtistDetail>($"v1/catalog/artists/{Uri.EscapeDataString(mbid)}", JsonOptions, cancellationToken);

    // ---- artwork ---------------------------------------------------------

    /// <summary>Fetches the front cover for a MusicBrainz release-group id; returns the image bytes (PNG/JPEG).</summary>
    public async Task<byte[]?> ArtworkFrontAsync(string mbid, int size = 500, CancellationToken cancellationToken = default)
        => await http.GetByteArrayAsync($"v1/artwork/front/{Uri.EscapeDataString(mbid)}?size={size.ToString()}", cancellationToken);

    /// <summary>Proxies an allowlisted provider URL (Cover Art Archive, Fanart.tv, ...).</summary>
    public async Task<byte[]?> ArtworkProxyAsync(string url, CancellationToken cancellationToken = default)
        => await http.GetByteArrayAsync($"v1/artwork/proxy?url={Uri.EscapeDataString(url)}", cancellationToken);

    // ---- directory (podcasts, radio) -------------------------------------

    /// <summary>Podcast Index search. When the cloud is unconfigured this returns a well-formed empty response.</summary>
    public async Task<PodcastSearchResponse?> DirectoryPodcastSearchAsync(
        string query,
        int? limit = null,
        CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<PodcastSearchResponse>(
            $"v1/directory/podcasts/search?q={Uri.EscapeDataString(query)}&limit={(limit ?? 20).ToString()}",
            JsonOptions, cancellationToken);

    /// <summary>Radio-Browser station search.</summary>
    public async Task<RadioSearchResponse?> DirectoryRadioSearchAsync(
        string? query = null,
        string? country = null,
        string? tag = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var pairs = new List<string>();
        if (!string.IsNullOrWhiteSpace(query)) pairs.Add($"q={Uri.EscapeDataString(query)}");
        if (!string.IsNullOrWhiteSpace(country)) pairs.Add($"country={Uri.EscapeDataString(country)}");
        if (!string.IsNullOrWhiteSpace(tag)) pairs.Add($"tag={Uri.EscapeDataString(tag)}");
        pairs.Add($"limit={(limit ?? 30).ToString()}");
        var qs = string.Join("&", pairs);
        return await http.GetFromJsonAsync<RadioSearchResponse>($"v1/directory/radio/search?{qs}", JsonOptions, cancellationToken);
    }

    // ---- recs (QuickMix) -------------------------------------------------

    /// <summary>Similarity recommendations from a seed (artist name or MBID).</summary>
    public async Task<QuickMixResponse?> RecsQuickMixAsync(
        string seed,
        int? limit = null,
        CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<QuickMixResponse>(
            $"v1/recs/quickmix?seed={Uri.EscapeDataString(seed)}&limit={(limit ?? 20).ToString()}",
            JsonOptions, cancellationToken);

    // ---- updates ---------------------------------------------------------

    /// <summary>RS256 public key in PEM form, served by <c>/v1/updates/signing-key</c>.</summary>
    public async Task<string?> UpdatesSigningKeyAsync(CancellationToken cancellationToken = default)
        => await http.GetStringAsync("v1/updates/signing-key", cancellationToken);

    /// <summary>Checks for a signed application release on the given channel.</summary>
    public async Task<UpdateCheckResponse?> CheckForUpdateAsync(string app, string channel = "stable", CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<UpdateCheckResponse>(
            $"v1/updates/{Uri.EscapeDataString(app)}/{Uri.EscapeDataString(channel)}",
            JsonOptions, cancellationToken);

    /// <summary>Verifies a manifest's detached signature against the published public key.</summary>
    public async Task<bool> VerifyReleaseSignatureAsync(
        UpdateManifest manifest,
        string signatureBase64,
        CancellationToken cancellationToken = default)
    {
        var key = await UpdatesSigningKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }
        return UpdateManifestCrypto.Verify(manifest, signatureBase64, key);
    }

    // ---- identity --------------------------------------------------------

    /// <summary>Returns the authenticated principal. Requires a bearer token.</summary>
    public async Task<MeResponse?> GetMeAsync(CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<MeResponse>("v1/identity/me", JsonOptions, cancellationToken);

    /// <summary>Lists the devices enrolled for the authenticated account.</summary>
    public async Task<IReadOnlyList<DeviceDto>?> ListDevicesAsync(CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<IReadOnlyList<DeviceDto>>("v1/identity/me/devices", JsonOptions, cancellationToken);

    /// <summary>Registers a new device for the authenticated account.</summary>
    public async Task<DeviceDto?> RegisterDeviceAsync(RegisterDeviceRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("v1/identity/me/devices", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions, cancellationToken);
    }

    /// <summary>Removes an enrolled device.</summary>
    public async Task<bool> RemoveDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var response = await http.DeleteAsync($"v1/identity/me/devices/{deviceId:D}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Reads the per-account settings envelope (versioned, optimistic concurrency).</summary>
    public async Task<SettingsDto?> GetSettingsAsync(CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<SettingsDto>("v1/identity/me/settings", JsonOptions, cancellationToken);

    /// <summary>Writes the per-account settings envelope. Pass <see cref="PutSettingsRequest.ExpectedVersion"/> for optimistic concurrency.</summary>
    public async Task<SettingsDto?> PutSettingsAsync(PutSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PutAsJsonAsync("v1/identity/me/settings", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SettingsDto>(JsonOptions, cancellationToken);
    }

    // ---- social ----------------------------------------------------------

    /// <summary>List every badge definition in the catalog.</summary>
    public async Task<IReadOnlyList<BadgeDefinitionDto>?> ListBadgeDefinitionsAsync(CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<IReadOnlyList<BadgeDefinitionDto>>("v1/social/badges", JsonOptions, cancellationToken);

    /// <summary>Fetches a public profile by handle.</summary>
    public async Task<ProfileDto?> GetProfileAsync(string handle, CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<ProfileDto>($"v1/social/profiles/{Uri.EscapeDataString(handle)}", JsonOptions, cancellationToken);

    /// <summary>Followers of a profile.</summary>
    public async Task<IReadOnlyList<ProfileDto>?> GetFollowersAsync(string handle, CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<IReadOnlyList<ProfileDto>>($"v1/social/profiles/{Uri.EscapeDataString(handle)}/followers", JsonOptions, cancellationToken);

    /// <summary>Profiles the given handle is following.</summary>
    public async Task<IReadOnlyList<ProfileDto>?> GetFollowingAsync(string handle, CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<IReadOnlyList<ProfileDto>>($"v1/social/profiles/{Uri.EscapeDataString(handle)}/following", JsonOptions, cancellationToken);

    /// <summary>The Zune Card for a profile: counts, badges, recent activities.</summary>
    public async Task<ZuneCardDto?> GetZuneCardAsync(string handle, CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<ZuneCardDto>($"v1/social/profiles/{Uri.EscapeDataString(handle)}/zunecard", JsonOptions, cancellationToken);

    /// <summary>The authenticated principal's own profile (requires bearer).</summary>
    public async Task<ProfileDto?> GetMyProfileAsync(CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<ProfileDto>("v1/social/profiles/me", JsonOptions, cancellationToken);

    /// <summary>Creates or updates the authenticated principal's profile.</summary>
    public async Task<ProfileDto?> UpsertMyProfileAsync(UpsertProfileRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PutAsJsonAsync("v1/social/profiles/me", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProfileDto>(JsonOptions, cancellationToken);
    }

    /// <summary>Follows the target profile.</summary>
    public async Task<bool> FollowAsync(string handle, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync($"v1/social/profiles/{Uri.EscapeDataString(handle)}/follow", content: null, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Unfollows the target profile.</summary>
    public async Task<bool> UnfollowAsync(string handle, CancellationToken cancellationToken = default)
    {
        var response = await http.DeleteAsync($"v1/social/profiles/{Uri.EscapeDataString(handle)}/follow", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>The authenticated user's merged feed (self + followees, newest first, blocked excluded).</summary>
    public async Task<IReadOnlyList<ActivityDto>?> GetFeedAsync(int? limit = null, int? offset = null, CancellationToken cancellationToken = default)
    {
        var qs = $"?limit={(limit ?? 30).ToString()}&offset={(offset ?? 0).ToString()}";
        return await http.GetFromJsonAsync<IReadOnlyList<ActivityDto>>($"v1/social/me/feed{qs}", JsonOptions, cancellationToken);
    }

    /// <summary>Posts an activity to the authenticated user's feed.</summary>
    public async Task<ActivityDto?> PostActivityAsync(PostActivityRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("v1/social/me/activities", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ActivityDto>(JsonOptions, cancellationToken);
    }

    /// <summary>Manually grants a badge code (admin/seed flow).</summary>
    public async Task<bool> GrantBadgeAsync(string code, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync($"v1/social/me/badges/{Uri.EscapeDataString(code)}", content: null, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Files a moderation report.</summary>
    public async Task<ReportDto?> ReportAsync(ReportRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("v1/social/reports", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ReportDto>(JsonOptions, cancellationToken);
    }

    /// <summary>The blocked accounts for the authenticated user.</summary>
    public async Task<IReadOnlyList<ProfileDto>?> ListBlocksAsync(CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<IReadOnlyList<ProfileDto>>("v1/social/me/blocks", JsonOptions, cancellationToken);

    /// <summary>Blocks the target profile.</summary>
    public async Task<bool> BlockAsync(string handle, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync($"v1/social/profiles/{Uri.EscapeDataString(handle)}/block", content: null, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Unblocks the target profile.</summary>
    public async Task<bool> UnblockAsync(string handle, CancellationToken cancellationToken = default)
    {
        var response = await http.DeleteAsync($"v1/social/profiles/{Uri.EscapeDataString(handle)}/block", cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
