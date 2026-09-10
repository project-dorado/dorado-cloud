namespace DoradoCloud.Shared;

/// <summary>Stable identity for the Dorado Cloud services.</summary>
public static class DoradoCloudInfo
{
    public const string ProductName = "Dorado Cloud";
    public const string ApiVersion = "v1";
    public const string ServiceVersion = "0.1.0";

    /// <summary>OAuth scope required by the Dorado clients to call the API.</summary>
    public const string ApiScope = "dorado.api";

    /// <summary>OpenIddict resource/audience name for the API.</summary>
    public const string ApiResource = "dorado-api";
}
