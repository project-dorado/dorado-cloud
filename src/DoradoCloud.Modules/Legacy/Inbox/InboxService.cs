using DoradoCloud.Modules.Data;
using Microsoft.EntityFrameworkCore;

namespace DoradoCloud.Modules.Legacy.Inbox;

/// <summary>Persists legacy Zune inbox messages (inbox.zune.net compatibility).</summary>
public sealed class InboxService(DoradoDbContext db)
{
    public async Task<InboxMessage> SendAsync(
        Guid? senderAccountId,
        string senderTag,
        string recipientTag,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var message = new InboxMessage
        {
            SenderAccountId = senderAccountId,
            SenderTag = Normalize(senderTag),
            RecipientTag = Normalize(recipientTag),
            Subject = Truncate(subject, 256),
            Body = Truncate(body, 4096)
        };

        db.InboxMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        return message;
    }

    public Task<List<InboxMessage>> InboxAsync(string recipientTag, int limit, CancellationToken cancellationToken)
    {
        var recipient = Normalize(recipientTag);
        var take = Math.Clamp(limit, 1, 100);

        return db.InboxMessages
            .Where(message => message.RecipientTag == recipient)
            .OrderByDescending(message => message.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max];
}
