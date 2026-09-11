using DoradoCloud.Legacy;
using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Legacy.Tuners;

/// <summary>
/// Recreates <c>tuners.zune.net</c>: PC-client resources served to the device.
/// Files come from an external, untracked corpus; with no corpus the service
/// fails closed.
/// </summary>
public sealed class LegacyTunersModule : LegacyModuleBase
{
    public override string Name => "tuners";

    public override IReadOnlyList<string> Hosts => new[] { "tuners.zune.net", "tuners-ssl.zune.net" };

    protected override void MapLegacy(RouteGroupBuilder group)
    {
        group.MapGet("/", () => Results.Text("Dorado Cloud tuners service.", LegacyConstants.TextPlain));

        group.MapGet("/{locale}/ZunePCClient/{version}/{fileName}",
            (string locale, string version, string fileName, ClientFileStore files) =>
            {
                if (!files.TryResolve(fileName, out var path))
                {
                    return Results.NotFound();
                }

                return Results.File(path, ClientFileStore.ContentTypeFor(path), fileName, enableRangeProcessing: true);
            }).WithName("legacy_tuners_client_file");
    }
}
