using System.Xml.Linq;
using DoradoCloud.Legacy;
using DoradoCloud.Legacy.Atom;
using DoradoCloud.Modules.Directory;
using DoradoCloud.Shared.Contracts;

namespace DoradoCloud.Modules.Legacy.Catalog;

/// <summary>Podcast Index-backed legacy podcast feeds.</summary>
public sealed class LegacyPodcastCatalog(PodcastIndexClient podcasts) : ILegacyPodcastCatalog
{
    private const string Ns = LegacyConstants.MusicCatalogNamespace;

    public Task<XElement> HubAsync(string locale, CancellationToken cancellationToken)
    {
        var feed = Feed("Podcasts", "podcast", $"/v3.2/{locale}/music/hub/podcast").Author("Dorado Cloud");
        feed.AddEntry("Top podcasts", "chart", "/podcastchart/zune/podcasts");
        feed.AddEntry("Categories", "categories", $"/v3.2/{locale}/podcastCategories");
        return Task.FromResult(feed.Build());
    }

    public async Task<XElement> ChartAsync(string locale, CancellationToken cancellationToken)
    {
        var feed = Feed("Top podcasts", "podcastChart", "/podcastchart/zune/podcasts");
        foreach (var item in await podcasts.TrendingAsync(25, cancellationToken))
        {
            AddPodcast(feed, item);
        }

        return feed.Build();
    }

    public async Task<XElement> CategoriesAsync(string locale, CancellationToken cancellationToken)
    {
        var feed = Feed("Categories", "podcastCategories", $"/v3.2/{locale}/podcastCategories");
        foreach (var category in await podcasts.CategoriesAsync(cancellationToken))
        {
            feed.AddEntry(
                category.Name,
                category.Id,
                $"/v3.2/{locale}/podcast?q={Uri.EscapeDataString(category.Name)}");
        }

        return feed.Build();
    }

    public async Task<XElement> SearchAsync(string locale, string query, CancellationToken cancellationToken)
    {
        var result = await podcasts.SearchAsync(query, 25, cancellationToken);
        var feed = Feed(query, "podcast", $"/v3.2/{locale}/podcast").Author(result.Attribution);

        foreach (var item in result.Items)
        {
            AddPodcast(feed, item);
        }

        return feed.Build();
    }

    private static void AddPodcast(AtomFeedBuilder feed, PodcastResult item)
    {
        feed.AddEntry(item.Title, item.FeedId, item.FeedUrl, entry => entry
            .Element("author", item.Author)
            .Element("description", item.Description)
            .ElementIf("imageId", string.IsNullOrWhiteSpace(item.ImageUrl) ? null : item.ImageUrl)
            .Element("feedUrl", item.FeedUrl)
            .ElementIf("categories", string.IsNullOrWhiteSpace(item.Categories) ? null : item.Categories)
            .ElementIf("language", string.IsNullOrWhiteSpace(item.Language) ? null : item.Language));
    }

    private static AtomFeedBuilder Feed(string title, string id, string selfHref)
        => new(title, id, selfHref, Ns);
}
