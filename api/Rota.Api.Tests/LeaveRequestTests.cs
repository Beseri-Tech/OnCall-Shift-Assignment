using System.Net;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Rota.Api.Data;
using Rota.Api.Endpoints;
using Rota.Api.Storage;

namespace Rota.Api.Tests;

/// <summary>Officers request confirmed leave (several ranges, optional document each); the admin approves or rejects every range.</summary>
public class LeaveRequestTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static readonly byte[] Pdf = "%PDF-1.4 test"u8.ToArray();

    /// <summary>In-memory stand-in for R2: "uploading" writes straight into it.</summary>
    private sealed class FakeStorage : IDocumentStorage
    {
        public readonly ConcurrentDictionary<string, byte[]> Objects = new();
        public bool IsEnabled => true;
        public Task<string> CreateUploadUrlAsync(string objectKey, string contentType, TimeSpan expiresIn) => Task.FromResult($"https://r2.test/up/{objectKey}");
        public Task<string> CreateDownloadUrlAsync(string objectKey, string name, string contentType, TimeSpan expiresIn, bool inline) => Task.FromResult($"https://r2.test/down/{objectKey}");
        public Task<StoredObject?> GetMetadataAsync(string objectKey, CancellationToken ct) =>
            Task.FromResult(Objects.TryGetValue(objectKey, out var b) ? new StoredObject(b.Length, null) : null);
        public Task<byte[]> ReadHeadAsync(string objectKey, int count, CancellationToken ct) => Task.FromResult(Objects[objectKey].Take(count).ToArray());
        public Task PutAsync(string objectKey, Stream content, string contentType, CancellationToken ct) => throw new NotSupportedException();
        public Task<(Stream Content, string? ContentType)?> GetAsync(string objectKey, CancellationToken ct) => throw new NotSupportedException();
        public Task CopyAsync(string sourceKey, string destinationKey, CancellationToken ct) { Objects[destinationKey] = Objects[sourceKey]; return Task.CompletedTask; }
        public Task DeleteAsync(string objectKey, CancellationToken ct) { Objects.TryRemove(objectKey, out _); return Task.CompletedTask; }
    }

    private WebApplicationFactory<Program> App(FakeStorage storage, Dictionary<string, string?>? settings = null) =>
        fixture.CreateApp(settings).WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<IDocumentStorage>(storage)));

    /// <summary>The browser's three steps: ask for a link, PUT to R2 (the fake bucket), confirm. Returns the document id.</summary>
    private static async Task<Guid> UploadAsync(HttpClient c, FakeStorage storage, string name, byte[] bytes)
    {
        var ticket = (await (await c.PostAsJsonAsync("/api/documents/upload-url",
            new UploadUrlRequest(DocumentKind.LeaveDocument, name, "application/pdf", bytes.Length), ApiFixture.Json)).Content
            .ReadFromJsonAsync<UploadTicket>(ApiFixture.Json))!;
        storage.Objects[ticket.UploadUrl["https://r2.test/up/".Length..]] = bytes;
        (await c.PostAsync($"/api/documents/{ticket.DocumentId}/complete", null)).EnsureSuccessStatusCode();
        return ticket.DocumentId;
    }

    private static Task<HttpResponseMessage> Send(HttpClient c, params object[] ranges) =>
        c.PostAsJsonAsync("/api/leave-requests", new { ranges }, ApiFixture.Json);

    private static object Range(string from, string to, string kind, string? reason = "Because", Guid? documentId = null) =>
        new { startDate = from, endDate = to, kind, reason, documentId };

    private static async Task<List<LeaveRequestRangeDto>> Read(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode) Assert.Fail($"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<List<LeaveRequestRangeDto>>(ApiFixture.Json))!;
    }

    [Fact]
    public async Task Officer_requests_two_ranges_and_admin_approves_one_and_rejects_the_other()
    {
        var storage = new FakeStorage();
        await using var app = App(storage);
        var admin = await ApiFixture.AdminClientAsync(app);
        (await admin.PostAsJsonAsync("/api/admin/people/bulk", new { names = "Dr One\nDr Two" })).EnsureSuccessStatusCode();
        var people = (await admin.GetFromJsonAsync<List<AdminPersonDto>>("/api/admin/people", ApiFixture.Json))!;
        var officer = await ApiFixture.OfficerClientAsync(app, admin, people[0].Id);

        var docId = await UploadAsync(officer, storage, "letter.pdf", Pdf);
        var sent = await Read(await Send(officer, Range("2026-11-14", "2026-11-15", "Relief", "Relief at Klinik UTC"),
            Range("2026-11-20", "2026-11-22", "Trip", "Umrah", docId)));
        Assert.Equal(2, sent.Count);
        Assert.All(sent, r => Assert.Equal(LeaveRequestStatus.Pending, r.Status));
        var trip = sent.Single(r => r.Kind == LeaveKind.Trip);
        var relief = sent.Single(r => r.Kind == LeaveKind.Relief);
        Assert.Equal("letter.pdf", trip.DocumentName);
        Assert.Null(relief.DocumentName);

        // The admin and the officer can open the document; it hangs off its range.
        Assert.Equal(docId, trip.DocumentId);
        foreach (var who in new[] { officer, admin })
            (await who.GetAsync($"/api/documents/{docId}/download-url")).EnsureSuccessStatusCode();

        // Approving creates confirmed leave of that kind; rejecting needs a note.
        (await admin.PostAsJsonAsync($"/api/admin/leave-requests/ranges/{relief.Id}/approve", new { })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/admin/leave-requests/ranges/{trip.Id}/reject", new { })).StatusCode);
        (await admin.PostAsJsonAsync($"/api/admin/leave-requests/ranges/{trip.Id}/reject", new { note = "Dates clash" })).EnsureSuccessStatusCode();

        var approvals = (await admin.GetFromJsonAsync<List<LeaveApprovalDto>>("/api/admin/leave-approvals", ApiFixture.Json))!;
        var a = Assert.Single(approvals);
        Assert.Equal(LeaveKind.Relief, a.Kind);
        Assert.Equal(new DateOnly(2026, 11, 14), a.StartDate);

        var mine = await Read(await officer.GetAsync("/api/leave-requests/mine"));
        Assert.Equal(LeaveRequestStatus.Approved, mine.Single(r => r.Id == relief.Id).Status);
        Assert.Equal("Dates clash", mine.Single(r => r.Id == trip.Id).DecisionNote);

        // Leave the admin enters directly shows up for the officer too, and removing approved leave updates the request.
        (await admin.PostAsJsonAsync("/api/admin/leave-approvals", new UpsertLeaveApprovalRequest(people[0].Id,
            new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 3), "Course", null, LeaveKind.Other), ApiFixture.Json)).EnsureSuccessStatusCode();
        mine = await Read(await officer.GetAsync("/api/leave-requests/mine"));
        Assert.Contains(mine, r => r.AddedByAdmin && r.Reason == "Course" && r.Status == LeaveRequestStatus.Approved);
        (await admin.DeleteAsync($"/api/admin/leave-approvals/{a.Id}")).EnsureSuccessStatusCode();
        mine = await Read(await officer.GetAsync("/api/leave-requests/mine"));
        Assert.Equal(LeaveRequestStatus.Removed, mine.Single(r => r.Id == relief.Id).Status);

        // A decided range can't be decided again or withdrawn.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/admin/leave-requests/ranges/{trip.Id}/approve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await officer.DeleteAsync($"/api/leave-requests/ranges/{trip.Id}")).StatusCode);
    }

    [Fact]
    public async Task Bad_requests_are_refused_and_another_officer_cannot_see_the_document()
    {
        var storage = new FakeStorage();
        await using var app = App(storage);
        var admin = await ApiFixture.AdminClientAsync(app);
        (await admin.PostAsJsonAsync("/api/admin/people/bulk", new { names = "Dr One\nDr Two" })).EnsureSuccessStatusCode();
        var people = (await admin.GetFromJsonAsync<List<AdminPersonDto>>("/api/admin/people", ApiFixture.Json))!;
        var one = await ApiFixture.OfficerClientAsync(app, admin, people[0].Id);
        var two = await ApiFixture.OfficerClientAsync(app, admin, people[1].Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await Send(one, Range("2026-11-14", "2026-11-15", "Trip", null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(one, Range("2026-11-15", "2026-11-14", "Trip"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(one, Range("2026-11-14", "2026-11-16", "Trip"), Range("2026-11-16", "2026-11-17", "Cr"))).StatusCode);

        // Someone else's upload can't be attached, and one upload can't serve two ranges.
        var docId = await UploadAsync(one, storage, "cr.pdf", Pdf);
        Assert.False((await Send(two, Range("2026-11-14", "2026-11-15", "Cr", "Because", docId))).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(one, Range("2026-11-14", "2026-11-15", "Cr", "Because", docId),
            Range("2026-11-20", "2026-11-21", "Cr", "Because", docId))).StatusCode);

        var sent = await Read(await Send(one, Range("2026-11-14", "2026-11-15", "Cr", "Because", docId)));
        Assert.Equal(HttpStatusCode.Forbidden, (await one.GetAsync("/api/admin/leave-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await two.GetAsync($"/api/documents/{docId}/download-url")).StatusCode);

        // Withdrawing removes the document too.
        Assert.Equal(HttpStatusCode.NoContent, (await one.DeleteAsync($"/api/leave-requests/ranges/{sent[0].Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await one.GetAsync($"/api/documents/{docId}/download-url")).StatusCode);
    }

    [Fact]
    public async Task Cleanup_removes_stale_uploads_and_rejected_documents_but_keeps_approved_ones()
    {
        var storage = new FakeStorage();
        await using var app = App(storage, new() { ["R2:RejectedRetentionDays"] = "0", ["R2:CleanupEnabled"] = "false" });
        var admin = await ApiFixture.AdminClientAsync(app);
        (await admin.PostAsJsonAsync("/api/admin/people/bulk", new { names = "Dr One" })).EnsureSuccessStatusCode();
        var people = (await admin.GetFromJsonAsync<List<AdminPersonDto>>("/api/admin/people", ApiFixture.Json))!;
        var officer = await ApiFixture.OfficerClientAsync(app, admin, people[0].Id);

        var keep = await UploadAsync(officer, storage, "approved.pdf", Pdf);
        var drop = await UploadAsync(officer, storage, "rejected.pdf", Pdf);
        var sent = await Read(await Send(officer, Range("2026-11-14", "2026-11-15", "Trip", "Umrah", keep),
            Range("2026-11-20", "2026-11-21", "Cr", "CR", drop)));
        (await admin.PostAsJsonAsync($"/api/admin/leave-requests/ranges/{sent.Single(r => r.Kind == LeaveKind.Trip).Id}/approve", new { })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/admin/leave-requests/ranges/{sent.Single(r => r.Kind == LeaveKind.Cr).Id}/reject", new { note = "No" })).EnsureSuccessStatusCode();

        // An upload that was never confirmed, made two days ago.
        var stale = (await (await officer.PostAsJsonAsync("/api/documents/upload-url",
            new UploadUrlRequest(DocumentKind.LeaveDocument, "x.pdf", "application/pdf", 10), ApiFixture.Json)).Content
            .ReadFromJsonAsync<UploadTicket>(ApiFixture.Json))!.DocumentId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RotaDbContext>();
            (await db.Documents.FindAsync(stale))!.CreatedAt = DateTimeOffset.UtcNow.AddDays(-2);
            await db.SaveChangesAsync();
        }

        using (var scope = app.Services.CreateScope())
            Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<DocumentService>().CleanupAsync(CancellationToken.None));

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RotaDbContext>();
            Assert.Equal(DocumentStatus.Available, (await db.Documents.FindAsync(keep))!.Status);
            Assert.Equal(DocumentStatus.Deleted, (await db.Documents.FindAsync(drop))!.Status);
            Assert.Equal(DocumentStatus.Deleted, (await db.Documents.FindAsync(stale))!.Status);
        }

        // The approved leave still shows its document to the admin.
        var approval = Assert.Single((await admin.GetFromJsonAsync<List<LeaveApprovalDto>>("/api/admin/leave-approvals", ApiFixture.Json))!);
        Assert.Equal(keep, approval.DocumentId);
        Assert.Equal("approved.pdf", approval.DocumentName);
    }
}
