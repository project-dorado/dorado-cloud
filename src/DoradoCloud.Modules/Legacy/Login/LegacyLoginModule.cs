using System.Text;
using System.Xml.Linq;
using DoradoCloud.Legacy;
using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Legacy.Login;

/// <summary>
/// Recreates <c>login.zune.net</c>'s WS-Trust endpoints plus the PPCRL
/// configuration. The bridge validates credentials against the Dorado account
/// store and issues an opaque ticket the client re-sends as
/// <c>Authorization: WLID1.0 &lt;ticket&gt;</c>. Disabled by default (501)
/// pending a security review.
/// </summary>
public sealed class LegacyLoginModule : LegacyModuleBase
{
    private static readonly XNamespace Soap = "http://www.w3.org/2003/05/soap-envelope";
    private static readonly XNamespace Wst = "http://schemas.xmlsoap.org/ws/2005/02/trust";
    private static readonly XNamespace Wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private static readonly XNamespace Psf = "http://schemas.microsoft.com/Passport/SoapServices/SOAPFault";
    private static readonly XNamespace Cfg = "http://schemas.microsoft.com/Passport/SoapServices/PPCRL";

    public override string Name => "login";

    public override IReadOnlyList<string> Hosts => new[] { "login.zune.net", "login-ssl.zune.net" };

    protected override void MapLegacy(RouteGroupBuilder group)
    {
        group.MapGet("/", () => Results.Text("Dorado Cloud legacy login bridge.", LegacyConstants.TextPlain));

        // The native client reads its WS-Trust host from the PPCRL config; serve
        // a Dorado-authored config so it points at this host.
        group.MapGet("/ppcrlconfig.bin", (IOptions<LegacyLoginOptions> options) =>
        {
            var baseUrl = options.Value.PublicBaseUrl.TrimEnd('/');
            var config = new XElement(Cfg + "ppcrl-config",
                new XAttribute(XNamespace.Xmlns + "cfg", Cfg),
                new XElement(Cfg + "WLIDSTS_WCF", $"{baseUrl}/RST2.srf"),
                new XElement(Cfg + "MSNWLIDSTS_WCF", $"{baseUrl}/RST2.srf"),
                new XElement(Cfg + "PassportSecurityTokenService", $"{baseUrl}/RST.srf"),
                new XElement(Cfg + "TrustedDomain", "zune.net"),
                new XElement(Cfg + "TrustedDomain", "live.com"));

            return Results.Text(config.ToXml(), LegacyConstants.Xml, Encoding.UTF8);
        }).WithName("legacy_login_ppcrlconfig");

        group.MapPost("/RST2.srf", Handle).WithName("legacy_login_rst2");
        group.MapPost("/NotRST.srf", Handle).WithName("legacy_login_norst");
    }

    private static async Task<IResult> Handle(
        HttpContext context,
        LegacyLoginService login,
        CancellationToken cancellationToken)
    {
        if (!login.Enabled)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status501NotImplemented,
                title: "Legacy login bridge disabled",
                detail: "The WS-Trust login bridge is security-gated. Set Legacy:Login:Enabled=true after review to enable it.");
        }

        using var reader = new StreamReader(context.Request.Body);
        var raw = await reader.ReadToEndAsync(cancellationToken);
        var (username, password) = ParseCredentials(raw);

        var result = await login.AuthenticateAsync(username, password, cancellationToken);
        if (!result.Success)
        {
            return Results.Text(
                Fault(result.Error ?? "login_failed"), LegacyConstants.Xml, Encoding.UTF8,
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Results.Text(SecurityTokenResponse(result), LegacyConstants.Xml, Encoding.UTF8);
    }

    private static (string? Username, string? Password) ParseCredentials(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !raw.TrimStart().StartsWith('<'))
        {
            return (null, null);
        }

        try
        {
            var document = XDocument.Parse(raw);
            var username = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Username")?.Value;
            var password = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Password")?.Value;
            return (username?.Trim(), password);
        }
        catch (Exception)
        {
            return (null, null);
        }
    }

    private static string SecurityTokenResponse(LegacyLoginResult result)
    {
        var envelope = new XElement(Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "s", Soap),
            new XAttribute(XNamespace.Xmlns + "wst", Wst),
            new XAttribute(XNamespace.Xmlns + "wsse", Wsse),
            new XAttribute(XNamespace.Xmlns + "psf", Psf),
            new XElement(Soap + "Body",
                new XElement(Wst + "RequestSecurityTokenResponse",
                    new XElement(Wst + "RequestedSecurityToken",
                        new XElement(Wsse + "BinarySecurityToken",
                            new XAttribute("Id", "Compact1"),
                            new XAttribute("ValueType", "urn:passport:compact"),
                            new XAttribute("EncodingType", "wsse:Base64Binary"),
                            result.Token ?? string.Empty)),
                    new XElement(Psf + "credProperty",
                        new XAttribute("Name", "AuthMembername"),
                        result.MemberName ?? string.Empty))));

        return envelope.ToXml();
    }

    private static string Fault(string code)
    {
        var envelope = new XElement(Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "s", Soap),
            new XElement(Soap + "Body",
                new XElement(Soap + "Fault",
                    new XElement(Soap + "Code", new XElement(Soap + "Value", "Sender")),
                    new XElement(Soap + "Reason", new XElement(Soap + "Text", code)))));

        return envelope.ToXml();
    }
}
