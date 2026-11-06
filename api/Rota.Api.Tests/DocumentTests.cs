using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Rota.Api.Data;
using Rota.Api.Endpoints;
using Rota.Api.Storage;

namespace Rota.Api.Tests;

/// <summary>Document storage: two-step upload, server-side file checks, who may read or delete what.</summary>
public class DocumentTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    /// <summary>In-memory stand-in for R2: "uploading" writes straight into it.</summary>
    private sealed class FakeStorage : IDocumentStorage
    {
        public readonly ConcurrentDictionary<string, byte[]> Objects = new();
        public bool IsEnabled { get; set; } = true;
        public Task<string> CreateUploadUrlAsync(string objectKey, string contentType, TimeSpan expiresIn) => Task.FromResult($"https://r2.test/up/{objectKey}");
        public Task<string> CreateDownloadUrlAsync(string objectKey, string name, string contentType, TimeSpan expiresIn, bool inline) =>
            Task.FromResult($"https://r2.test/down/{objectKey}");
        public Task<StoredObject?> GetMetadataAsync(string objectKey, CancellationToken ct) =>
            Task.FromResult(Objects.TryGetValue(objectKey, out var b) ? new StoredObject(b.Length, null) : null);
        public Task<byte[]> ReadHeadAsync(string objectKey, int count, CancellationToken ct) =>
            Task.FromResult(Objects[objectKey].Take(count).ToArray());
        public async Task PutAsync(string objectKey, Stream content, string contentType, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            Objects[objectKey] = ms.ToArray();
        }
        public Task<(Stream Content, string? ContentType)?> GetAsync(string objectKey, CancellationToken ct) =>
            Task.FromResult<(Stream, string?)?>(Objects.TryGetValue(objectKey, out var b) ? (new MemoryStream(b), null) : null);
        public Task CopyAsync(string sourceKey, string destinationKey, CancellationToken ct)
        {
            Objects[destinationKey] = Objects[sourceKey];
            return Task.CompletedTask;
        }
        public Task DeleteAsync(string objectKey, CancellationToken ct)
        {
            Objects.TryRemove(objectKey, out _);
            return Task.CompletedTask;
        }
        public void Dispose() { }
    }

    private static readonly byte[] Pdf = "%PDF-1.7 fake pdf body"u8.ToArray();

    private static async Task<T> Read<T>(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.ReasonPhrase}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<T>(ApiFixture.Json))!;
    }

    private WebApplicationFactory<Program> App(FakeStorage storage) =>
        fixture.CreateApp().WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<IDocumentStorage>(storage)));

    private static async Task<UploadTicket> RequestAsync(HttpClient c, DocumentKind kind, string name, string type, long size) =>
        await Read<UploadTicket>(await c.PostAsJsonAsync("/api/documents/upload-url", new UploadUrlRequest(kind, name, type, size), ApiFixture.Json));

    /// <summary>Request, "upload" to the fake bucket, complete.</summary>
    private static async Task<DocumentDto> UploadAsync(HttpClient c, FakeStorage storage, DocumentKind kind, string name,
        string type, byte[] bytes)
    {
        var ticket = await RequestAsync(c, kind, name, type, bytes.Length);
        storage.Objects[ticket.UploadUrl["https://r2.test/up/".Length..]] = bytes;
        return await Read<DocumentDto>(await c.PostAsync($"/api/documents/{ticket.DocumentId}/complete", null));
    }

    private static async Task<HttpClient> OfficerAsync(WebApplicationFactory<Program> app, HttpClient admin, string name)
    {
        var p = await Read<SavePersonResult>(await admin.PostAsJsonAsync("/api/admin/people", new UpsertPersonRequest(name, null), ApiFixture.Json));
        return await ApiFixture.OfficerClientAsync(app, admin, p.Id);
    }

    [Fact]
    public async Task Upload_is_checked_then_downloadable_by_owner_and_admin_only()
    {
        var storage = new FakeStorage();
        await using var app = App(storage);
        var admin = await ApiFixture.AdminClientAsync(app);
        var aminah = await OfficerAsync(app, admin, "Dr Aminah");
        var bob = await OfficerAsync(app, admin, "Dr Bob");

        var doc = await UploadAsync(aminah, storage, DocumentKind.LeaveDocument, "invitation.pdf", "application/pdf", Pdf);
        Assert.Equal(("invitation.pdf", Pdf.Length), (doc.FileName, (int)doc.SizeBytes));
        Assert.DoesNotContain(storage.Objects.Keys, k => k.StartsWith("quarantine/"));   // promoted to a permanent key
        Assert.Single(storage.Objects.Keys, k => k.StartsWith("documents/leavedocument/") && k.EndsWith(".pdf"));

        var link = await Read<DownloadTicket>(await aminah.GetAsync($"/api/documents/{doc.Id}/download-url"));
        Assert.StartsWith("https://r2.test/down/documents/", link.DownloadUrl);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/documents/{doc.Id}/download-url")).StatusCode);

        // Another officer can't tell it exists.
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/documents/{doc.Id}/download-url")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/documents/{doc.Id}")).StatusCode);

        // Completing twice is harmless.
        Assert.Equal(HttpStatusCode.OK, (await aminah.PostAsync($"/api/documents/{doc.Id}/complete", null)).StatusCode);

        (await aminah.DeleteAsync($"/api/documents/{doc.Id}")).EnsureSuccessStatusCode();
        Assert.Empty(storage.Objects);
        Assert.Equal(HttpStatusCode.NotFound, (await aminah.GetAsync($"/api/documents/{doc.Id}/download-url")).StatusCode);
    }

    [Fact]
    public async Task Pending_and_spoofed_uploads_are_never_downloadable()
    {
        var storage = new FakeStorage();
        await using var app = App(storage);
        var admin = await ApiFixture.AdminClientAsync(app);
        var aminah = await OfficerAsync(app, admin, "Dr Aminah");

        // Link issued but nothing uploaded yet.
        var pending = await RequestAsync(aminah, DocumentKind.Other, "a.pdf", "application/pdf", Pdf.Length);
        Assert.Equal(HttpStatusCode.NotFound, (await aminah.GetAsync($"/api/documents/{pending.DocumentId}/download-url")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await aminah.PostAsync($"/api/documents/{pending.DocumentId}/complete", null)).StatusCode);

        // A .pdf name on bytes that aren't a PDF is rejected and its bytes removed.
        byte[] fake = "MZ not a pdf at all"u8.ToArray();
        var spoof = await RequestAsync(aminah, DocumentKind.Other, "evil.pdf", "application/pdf", fake.Length);
        storage.Objects[$"quarantine/{spoof.DocumentId:N}"] = fake;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await aminah.PostAsync($"/api/documents/{spoof.DocumentId}/complete", null)).StatusCode);
        Assert.DoesNotContain(storage.Objects.Keys, k => k.Contains(spoof.DocumentId.ToString("N")));

        // Announced size must match what arrived.
        var lie = await RequestAsync(aminah, DocumentKind.Other, "b.pdf", "application/pdf", 10);
        storage.Objects[$"quarantine/{lie.DocumentId:N}"] = Pdf;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await aminah.PostAsync($"/api/documents/{lie.DocumentId}/complete", null)).StatusCode);
    }

    [Fact]
    public async Task Upload_requests_are_validated()
    {
        var storage = new FakeStorage();
        await using var app = App(storage);
        var admin = await ApiFixture.AdminClientAsync(app);
        var aminah = await OfficerAsync(app, admin, "Dr Aminah");

        async Task<HttpStatusCode> Try(HttpClient c, DocumentKind k, string name, string type, long size) =>
            (await c.PostAsJsonAsync("/api/documents/upload-url", new UploadUrlRequest(k, name, type, size), ApiFixture.Json)).StatusCode;

        Assert.Equal(HttpStatusCode.BadRequest, await Try(aminah, DocumentKind.Other, "run.exe", "application/octet-stream", 100));
        Assert.Equal(HttpStatusCode.BadRequest, await Try(aminah, DocumentKind.Other, "a.pdf", "image/png", 100));        // type vs name
        Assert.Equal(HttpStatusCode.BadRequest, await Try(aminah, DocumentKind.Other, "a.pdf", "application/pdf", 0));
        Assert.Equal(HttpStatusCode.BadRequest, await Try(aminah, DocumentKind.Other, "a.pdf", "application/pdf", 21L * 1024 * 1024));
        Assert.Equal(HttpStatusCode.Forbidden, await Try(aminah, DocumentKind.Manual, "m.pdf", "application/pdf", 100));  // admins only
        Assert.Equal(HttpStatusCode.BadRequest, await Try(admin, DocumentKind.Manual, "m.png", "image/png", 100));        // manual is a PDF

        var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, await Try(anonymous, DocumentKind.Other, "a.pdf", "application/pdf", 100));
    }

    [Fact]
    public async Task The_manual_is_for_everyone_and_a_new_one_replaces_the_old()
    {
        var storage = new FakeStorage();
        await using var app = App(storage);
        var admin = await ApiFixture.AdminClientAsync(app);
        var aminah = await OfficerAsync(app, admin, "Dr Aminah");

        Assert.Equal(HttpStatusCode.NoContent, (await aminah.GetAsync("/api/documents/manual")).StatusCode);

        var first = await UploadAsync(admin, storage, DocumentKind.Manual, "manual-v1.pdf", "application/pdf", Pdf);
        Assert.Equal(first.Id, (await Read<DocumentDto>(await aminah.GetAsync("/api/documents/manual"))).Id);
        Assert.Equal(HttpStatusCode.OK, (await aminah.GetAsync($"/api/documents/{first.Id}/download-url")).StatusCode);

        var second = await UploadAsync(admin, storage, DocumentKind.Manual, "manual-v2.pdf", "application/pdf", Pdf);
        Assert.Equal(second.Id, (await Read<DocumentDto>(await aminah.GetAsync("/api/documents/manual"))).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await aminah.GetAsync($"/api/documents/{first.Id}/download-url")).StatusCode);
        Assert.Single(storage.Objects);

        // Officers can read the manual but not remove it.
        Assert.Equal(HttpStatusCode.NotFound, (await aminah.DeleteAsync($"/api/documents/{second.Id}")).StatusCode);
        (await admin.DeleteAsync($"/api/documents/{second.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await aminah.GetAsync("/api/documents/manual")).StatusCode);
    }

    [Fact]
    public async Task Without_R2_configured_uploads_say_so()
    {
        await using var app = App(new FakeStorage { IsEnabled = false });
        var admin = await ApiFixture.AdminClientAsync(app);
        var res = await admin.PostAsJsonAsync("/api/documents/upload-url",
            new UploadUrlRequest(DocumentKind.Manual, "m.pdf", "application/pdf", 100), ApiFixture.Json);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
    }
}
