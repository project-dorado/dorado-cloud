using System.Xml.Linq;
using DoradoCloud.Legacy;
using DoradoCloud.Legacy.Atom;
using DoradoCloud.Modules.Directory;
using DoradoCloud.Shared.Contracts;

namespace DoradoCloud.Modules.Legacy.Catalog;

/// <summary>
/// Atom feed shapes for the legacy podcast hub, backed by the Podcast Index
/// adapter (M2). When the provider is unconfigured the hub still returns a
/// valid, reference-shaped feed (charts/categories are simply empty).
/// </summary>
public interface ILegacyPodcastCatalog
{
    Task<XElement> HubAsync(string locale, CancellationToken cancellationToken);
    Task<XElement> ChartAsync(string locale, CancellationToken cancellationToken);
    Task<XElement> CategoriesAsync(string locale, CancellationToken cancellationToken);
    Task<XElement> SearchAsync(string locale, string query, CancellationToken cancellationToken);
}
