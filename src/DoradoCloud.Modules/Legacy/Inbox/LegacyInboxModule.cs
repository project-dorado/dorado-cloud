using System.Security.Claims;
using System.Text;
using System.Xml.Linq;
using DoradoCloud.Legacy;
using DoradoCloud.Legacy.Atom;
using DoradoCloud.Modules.Abstractions;
using DoradoCloud.Modules.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Legacy.Inbox;

/// <summary>
/// Recreates <c>inbox.zune.net</c>: legacy messaging persisted to a Dorado
/// store. The legacy client has no Dorado bearer token, so the sender is taken
/// from an optional <c>from</c> query value (or the authenticated principal
/// when present).
/// </summary>
public sealed class LegacyInboxModule : LegacyModuleBase
{
    private const string Ns = LegacyConstants.SocialNamespace;

    public override string Name => "inbox";

    public override IReadOnlyList<string> Hosts => new[] { "inbox.zune.net", "inbox-ssl.zune.net" };

    protected override void MapLegacy(RouteGroupBuilder group)
    {
        group.MapGet("/", () => Results.Text("Dorado Cloud inbox service.", LegacyConstants.TextPlain));

        group.MapPost("/messaging/{zuneTag}/{action}", Send);
        group.MapPost("/{locale}/messaging/{zuneTag}/{action}", Send);

        group.MapGet("/messaging/{zuneTag}/inbox", List);
        group.MapGet("/{locale}/messaging/{zuneTag}/inbox", List);
    }

    private static async Task<IResult> Send(
        string zuneTag,
        string action,
        HttpContext context,
        ClaimsPrincipal user,
        InboxService inbox,
        CancellationToken ct)
    {
        using var reader = new StreamReader(context.Request.Body);
        var raw = await reader.ReadToEndAsync(ct);
        var (subject, body) = Parse(raw);

        var senderAccountId = user.GetAccountId();
        var senderTag = context.Request.Query["from"].FirstOrDefault()
            ?? senderAccountId?.ToString()
            ?? "anonymous";

        var message = await inbox.SendAsync(senderAccountId, senderTag, zuneTag, subject, body, ct);
        return Results.Text($"<Message id=\"{message.Id}\" action=\"{action}\" />", LegacyConstants.Xml, Encoding.UTF8);
    }

    private static async Task<IResult> List(string zuneTag, int? limit, InboxService inbox, CancellationToken ct)
    {
        var messages = await inbox.InboxAsync(zuneTag, limit ?? 50, ct);
        var feed = new AtomFeedBuilder($"{zuneTag} inbox", zuneTag, $"/messaging/{zuneTag}/inbox", Ns);

        foreach (var message in messages)
        {
            feed.AddEntry(Truncate(message.Subject, 80), message.Id.ToString(), null, entry => entry
                .Element("sender", message.SenderTag)
                .Element("subject", message.Subject)
                .Element("body", message.Body)
                .Element("receivedAt", message.CreatedAt.UtcDateTime.ToString("O")));
        }

        return Results.Text(feed.Build().ToXml(), LegacyConstants.Xml, Encoding.UTF8);
    }

    private static (string Subject, string Body) Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (string.Empty, string.Empty);
        }

        if (!raw.TrimStart().StartsWith('<'))
        {
            return (string.Empty, raw.Trim());
        }

        try
        {
            var document = XDocument.Parse(raw);
            var subject = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Subject")?.Value ?? string.Empty;
            var body = document.Descendants()
                .FirstOrDefault(e => e.Name.LocalName is "Body" or "Message" or "Content")?.Value ?? string.Empty;
            return (subject.Trim(), body.Trim());
        }
        catch (Exception)
        {
            return (string.Empty, raw.Trim());
        }
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) ? "(no subject)" : value.Length <= max ? value : value[..max];
}
