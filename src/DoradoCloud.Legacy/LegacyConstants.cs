namespace DoradoCloud.Legacy;

/// <summary>
/// Constants for the legacy Zune <c>*.zune.net</c> wire protocol (Atom/XML).
/// The original services spoke Atom feeds with a service-specific local
/// namespace, a small set of media types, and the <c>Zune/4.8</c> user agent.
/// </summary>
public static class LegacyConstants
{
    /// <summary>User agent the Zune 4.8 desktop software expects (and sends).</summary>
    public const string ZuneUserAgent = "Zune/4.8";

    public const string AtomNamespace = "http://www.w3.org/2005/Atom";
    public const string OpenSearchNamespace = "http://a9.com/-/spec/opensearch/1.1/";
    public const string MusicCatalogNamespace = "http://schemas.zune.net/catalog/music/2007/10";
    public const string SocialNamespace = "http://schemas.zune.net/social/2009/01";
    public const string CommerceNamespace = "http://schemas.zune.net/commerce/2009/01";

    public const string AtomXml = "application/atom+xml";
    public const string Xml = "application/xml";
    public const string TextPlain = "text/plain";
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Rss = "application/rss+xml";
}
