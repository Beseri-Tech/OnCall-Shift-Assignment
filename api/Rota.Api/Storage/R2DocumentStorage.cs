using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Rota.Api.Storage;

/// <summary>Cloudflare R2 through its S3-compatible API. The bucket stays private; browsers only get expiring presigned links.</summary>
public sealed class R2DocumentStorage : IDocumentStorage, IDisposable
{
    private readonly R2Options _options;
    private readonly AmazonS3Client? _s3;

    public R2DocumentStorage(IOptions<R2Options> options)
    {
        _options = options.Value;
        if (!_options.IsConfigured) return;

        _s3 = new AmazonS3Client(new BasicAWSCredentials(_options.AccessKeyId, _options.SecretAccessKey), new AmazonS3Config
        {
            ServiceURL = _options.Endpoint,
            AuthenticationRegion = "auto",
            ForcePathStyle = true,
            // R2 does not support the SDK's newer default checksum headers.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        });
    }

    public bool IsEnabled => _s3 is not null;

    private AmazonS3Client S3 => _s3 ?? throw new InvalidOperationException("Document storage (R2) is not configured.");

    public string CreateUploadUrl(string objectKey, string contentType, TimeSpan expiresIn) =>
        S3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = DateTime.UtcNow.Add(expiresIn),
        });

    public string CreateDownloadUrl(string objectKey, string downloadFileName, string contentType, TimeSpan expiresIn, bool inline) =>
        S3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiresIn),
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentType = contentType,
                ContentDisposition = ContentDisposition(downloadFileName, inline),
            },
        });

    public async Task<StoredObject?> GetMetadataAsync(string objectKey, CancellationToken ct)
    {
        try
        {
            var r = await S3.GetObjectMetadataAsync(_options.BucketName, objectKey, ct);
            return new StoredObject(r.ContentLength, r.Headers.ContentType);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<byte[]> ReadHeadAsync(string objectKey, int count, CancellationToken ct)
    {
        using var r = await S3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            ByteRange = new ByteRange(0, count - 1),
        }, ct);
        using var ms = new MemoryStream();
        await r.ResponseStream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    public async Task PutAsync(string objectKey, Stream content, string contentType, CancellationToken ct)
    {
        // Buffer so the SDK knows the length (R2 rejects unsigned streaming bodies); files here are small.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        await S3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = buffer,
            ContentType = contentType,
        }, ct);
    }

    public async Task<(Stream Content, string? ContentType)?> GetAsync(string objectKey, CancellationToken ct)
    {
        try
        {
            var r = await S3.GetObjectAsync(_options.BucketName, objectKey, ct);
            return (r.ResponseStream, r.Headers.ContentType);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task CopyAsync(string sourceKey, string destinationKey, CancellationToken ct) =>
        S3.CopyObjectAsync(_options.BucketName, sourceKey, _options.BucketName, destinationKey, ct);

    public Task DeleteAsync(string objectKey, CancellationToken ct) =>
        S3.DeleteObjectAsync(_options.BucketName, objectKey, ct);   // S3 delete succeeds for a missing key

    /// <summary>Content-Disposition with an ASCII fallback and the real name encoded per RFC 5987.</summary>
    internal static string ContentDisposition(string fileName, bool inline)
    {
        var ascii = new string(fileName.Select(c => c is >= ' ' and <= '~' and not '"' and not '\\' and not ';' ? c : '_').ToArray());
        return $"{(inline ? "inline" : "attachment")}; filename=\"{ascii}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
    }

    public void Dispose() => _s3?.Dispose();
}
