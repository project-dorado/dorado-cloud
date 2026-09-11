using System.Text;
using System.Xml.Linq;
using DoradoCloud.Legacy;
using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Legacy.Login;

/// <summary>
/// Recreates <c>login.zune.net</c>'s WS-Trust endpoints. The bridge validates
/// credentials against the Dorado account store, but is disabled by default
/// pending a security review; when disabled it answers <c>501</c>.
/// </summary>
public sealed class LegacyLoginModule : LegacyModuleBase
{
    private static readonly XNamespace Soap = "http://www.w3.org/2003/05/soap-envelope";
    private static readonly XNamespace Wst = "http://schemas.xmlsoap.org/ws/2005/02/trust";
    private static readonly XNamespace Wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";

    public override string Name => "login";

    public override IReadOnlyList<string> Hosts => new[] { "login.zune.net", "login-ssl.zune.net" };

    protected override void MapLegacy(RouteGroupBuilder group)
    {
        group.MapGet("/", () => Results.Text("Dorado Cloud legacy login bridge.", LegacyConstants.TextPlain));

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
            new XElement(Soap + "Body",
                new XElement(Wst + "RequestSecurityTokenResponse",
                    new XElement(Wst + "TokenType", "http://schemas.xmlsoap.org/ws/2005/02/trust/SecurityContextToken"),
                    new XElement(Wst + "RequestedSecurityToken",
                        new XElement(Wsse + "BinarySecurityToken",
                            new XAttribute("Id", result.AccountId?.ToString() ?? string.Empty),
                            result.Token ?? string.Empty)))));

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
