using System.Text;
using System.Xml.Linq;
using DoradoCloud.Legacy;
using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Legacy.MetaServices;

/// <summary>
/// Recreates the metadata services endpoint discovery
/// (<c>fai.music.metaservices.microsoft.com/ZuneAPI/EndPoints.aspx</c>),
/// pointing the client at the Dorado Cloud catalog and artwork hosts.
/// </summary>
public sealed class LegacyMetaServicesModule : LegacyModuleBase
{
    public override string Name => "metaservices";

    public override IReadOnlyList<string> Hosts => new[]
    {
        "fai.music.metaservices.microsoft.com",
        "metaservices.zune.net"
    };

    protected override void MapLegacy(RouteGroupBuilder group)
    {
        group.MapGet("/", () => Results.Text("Dorado Cloud metadata services.", LegacyConstants.TextPlain));

        group.MapGet("/ZuneAPI/EndPoints.aspx", EndPoints).WithName("legacy_metaservices_endpoints");
        group.MapGet("/ZuneAPI/EndPoints", EndPoints);
    }

    private static readonly XNamespace EndpointNamespace =
        "http://schemas.zune.net/metaservices/endpoints/2009/01";

    private static IResult EndPoints()
    {
        var root = new XElement(EndpointNamespace + "ZuneAPI",
            new XElement(EndpointNamespace + "EndPoints",
                new XElement(EndpointNamespace + "EndPoint", new XAttribute("name", "catalog"), "http://catalog.zune.net"),
                new XElement(EndpointNamespace + "EndPoint", new XAttribute("name", "imageCatalog"), "http://image.catalog.zune.net"),
                new XElement(EndpointNamespace + "EndPoint", new XAttribute("name", "mix"), "http://mix.zune.net"),
                new XElement(EndpointNamespace + "EndPoint", new XAttribute("name", "social"), "http://socialapi.zune.net")));

        return Results.Text(root.ToXml(), LegacyConstants.Xml, Encoding.UTF8);
    }
}
