using System.Xml.Linq;

namespace DoradoCloud.Legacy.Atom;

/// <summary>
/// Builds an Atom <c>a:entry</c>. Local (service-specific) elements — such as
/// <c>primaryArtist</c>, <c>rights</c>, <c>image</c> or <c>isActionable</c> —
/// are emitted in the feed's local namespace.
/// </summary>
public sealed class AtomEntryBuilder
{
    private static readonly XNamespace Atom = LegacyConstants.AtomNamespace;

    private readonly XNamespace _local;
    private readonly XElement _entry;

    internal AtomEntryBuilder(XNamespace local, string title, string id, string? href = null)
    {
        _local = local;
        _entry = new XElement(Atom + "entry");
        if (href is not null)
        {
            _entry.Add(AtomFeedBuilder.Link(href, "alternate"));
        }

        _entry.Add(new XElement(Atom + "updated", DateTimeOffset.UtcNow.ToString("O")));
        _entry.Add(new XElement(Atom + "title", new XAttribute("type", "text"), title));
        _entry.Add(new XElement(Atom + "id", id));
    }

    /// <summary>Adds a simple local element with a text value.</summary>
    public AtomEntryBuilder Element(string name, string value)
    {
        _entry.Add(new XElement(_local + name, value));
        return this;
    }

    /// <summary>Adds a local element only when <paramref name="value"/> is non-null.</summary>
    public AtomEntryBuilder ElementIf(string name, string? value)
        => value is null ? this : Element(name, value);

    /// <summary>Adds a local element and lets the caller populate its children.</summary>
    public AtomEntryBuilder Container(string name, Action<XElement> configure)
    {
        var element = new XElement(_local + name);
        configure(element);
        _entry.Add(element);
        return this;
    }

    /// <summary>Adds a <c>primaryArtist</c> child with <c>id</c> and <c>name</c>.</summary>
    public AtomEntryBuilder PrimaryArtist(string id, string name)
        => Container("primaryArtist", element =>
        {
            element.Add(new XElement(_local + "id", id));
            element.Add(new XElement(_local + "name", name));
        });

    /// <summary>Adds an <c>image</c> child carrying the provider image id.</summary>
    public AtomEntryBuilder Image(string id)
        => Container("image", element => element.Add(new XElement(_local + "id", id)));

    public XElement Build() => _entry;
}
