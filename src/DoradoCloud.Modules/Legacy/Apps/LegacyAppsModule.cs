using System.Text;
using System.Xml.Linq;
using DoradoCloud.Legacy;
using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Legacy.Apps;

/// <summary>
/// Read-only Zune HD application catalog and package download, recreated for
/// hosts-patched clients. Packages are streamed from an external, untracked
/// corpus; there is no purchase, billing or DRM.
/// </summary>
public sealed class LegacyAppsModule : LegacyModuleBase
{
    public override string Name => "apps";

    public override IReadOnlyList<string> Hosts => new[] { "catalog.zune.net", "apps.zune.net" };

    protected override void MapLegacy(RouteGroupBuilder group)
    {
        group.MapGet("/", () => Results.Text("Dorado Cloud app catalog (read-only).", LegacyConstants.TextPlain));

        group.MapGet("/appCategories",
            async (ILegacyAppsCatalog apps, CancellationToken ct) => Xml(await apps.CategoriesAsync("en-US", ct)));

        group.MapGet("/apps",
            async (string? q, string? category, ILegacyAppsCatalog apps, CancellationToken ct)
                => Xml(await apps.AppsAsync("en-US", q, category, ct)));

        group.MapGet("/clientTypes/hubTypes/apps/hub",
            async (ILegacyAppsCatalog apps, CancellationToken ct) => Xml(await apps.AppsAsync("en-US", null, null, ct)));

        group.MapGet("/apps/{id}",
            async (string id, ILegacyAppsCatalog apps, CancellationToken ct) => Xml(await apps.AppAsync("en-US", id, ct)));

        group.MapGet("/apps/{id}/package", (string id, ILegacyAppsCatalog apps) =>
        {
            if (!apps.TryResolvePackage(id, out var path))
            {
                return Results.NotFound();
            }

            return Results.File(path, "application/octet-stream", Path.GetFileName(path), enableRangeProcessing: true);
        }).WithName("legacy_apps_package");
    }

    private static IResult Xml(XElement? element)
        => element is null
            ? Results.NotFound()
            : Results.Text(element.ToXml(), LegacyConstants.Xml, Encoding.UTF8);
}
