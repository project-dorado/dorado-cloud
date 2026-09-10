using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Storage;

/// <summary>S3 / MinIO-backed object storage.</summary>
public sealed class S3ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;

    public S3ObjectStorage(IOptions<StorageOptions> options)
    {
        var s3 = options.Value.S3;
        _bucket = s3.Bucket;

        var config = new AmazonS3Config
        {
            ForcePathStyle = s3.ForcePathStyle
        };

        if (!string.IsNullOrWhiteSpace(s3.ServiceUrl))
        {
            config.ServiceURL = s3.ServiceUrl;
        }

        _client = new AmazonS3Client(s3.AccessKey, s3.SecretKey, config);
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _client.GetObjectAsync(_bucket, key, cancellationToken);
            using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            ContentType = contentType,
            InputStream = new MemoryStream(content)
        }, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_bucket, key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
