using System.Text;
using System.Xml.Linq;
using DoradoCloud.Legacy;
using DoradoCloud.Modules.Abstractions;
using DoradoCloud.Modules.Legacy.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Legacy.Mix;

/// <summary>
/// Recreates <c>mix.zune.net</c>: the Mixview "similar tracks" endpoint. Backed
/// by the shared MusicBrainz catalog (same logic as the catalog host's v4.0
/// route).
/// </summary>
public sealed class LegacyMixModule : LegacyModuleBase
{
    public override string Name => "mix";

    public override IReadOnlyList<string> Hosts => new[] { "mix.zune.net", "mix-ssl.zune.net" };

    protected override void MapLegacy(RouteGroupBuilder group)
    {
        group.MapGet("/", () => Results.Text("Dorado Cloud Mixview service.", LegacyConstants.TextPlain));

        group.MapGet("/v4.0/{culture}/track/{trackId}/similarTracks",
            async (string culture, string trackId, ILegacyMusicCatalog catalog, CancellationToken ct) =>
            {
                var feed = await catalog.SimilarTracksAsync(culture, trackId, ct);
                return Results.Text(feed.ToXml(), LegacyConstants.Xml, Encoding.UTF8);
            }).WithName("legacy_mix_similar_tracks");
    }
}
