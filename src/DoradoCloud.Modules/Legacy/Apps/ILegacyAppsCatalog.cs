using System.Xml.Linq;

namespace DoradoCloud.Modules.Legacy.Apps;

/// <summary>
/// Read-only Zune HD application catalog over an external corpus of
/// <c>.zcp</c> packages. Discovery only; packages are served as-is.
/// </summary>
public interface ILegacyAppsCatalog
{
    Task<XElement> CategoriesAsync(string locale, CancellationToken cancellationToken);
    Task<XElement> AppsAsync(string locale, string? query, string? category, CancellationToken cancellationToken);
    Task<XElement?> AppAsync(string locale, string id, CancellationToken cancellationToken);

    /// <summary>Resolves an app package by catalog id.</summary>
    bool TryResolvePackage(string id, out string path);
}
