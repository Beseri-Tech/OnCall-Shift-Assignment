namespace Rota.Api.Services;

/// <summary>A stored file being read back.</summary>
public sealed record StoredDocument(Stream Content, string ContentType) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

/// <summary>
/// Where uploaded documents live. The API checks who may see a file before reading it, so implementations only deal with bytes
/// (local disk here; Cloudflare R2 can be plugged in by registering another implementation).
/// </summary>
public interface IDocumentStore
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct);

    /// <summary>Null when there is no such file.</summary>
    Task<StoredDocument?> GetAsync(string key, CancellationToken ct);

    /// <summary>Does nothing when there is no such file.</summary>
    Task DeleteAsync(string key, CancellationToken ct);
}

/// <summary>Stores files under a folder (Documents:Path). Fine for one server; the key is never trusted as a path.</summary>
public sealed class LocalDocumentStore(IConfiguration config, IHostEnvironment env) : IDocumentStore
{
    private readonly string _root = Path.GetFullPath(config["Documents:Path"] is { Length: > 0 } p
        ? p : Path.Combine(env.ContentRootPath, "data", "documents"));

    private string PathOf(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, key));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Invalid document key.", nameof(key));
        return full;
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public Task<StoredDocument?> GetAsync(string key, CancellationToken ct)
    {
        var path = PathOf(key);
        return Task.FromResult<StoredDocument?>(File.Exists(path)
            ? new StoredDocument(File.OpenRead(path), ContentTypeOf(path)) : null);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = PathOf(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private static string ContentTypeOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream",
    };
}
