using DoradoCloud.Modules.Catalog;
using DoradoCloud.Modules.Providers;
using DoradoCloud.Shared.Contracts;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Recommendations;

/// <summary>
/// QuickMix similarity. This pragmatic engine scores candidates from MusicBrainz
/// relationships (band members / collaborators) and shared genre tags. It is
/// deterministic, cache-friendly and offline-testable; a pgvector embedding
/// pipeline can later replace the scoring without changing the endpoint.
/// </summary>
public sealed class QuickMixService(
    MusicBrainzClient brainz,
    IDistributedCache cache,
    IOptions<CatalogOptions> options,
    EmbeddingService? embeddings = null)
{
    public async Task<QuickMixResponse> RecommendAsync(string seed, int limit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(seed))
        {
            return new QuickMixResponse(seed ?? string.Empty, null, 0, CatalogOptions.Attribution, []);
        }

        limit = Math.Clamp(limit, 1, 50);
        var key = $"recs:quickmix:{seed.Trim().ToLowerInvariant()}:{limit}";

        return await cache.GetOrCreateAsync(key, TimeSpan.FromSeconds(options.Value.CacheSeconds), async token =>
        {
            string? seedMbid = Guid.TryParse(seed, out _) ? seed : null;

            if (seedMbid is null)
            {
                var search = await brainz.SearchAsync(seed, "artist", 1, token);
                var top = search.Items.FirstOrDefault();
                if (top is null)
                {
                    return new QuickMixResponse(seed, null, 0, CatalogOptions.Attribution, []);
                }

                seedMbid = top.Mbid;
            }

            var genres = await brainz.GetArtistGenresAsync(seedMbid!, token);
            var related = await brainz.GetRelatedArtistsAsync(seedMbid!, token);

            var scores = new Dictionary<string, (CatalogSearchItem Item, double Score, HashSet<string> Reasons)>();

            void Add(CatalogSearchItem item, double score, string reason)
            {
                if (string.IsNullOrWhiteSpace(item.Mbid) ||
                    string.Equals(item.Mbid, seedMbid, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (!scores.TryGetValue(item.Mbid, out var entry))
                {
                    entry = (item, 0, []);
                }

                entry.Score += score;
                entry.Reasons.Add(reason);
                scores[item.Mbid] = entry;
            }

            foreach (var artist in related)
            {
                Add(artist, 2.0, "related artist");
            }

            foreach (var genre in genres.Take(2))
            {
                foreach (var artist in await brainz.SearchByTagAsync(genre, 15, token))
                {
                    Add(artist, 1.0, $"shares genre {genre}");
                }
            }

            // Blend in embedding neighbours when the seed has a vector (M10).
            if (embeddings is not null)
            {
                var seedVector = await embeddings.GetVectorAsync(seedMbid!, token);
                if (seedVector is not null)
                {
                    foreach (var neighbour in await embeddings.NearestAsync(seedVector, limit * 2, token))
                    {
                        Add(
                            new CatalogSearchItem("artist", neighbour.Mbid, neighbour.Title, neighbour.Title, string.Empty, null),
                            1.5 * Math.Max(neighbour.Score, 0),
                            "embedding similarity");
                    }
                }
            }

            var items = scores.Values
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.Item.Title, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .Select(entry => new QuickMixCandidate(
                    entry.Item.Mbid,
                    entry.Item.Title,
                    Math.Round(entry.Score, 2),
                    entry.Reasons.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList()))
                .ToList();

            return new QuickMixResponse(seed, seedMbid, items.Count, CatalogOptions.Attribution, items);
        }, cancellationToken);
    }
}
