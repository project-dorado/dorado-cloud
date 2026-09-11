using System.Xml.Linq;

namespace DoradoCloud.Legacy;

/// <summary>Serialization helpers for the legacy XML wire format.</summary>
public static class LegacyXml
{
    /// <summary>Serializes an element with a UTF-8 XML declaration, no pretty-printing.</summary>
    public static string ToXml(this XElement element)
        => "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + element.ToString(SaveOptions.DisableFormatting);
}
