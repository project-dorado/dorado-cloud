using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules.Legacy.Tiles;

public static class TilesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <c>tiles.zune.net</c> compatibility service. Tile images
    /// are read from <c>Tiles:CorpusRoot</c> (external, untracked).
    /// </summary>
    public static IServiceCollection AddDoradoLegacyTiles(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TilesOptions>(configuration.GetSection("Tiles"));
        services.AddSingleton<TileStore>();
        return services;
    }
}
