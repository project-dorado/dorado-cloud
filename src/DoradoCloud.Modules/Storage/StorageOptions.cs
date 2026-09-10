namespace DoradoCloud.Modules.Storage;

public sealed class StorageOptions
{
    /// <summary><c>local</c> (default) or <c>s3</c> (MinIO / AWS S3).</summary>
    public string Provider { get; set; } = "local";

    public string LocalRoot { get; set; } = "data/objects";

    public S3Options S3 { get; set; } = new();
}

public sealed class S3Options
{
    public string ServiceUrl { get; set; } = string.Empty;
    public string Bucket { get; set; } = "dorado-cloud";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool ForcePathStyle { get; set; } = true;
}
