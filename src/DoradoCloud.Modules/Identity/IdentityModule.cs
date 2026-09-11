using System.Security.Claims;
using DoradoCloud.Modules.Abstractions;
using DoradoCloud.Shared.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenIddict.Abstractions;

namespace DoradoCloud.Modules.Identity;

/// <summary>
/// Identity liveness, the current principal, and the account-scoped device
/// registry and settings sync.
/// </summary>
public sealed class IdentityModule : EndpointModuleBase
{
    public override string Name => "identity";

    protected override void MapV1(RouteGroupBuilder group)
    {
        group.MapGet("/ping", () => Ping(Name));

        group.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            accountId = user.GetAccountId(),
            subject = user.FindFirst(OpenIddictConstants.Claims.Subject)?.Value,
            name = user.Identity?.Name,
            email = user.GetEmail()
        })).RequireAuthorization();

        var me = group.MapGroup("/me").RequireAuthorization();

        me.MapGet("/devices", async (ClaimsPrincipal user, DeviceService devices, CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            var list = await devices.ListAsync(accountId, cancellationToken);
            return Results.Ok(list.Select(DeviceService.ToDto));
        }).WithName("identity_devices_list");

        me.MapPost("/devices", async (
            ClaimsPrincipal user,
            RegisterDeviceRequest request,
            DeviceService devices,
            CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            var device = await devices.RegisterAsync(accountId, request, cancellationToken);
            return Results.Created($"/v1/identity/me/devices/{device.Id}", DeviceService.ToDto(device));
        }).WithName("identity_devices_register");

        me.MapDelete("/devices/{deviceId:guid}", async (
            ClaimsPrincipal user,
            Guid deviceId,
            DeviceService devices,
            CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            return await devices.RemoveAsync(accountId, deviceId, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        }).WithName("identity_devices_remove");

        me.MapGet("/settings", async (ClaimsPrincipal user, SettingsService settings, CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            return Results.Ok(await settings.GetAsync(accountId, cancellationToken));
        }).WithName("identity_settings_get");

        me.MapPut("/settings", async (
            ClaimsPrincipal user,
            PutSettingsRequest request,
            SettingsService settings,
            CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            var (dto, conflict) = await settings.PutAsync(accountId, request, cancellationToken);
            return conflict
                ? Results.Conflict(new { error = "version_conflict" })
                : Results.Ok(dto);
        }).WithName("identity_settings_put");

        // GDPR: data portability export and right-to-erasure.
        me.MapGet("/export", async (ClaimsPrincipal user, AccountService accounts, CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            return Results.Ok(await accounts.ExportAsync(accountId, cancellationToken));
        }).WithName("identity_export");

        me.MapDelete("", async (ClaimsPrincipal user, AccountService accounts, CancellationToken cancellationToken) =>
        {
            if (user.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            var deleted = await accounts.DeleteAccountAsync(accountId, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).WithName("identity_delete_account");
    }
}
