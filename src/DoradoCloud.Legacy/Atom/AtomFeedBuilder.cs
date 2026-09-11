using System.Xml.Linq;

namespace DoradoCloud.Legacy.Atom;

/// <summary>
/// Builds the Atom feeds the Zune 4.8 client expects. The original services
/// emit an <c>a:feed</c> (Atom namespace) whose default namespace is the
/// service-specific local namespace, e.g.
/// <c>http://schemas.zune.net/catalog/music/2007/10</c>.
/// </summary>
public sealed class AtomFeedBuilder
{
    private static readonly XNamespace Atom = LegacyConstants.AtomNamespace;

    private readonly XNamespace _local;
    private readonly XElement _feed;

    public AtomFeedBuilder(
        string title,
        string id,
        string? selfHref = null,
        string localNamespace = LegacyConstants.MusicCatalogNamespace,
        bool openSearch = false)
    {
        _local = localNamespace;
        _feed = new XElement(Atom + "feed",
            new XAttribute(XNamespace.Xmlns + "a", Atom),
            new XAttribute("xmlns", localNamespace));

        if (openSearch)
        {
            _feed.Add(new XAttribute(XNamespace.Xmlns + "os", LegacyConstants.OpenSearchNamespace));
        }

        if (selfHref is not null)
        {
            _feed.Add(Link(selfHref));
        }

        _feed.Add(new XElement(Atom + "updated", DateTimeOffset.UtcNow.ToString("O")));
        _feed.Add(new XElement(Atom + "title", new XAttribute("type", "text"), title));
        _feed.Add(new XElement(Atom + "id", id));
    }

    /// <summary>Replaces the feed's <c>a:title</c>.</summary>
    public AtomFeedBuilder Title(string title)
    {
        SetAtomChild("title", title, "type", "text");
        return this;
    }

    /// <summary>Replaces the feed's <c>a:updated</c> timestamp.</summary>
    public AtomFeedBuilder Updated(DateTimeOffset timestamp)
    {
        SetAtomChild("updated", timestamp.UtcDateTime.ToString("O"));
        return this;
    }

    /// <summary>Adds an <c>a:author/a:name</c> to the feed.</summary>
    public AtomFeedBuilder Author(string name)
    {
        _feed.Add(new XElement(Atom + "author", new XElement(Atom + "name", name)));
        return this;
    }

    /// <summary>Adds a simple local element with a text value to the feed.</summary>
    public AtomFeedBuilder Element(string name, string value)
    {
        _feed.Add(new XElement(_local + name, value));
        return this;
    }

    /// <summary>Adds a local element and lets the caller populate its children.</summary>
    public AtomFeedBuilder Container(string name, Action<XElement> configure)
    {
        var element = new XElement(_local + name);
        configure(element);
        _feed.Add(element);
        return this;
    }

    /// <summary>Creates an entry builder bound to this feed's local namespace.</summary>
    public AtomEntryBuilder NewEntry(string title, string id, string? href = null)
        => new(_local, title, id, href);

    public AtomFeedBuilder AddEntry(AtomEntryBuilder entry)
    {
        _feed.Add(entry.Build());
        return this;
    }

    public AtomFeedBuilder AddEntry(string title, string id, string? href = null, Action<AtomEntryBuilder>? configure = null)
    {
        var entry = NewEntry(title, id, href);
        configure?.Invoke(entry);
        return AddEntry(entry);
    }

    public XElement Build() => _feed;

    /// <summary>Serializes the feed with an XML declaration, no pretty-printing.</summary>
    public string ToXml() => LegacyXml.ToXml(_feed);

    /// <summary>An Atom <c>a:link</c> element.</summary>
    public static XElement Link(string href, string rel = "self", string type = LegacyConstants.AtomXml)
    {
        var link = new XElement(Atom + "link",
            new XAttribute("rel", rel),
            new XAttribute("type", type));
        link.SetAttributeValue("href", href);
        return link;
    }

    private void SetAtomChild(string localName, string value, string? attribute = null, string? attributeValue = null)
    {
        var existing = _feed.Element(Atom + localName);
        var replacement = new XElement(Atom + localName, value);
        if (attribute is not null)
        {
            replacement.SetAttributeValue(attribute, attributeValue);
        }

        if (existing is not null)
        {
            existing.ReplaceWith(replacement);
        }
        else
        {
            _feed.Add(replacement);
        }
    }
}
