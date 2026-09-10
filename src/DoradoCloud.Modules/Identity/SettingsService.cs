using DoradoCloud.Modules.Data;
using DoradoCloud.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DoradoCloud.Modules.Identity;

/// <summary>
/// Per-account settings document with optimistic concurrency: a write supplies
/// the version it last saw and is rejected if another device has moved ahead.
/// </summary>
public sealed class SettingsService(DoradoDbContext db)
{
    public async Task<SettingsDto> GetAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var settings = await db.UserSettings.FindAsync([accountId], cancellationToken);
        return settings is null
            ? new SettingsDto("{}", 0, DateTimeOffset.MinValue)
            : new SettingsDto(settings.PayloadJson, settings.Version, settings.UpdatedAt);
    }

    public async Task<(SettingsDto? Dto, bool Conflict)> PutAsync(
        Guid accountId,
        PutSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = await db.UserSettings.FindAsync([accountId], cancellationToken);

        if (settings is null)
        {
            if (request.ExpectedVersion is > 0)
            {
                return (null, true);
            }

            settings = new UserSettings
            {
                AccountId = accountId,
                PayloadJson = request.PayloadJson,
                Version = 1,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.UserSettings.Add(settings);
        }
        else
        {
            if (request.ExpectedVersion is int expected && expected != settings.Version)
            {
                return (null, true);
            }

            settings.PayloadJson = request.PayloadJson;
            settings.Version += 1;
            settings.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return (new SettingsDto(settings.PayloadJson, settings.Version, settings.UpdatedAt), false);
    }
}
