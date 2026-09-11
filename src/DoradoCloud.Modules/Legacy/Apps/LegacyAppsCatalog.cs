using System.Xml.Linq;
using DoradoCloud.Legacy;
using DoradoCloud.Legacy.Atom;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Legacy.Apps;

/// <summary>
/// Read-only catalog over an external corpus of <c>.zcp</c> packages. No
/// purchase, billing or DRM: discovery plus direct package download only.
/// </summary>
public sealed class LegacyAppsCatalog(IOptions<AppsOptions> options, ILogger<LegacyAppsCatalog> logger) : ILegacyAppsCatalog
{
    private const string Ns = LegacyConstants.MusicCatalogNamespace;

    public Task<XElement> CategoriesAsync(string locale, CancellationToken cancellationToken)
    {
        var categories = Enumerate()
            .Select(app => app.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category, StringComparer.OrdinalIgnoreCase);

        var feed = Feed("Categories", "appCategories", "/appCategories");
        foreach (var category in categories)
        {
            feed.AddEntry(category, Slug(category), $"/apps?category={Uri.EscapeDataString(category)}");
        }

        return Task.FromResult(feed.Build());
    }

    public Task<XElement> AppsAsync(string locale, string? query, string? category, CancellationToken cancellationToken)
    {
        IEnumerable<LegacyApp> apps = Enumerate();

        if (!string.IsNullOrWhiteSpace(category))
        {
            apps = apps.Where(app => app.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            apps = apps.Where(app =>
                app.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                app.Id.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var feed = Feed("Apps", "apps", "/apps");
        foreach (var app in apps)
        {
            AddApp(feed, app);
        }

        return Task.FromResult(feed.Build());
    }

    public Task<XElement?> AppAsync(string locale, string id, CancellationToken cancellationToken)
    {
        var app = Enumerate().FirstOrDefault(candidate => candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (app is null)
        {
            return Task.FromResult<XElement?>(null);
        }

        var feed = Feed(app.Title, app.Id, $"/apps/{app.Id}");
        AddApp(feed, app);
        return Task.FromResult<XElement?>(feed.Build());
    }

    public bool TryResolvePackage(string id, out string path)
    {
        path = string.Empty;
        var app = Enumerate().FirstOrDefault(candidate => candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (app is null)
        {
            return false;
        }

        path = app.Path;
        return true;
    }

    private static void AddApp(AtomFeedBuilder feed, LegacyApp app)
    {
        feed.AddEntry(app.Title, app.Id, $"/apps/{app.Id}", entry => entry
            .Element("category", app.Category)
            .Element("store", "Zest")
            .Element("cost", "Free")
            .Element("packageUrl", $"/apps/{app.Id}/package"));
    }

    private IReadOnlyList<LegacyApp> Enumerate()
    {
        var root = ResolveRoot();
        if (root is null)
        {
            return [];
        }

        var extension = options.Value.PackageExtension;
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".zcp";
        }
        else if (!extension.StartsWith('.'))
        {
            extension = "." + extension;
        }

        try
        {
            return System.IO.Directory.EnumerateFiles(root, "*" + extension, SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(1, options.Value.MaxPackages))
                .Select(path => ToApp(root, path))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enumerate the app corpus");
            return [];
        }
    }

    private static LegacyApp ToApp(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        var withoutExtension = relative[..^Path.GetExtension(relative).Length];
        var segments = withoutExtension.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var id = string.Join('.', segments.Select(Slug)).Trim('.');
        var title = PrettyTitle(segments[^1]);
        var category = segments.Length > 1 ? PrettyTitle(segments[0]) : "Applications";

        return new LegacyApp(id, title, category, path);
    }

    private string? ResolveRoot()
    {
        if (string.IsNullOrWhiteSpace(options.Value.CorpusRoot))
        {
            return null;
        }

        string full;
        try
        {
            full = Path.GetFullPath(options.Value.CorpusRoot);
        }
        catch
        {
            return null;
        }

        return System.IO.Directory.Exists(full) ? full : null;
    }

    private static AtomFeedBuilder Feed(string title, string id, string? selfHref = null)
        => new(title, id, selfHref, Ns);

    private static string Slug(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        return new string(chars).Trim('-');
    }

    private static string PrettyTitle(string value) => value.Replace('-', ' ').Replace('_', ' ').Trim();

    private sealed record LegacyApp(string Id, string Title, string Category, string Path);
}
