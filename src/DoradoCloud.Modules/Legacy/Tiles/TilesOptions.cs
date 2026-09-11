namespace DoradoCloud.Modules.Legacy.Tiles;

/// <summary>
/// Configuration for the <c>tiles.zune.net</c> compatibility service. Tile
/// images (member backgrounds and avatars) live in an external, untracked
/// corpus; with no root configured the service fails closed.
/// </summary>
public sealed class TilesOptions
{
    /// <summary>Directory holding <c>Background/</c> and <c>Avatar/</c> tile images.</summary>
    public string CorpusRoot { get; set; } = string.Empty;
}
