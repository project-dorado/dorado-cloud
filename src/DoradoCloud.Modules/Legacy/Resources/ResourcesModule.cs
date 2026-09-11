using System.Text;
using DoradoCloud.Legacy;
using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Legacy.Resources;

/// <summary>
/// Recreates <c>resources.zune.net</c>: the firmware update resource host the
/// Zune desktop software and devices poll for baseline CABs. Baselines are
/// streamed from an external, untracked corpus; with no corpus configured the
/// service fails closed.
/// </summary>
public sealed class ResourcesModule : LegacyModuleBase
{
    private const string CabContentType = "application/vnd.ms-cab-compressed";

    public override string Name => "resources";

    public override IReadOnlyList<string> Hosts => new[] { "resources.zune.net", "resources-ssl.zune.net" };

    protected override void MapLegacy(RouteGroupBuilder group)
    {
        group.MapGet("/", () => Results.Text(
            "Dorado Cloud resources service. Firmware updates are served from a configured local corpus.",
            LegacyConstants.TextPlain));

        group.MapGet("/FirmwareUpdate.xml", Manifest).WithName("legacy_resources_firmware_update");
        group.MapGet("/firmware/FirmwareUpdate.xml", Manifest);
        group.MapGet("/v4_5/zuneprod.xml", Manifest);
        group.MapGet("/firmware/v4_5/zuneprod.xml", Manifest);

        group.MapGet("/{fileName}", (string fileName, FirmwareCatalog catalog) =>
        {
            if (!catalog.TryResolveFile(fileName, out var path))
            {
                return Results.NotFound();
            }

            return Results.File(path, CabContentType, fileName, enableRangeProcessing: true);
        }).WithName("legacy_resources_firmware_cab");
    }

    private static IResult Manifest(FirmwareCatalog catalog)
    {
        if (!catalog.CorpusAvailable)
        {
            return Results.NotFound();
        }

        var xml = catalog.BuildManifest().ToString();
        return Results.Text(xml, LegacyConstants.Xml, Encoding.UTF8);
    }
}
