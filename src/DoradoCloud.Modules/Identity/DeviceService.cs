using DoradoCloud.Modules.Data;
using DoradoCloud.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DoradoCloud.Modules.Identity;

/// <summary>Registry of the devices an account has enrolled.</summary>
public sealed class DeviceService(DoradoDbContext db)
{
    public async Task<IReadOnlyList<Device>> ListAsync(Guid accountId, CancellationToken cancellationToken = default)
        => await db.Devices
            .Where(d => d.AccountId == accountId)
            .OrderByDescending(d => d.LastSeenAt)
            .ToListAsync(cancellationToken);

    public async Task<Device> RegisterAsync(
        Guid accountId,
        RegisterDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        var device = new Device
        {
            AccountId = accountId,
            Name = request.Name.Trim(),
            Platform = request.Platform.Trim(),
            Serial = request.Serial,
            AppVersion = request.AppVersion
        };

        db.Devices.Add(device);
        await db.SaveChangesAsync(cancellationToken);
        return device;
    }

    public async Task<bool> RemoveAsync(Guid accountId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        var device = await db.Devices
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.AccountId == accountId, cancellationToken);

        if (device is null)
        {
            return false;
        }

        db.Devices.Remove(device);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public static DeviceDto ToDto(Device device)
        => new(device.Id, device.Name, device.Platform, device.Serial, device.AppVersion, device.CreatedAt, device.LastSeenAt);
}
