namespace DoradoCloud.Legacy;

/// <summary>
/// Maps between the stable provider identifiers Dorado uses (MusicBrainz MBIDs,
/// which are GUIDs) and the <c>urn:uuid:</c> identifiers the Zune client sends
/// and expects.
/// </summary>
public interface ILegacyIdMapper
{
    /// <summary>Converts a provider id to the legacy <c>urn:uuid:</c> form.</summary>
    string ToLegacy(string providerId);

    /// <summary>Extracts the provider id from a legacy identifier.</summary>
    bool TryFromLegacy(string? legacyId, out string providerId);
}

/// <summary>
/// The default deterministic mapper: provider GUIDs and legacy <c>urn:uuid:</c>
/// identifiers differ only by the URN prefix. Non-GUID provider ids are passed
/// through unchanged, so callers can still resolve them.
/// </summary>
public sealed class LegacyIdMapper : ILegacyIdMapper
{
    public const string UrnUuidPrefix = "urn:uuid:";

    public string ToLegacy(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);

        if (providerId.StartsWith(UrnUuidPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return providerId;
        }

        return Guid.TryParse(providerId, out var guid)
            ? UrnUuidPrefix + guid.ToString("D")
            : providerId;
    }

    public bool TryFromLegacy(string? legacyId, out string providerId)
    {
        providerId = string.Empty;
        if (string.IsNullOrWhiteSpace(legacyId))
        {
            return false;
        }

        var value = legacyId;
        if (value.StartsWith(UrnUuidPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = value[UrnUuidPrefix.Length..];
        }

        if (value.Length == 0)
        {
            return false;
        }

        providerId = value;
        return true;
    }
}
