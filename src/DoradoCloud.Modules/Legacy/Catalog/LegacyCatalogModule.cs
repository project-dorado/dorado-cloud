using System.Text;
using System.Xml.Linq;
using DoradoCloud.Legacy;
using DoradoCloud.Legacy.Atom;
using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Legacy.Catalog;

/// <summary>
/// Recreates <c>catalog.zune.net</c>: the Atom/XML music catalog (search,
/// albums, artists, genres, tracks, charts) the Zune 4.8 client browses.
/// </summary>
public sealed class LegacyCatalogModule : LegacyModuleBase
{
    public override string Name => "catalog";

    public override IReadOnlyList<string> Hosts => new[] { "catalog.zune.net", "catalog-ssl.zune.net" };

    protected override void MapLegacy(RouteGroupBuilder group)
    {
        group.MapGet("/", () => Results.Text("Dorado Cloud music catalog.", LegacyConstants.TextPlain));

        group.MapGet("/v3.2/{locale}/hubs/music",
            async (string locale, ILegacyMusicCatalog catalog, CancellationToken ct) => Xml(await catalog.HubsAsync(locale, ct)));

        group.MapGet("/v3.2/{locale}/music/genre",
            async (string locale, ILegacyMusicCatalog catalog, CancellationToken ct) => Xml(await catalog.GenresAsync(locale, ct)));

        group.MapGet("/v3.2/{locale}/music/genre/{genreId}",
            async (string locale, string genreId, ILegacyMusicCatalog catalog, CancellationToken ct) => Xml(await catalog.GenreAsync(locale, genreId, ct)));

        group.MapGet("/v3.2/{locale}/music/genre/{genreId}/albums",
            async (string locale, string genreId, ILegacyMusicCatalog catalog, CancellationToken ct) => Xml(await catalog.GenreAlbumsAsync(locale, genreId, ct)));

        group.MapGet("/v3.2/{locale}/music/album",
            async (string locale, string? q, ILegacyMusicCatalog catalog, CancellationToken ct) =>
                string.IsNullOrWhiteSpace(q)
                    ? Results.BadRequest(new { error = "query_required" })
                    : Xml(await catalog.AlbumSearchAsync(locale, q, ct)));

        group.MapGet("/v3.2/{locale}/music/album/{albumId}",
            async (string locale, string albumId, ILegacyMusicCatalog catalog, CancellationToken ct) => Xml(await catalog.AlbumDetailAsync(locale, albumId, ct)));

        // Video/movie hubs: no provider is configured; return valid empty feeds
        // so the client's hub tree still renders (see docs/legacy-client-setup.md).
        group.MapGet("/v3.2/{locale}/music/hub/movie",
            (string locale) => Xml(EmptyFeed("Movies", "movies", $"/v3.2/{locale}/music/hub/movie")));
        group.MapGet("/v3.2/{locale}/music/hub/video",
            (string locale) => Xml(EmptyFeed("Videos", "videos", $"/v3.2/{locale}/music/hub/video")));
        group.MapGet("/v3.2/{locale}/chart/zuneDownload/movie",
            (string locale) => Xml(EmptyFeed("Movies", "movies", $"/v3.2/{locale}/chart/zuneDownload/movie")));

        group.MapGet("/v3.2/{locale}/music/artist/{artistId}",
            async (string locale, string artistId, ILegacyMusicCatalog catalog, CancellationToken ct) => Xml(await catalog.ArtistAsync(locale, artistId, ct)));

        group.MapGet("/v3.2/{locale}/music/artist/{artistId}/albums",
            async (string locale, string artistId, ILegacyMusicCatalog catalog, CancellationToken ct) => Xml(await catalog.ArtistAlbumsAsync(locale, artistId, ct)));

        group.MapGet("/v3.2/{locale}/music/artist/{artistId}/tracks",
            async (string locale, string artistId, ILegacyMusicCatalog catalog, CancellationToken ct) => Xml(await catalog.ArtistTracksAsync(locale, artistId, ct)));

        group.MapGet("/v3.2/{locale}/music/track",
            async (string locale, string? q, ILegacyMusicCatalog catalog, CancellationToken ct) =>
                string.IsNullOrWhiteSpace(q)
                    ? Results.BadRequest(new { error = "query_required" })
                    : Xml(await catalog.TrackSearchAsync(locale, q, ct)));

        group.MapGet("/v3.2/{locale}/music/track/{trackId}",
            async (string locale, string trackId, ILegacyMusicCatalog catalog, CancellationToken ct) => Xml(await catalog.TrackDetailAsync(locale, trackId, ct)));

        group.MapGet("/v3.2/{locale}/music/chart/zune/tracks",
            async (string locale, ILegacyMusicCatalog catalog, CancellationToken ct) => Xml(await catalog.ChartTracksAsync(locale, ct)));

        // ---- podcast hub (Podcast Index; empty feed when unconfigured) ----
        group.MapGet("/v3.2/{locale}/music/hub/podcast",
            async (string locale, ILegacyPodcastCatalog podcasts, CancellationToken ct) => Xml(await podcasts.HubAsync(locale, ct)));

        group.MapGet("/podcastchart/zune/podcasts",
            async (ILegacyPodcastCatalog podcasts, CancellationToken ct) => Xml(await podcasts.ChartAsync(DefaultLocale, ct)));

        group.MapGet("/v3.2/{locale}/podcastCategories",
            async (string locale, ILegacyPodcastCatalog podcasts, CancellationToken ct) => Xml(await podcasts.CategoriesAsync(locale, ct)));

        group.MapGet("/podcastCategories",
            async (ILegacyPodcastCatalog podcasts, CancellationToken ct) => Xml(await podcasts.CategoriesAsync(DefaultLocale, ct)));

        group.MapGet("/v3.2/{locale}/podcast", Podcast);
        group.MapGet("/podcast", (string? q, string? url, ILegacyPodcastCatalog podcasts, RssProxyClient rss, CancellationToken ct)
            => Podcast(DefaultLocale, q, url, podcasts, rss, ct));

        group.MapGet("/v4.0/{culture}/track/{trackId}/similarTracks",
            async (string culture, string trackId, ILegacyMusicCatalog catalog, CancellationToken ct) => Xml(await catalog.SimilarTracksAsync(culture, trackId, ct)));
    }

    private static IResult Xml(XElement? element)
        => element is null
            ? Results.NotFound()
            : Results.Text(element.ToXml(), LegacyConstants.Xml, Encoding.UTF8);

    private const string DefaultLocale = "en-US";

    private static XElement EmptyFeed(string title, string id, string selfHref)
        => new AtomFeedBuilder(title, id, selfHref, LegacyConstants.MusicCatalogNamespace).Build();

    private static async Task<IResult> Podcast(
        string locale,
        string? q,
        string? url,
        ILegacyPodcastCatalog podcasts,
        RssProxyClient rss,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            var result = await rss.FetchAsync(url, cancellationToken);
            return result is null
                ? Results.NotFound()
                : Results.File(result.Content, result.ContentType);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            return Xml(await podcasts.SearchAsync(locale, q, cancellationToken));
        }

        return Results.BadRequest(new { error = "query_or_url_required" });
    }
}
