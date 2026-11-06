namespace Rota.Api.Storage;

public sealed record StoredObject(long Size, string? ContentType);

/// <summary>
/// Where document bytes live. Provider-neutral so the rest of the app never touches the AWS SDK; the one implementation
/// is <see cref="R2DocumentStorage"/>. Callers only ever pass server-generated object keys.
/// </summary>
public interface IDocumentStorage
{
    /// <summary>False when R2 has not been configured; every other member then throws.</summary>
    bool IsEnabled { get; }

    /// <summary>A short-lived link the browser PUTs the file to. The Content-Type is signed, so the browser must send exactly it.</summary>
    Task<string> CreateUploadUrlAsync(string objectKey, string contentType, TimeSpan expiresIn);

    /// <summary>A short-lived link the browser opens to read the file; <paramref name="downloadFileName"/> is what it is saved as.</summary>
    Task<string> CreateDownloadUrlAsync(string objectKey, string downloadFileName, string contentType, TimeSpan expiresIn, bool inline);

    /// <summary>Size and type of the stored object, or null when it does not exist.</summary>
    Task<StoredObject?> GetMetadataAsync(string objectKey, CancellationToken ct);

    /// <summary>The first <paramref name="count"/> bytes of the object (for checking the file signature).</summary>
    Task<byte[]> ReadHeadAsync(string objectKey, int count, CancellationToken ct);

    /// <summary>Stores a stream under a server-chosen key (for features that pass files through the API).</summary>
    Task PutAsync(string objectKey, Stream content, string contentType, CancellationToken ct);

    /// <summary>Opens the object for reading, or null when it does not exist. The caller disposes the stream.</summary>
    Task<(Stream Content, string? ContentType)?> GetAsync(string objectKey, CancellationToken ct);

    Task CopyAsync(string sourceKey, string destinationKey, CancellationToken ct);

    /// <summary>Deletes the object; succeeds when it is already gone.</summary>
    Task DeleteAsync(string objectKey, CancellationToken ct);
}
