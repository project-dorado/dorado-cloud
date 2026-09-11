using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules.Legacy.Inbox;

public static class InboxServiceCollectionExtensions
{
    /// <summary>Registers the legacy <c>inbox.zune.net</c> message store.</summary>
    public static IServiceCollection AddDoradoLegacyInbox(this IServiceCollection services)
    {
        services.AddScoped<InboxService>();
        return services;
    }
}
