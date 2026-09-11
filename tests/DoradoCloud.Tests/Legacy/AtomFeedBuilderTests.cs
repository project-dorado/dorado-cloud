using DoradoCloud.Legacy;
using DoradoCloud.Legacy.Atom;

namespace DoradoCloud.Tests.Legacy;

/// <summary>
/// Shape tests for the Atom/XML writer. These lock the wire format the legacy
/// Zune client parses: an <c>a:feed</c> with a service-local default namespace,
/// <c>a:entry</c> children, and local elements such as <c>primaryArtist</c>.
/// The expected values are authored by Dorado; no Microsoft feed is copied.
/// </summary>
public sealed class AtomFeedBuilderTests
{
    [Fact]
    public void Feed_has_atom_prefix_and_local_default_namespace()
    {
        var feed = new AtomFeedBuilder("Genres", "genre", "/v3.2/en-US/music/genre")
            .Author("Dorado Cloud");

        var xml = feed.ToXml();

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", xml);
        Assert.Contains($"xmlns:a=\"{LegacyConstants.AtomNamespace}\"", xml);
        Assert.Contains($"xmlns=\"{LegacyConstants.MusicCatalogNamespace}\"", xml);
        Assert.Contains("<a:feed ", xml);
        Assert.Contains("<a:title type=\"text\">Genres</a:title>", xml);
        Assert.Contains("<a:id>genre</a:id>", xml);
        Assert.Contains("<a:author><a:name>Dorado Cloud</a:name></a:author>", xml);
    }

    [Fact]
    public void Entry_carries_local_elements_and_links()
    {
        const string artistId = "urn:uuid:37690e00-0600-11db-89ca-0019b92a3933";

        var feed = new AtomFeedBuilder("Genres", "genre")
            .AddEntry("Rock", "urn:uuid:43c33b95-2ee2-4432-b272-0b29190fec9b",
                "/v3.2/en-US/music/genre/rock",
                entry => entry
                    .PrimaryArtist(artistId, "Love Infinity")
                    .Image("urn:uuid:43c33b95-2ee2-4432-b272-0b29190fec9b")
                    .Element("index", "1"));

        var xml = feed.ToXml();

        Assert.Contains("<a:entry>", xml);
        Assert.Contains("<a:link rel=\"alternate\" type=\"application/atom+xml\" href=\"/v3.2/en-US/music/genre/rock\" />", xml);
        Assert.Contains("<primaryArtist><id>" + artistId + "</id><name>Love Infinity</name></primaryArtist>", xml);
        Assert.Contains("<image><id>urn:uuid:43c33b95-2ee2-4432-b272-0b29190fec9b</id></image>", xml);
        Assert.Contains("<index>1</index>", xml);
    }

    [Fact]
    public void ElementIf_skips_null_values()
    {
        var feed = new AtomFeedBuilder("x", "x")
            .AddEntry("Title", "id", null, entry => entry.ElementIf("duration", null).ElementIf("trackNumber", "3"));

        var xml = feed.ToXml();

        Assert.DoesNotContain("duration", xml);
        Assert.Contains("<trackNumber>3</trackNumber>", xml);
    }
}
