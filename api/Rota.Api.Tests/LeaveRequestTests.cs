using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Rota.Api.Data;

namespace Rota.Api.Tests;

/// <summary>Officers request confirmed leave (several ranges, optional document each); the admin approves or rejects every range.</summary>
public class LeaveRequestTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4 test");

    private static MultipartFormDataContent Form(object[] ranges, params (int Index, string Name, byte[] Bytes)[] files)
    {
        var form = new MultipartFormDataContent { { new StringContent(JsonSerializer.Serialize(new { ranges }, ApiFixture.Json)), "request" } };
        foreach (var f in files) form.Add(new ByteArrayContent(f.Bytes), $"file{f.Index}", f.Name);
        return form;
    }

    private static object Range(string from, string to, string kind, string? reason = "Because") => new { startDate = from, endDate = to, kind, reason };

    private static async Task<List<LeaveRequestRangeDto>> Read(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode) Assert.Fail($"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<List<LeaveRequestRangeDto>>(ApiFixture.Json))!;
    }

    [Fact]
    public async Task Officer_requests_two_ranges_and_admin_approves_one_and_rejects_the_other()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        (await admin.PostAsJsonAsync("/api/admin/people/bulk", new { names = "Dr One\nDr Two" })).EnsureSuccessStatusCode();
        var people = (await admin.GetFromJsonAsync<List<AdminPersonDto>>("/api/admin/people", ApiFixture.Json))!;
        var officer = await ApiFixture.OfficerClientAsync(app, admin, people[0].Id);

        var sent = await Read(await officer.PostAsync("/api/leave-requests",
            Form([Range("2026-11-14", "2026-11-15", "Relief", "Relief at Klinik UTC"), Range("2026-11-20", "2026-11-22", "Trip", "Umrah")],
                (1, "letter.pdf", Pdf))));
        Assert.Equal(2, sent.Count);
        Assert.All(sent, r => Assert.Equal(LeaveRequestStatus.Pending, r.Status));
        var trip = sent.Single(r => r.Kind == LeaveKind.Trip);
        var relief = sent.Single(r => r.Kind == LeaveKind.Relief);
        Assert.Equal("letter.pdf", trip.DocumentName);
        Assert.Null(relief.DocumentName);

        // The document comes back for the officer and the admin.
        foreach (var who in new[] { officer, admin })
        {
            var doc = await who.GetAsync($"/api/leave-requests/ranges/{trip.Id}/document");
            Assert.Equal(HttpStatusCode.OK, doc.StatusCode);
            Assert.Equal(Pdf, await doc.Content.ReadAsByteArrayAsync());
        }

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

        // A decided range can't be decided again or withdrawn.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/admin/leave-requests/ranges/{relief.Id}/approve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await officer.DeleteAsync($"/api/leave-requests/ranges/{relief.Id}")).StatusCode);
    }

    [Fact]
    public async Task Bad_requests_are_refused_and_another_officer_cannot_see_the_document()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        (await admin.PostAsJsonAsync("/api/admin/people/bulk", new { names = "Dr One\nDr Two" })).EnsureSuccessStatusCode();
        var people = (await admin.GetFromJsonAsync<List<AdminPersonDto>>("/api/admin/people", ApiFixture.Json))!;
        var one = await ApiFixture.OfficerClientAsync(app, admin, people[0].Id);
        var two = await ApiFixture.OfficerClientAsync(app, admin, people[1].Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await one.PostAsync("/api/leave-requests", Form([Range("2026-11-14", "2026-11-15", "Trip", null)]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await one.PostAsync("/api/leave-requests", Form([Range("2026-11-15", "2026-11-14", "Trip")]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await one.PostAsync("/api/leave-requests",
            Form([Range("2026-11-14", "2026-11-16", "Trip"), Range("2026-11-16", "2026-11-17", "Cr")]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await one.PostAsync("/api/leave-requests",
            Form([Range("2026-11-14", "2026-11-15", "Trip")], (0, "fake.pdf", Encoding.ASCII.GetBytes("not a pdf"))))).StatusCode);

        var sent = await Read(await one.PostAsync("/api/leave-requests",
            Form([Range("2026-11-14", "2026-11-15", "Cr")], (0, "cr.pdf", Pdf))));
        Assert.Equal(HttpStatusCode.NotFound, (await two.GetAsync($"/api/leave-requests/ranges/{sent[0].Id}/document")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await one.GetAsync("/api/admin/leave-requests")).StatusCode);

        // The officer can withdraw while it is pending.
        Assert.Equal(HttpStatusCode.NoContent, (await one.DeleteAsync($"/api/leave-requests/ranges/{sent[0].Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await one.GetAsync($"/api/leave-requests/ranges/{sent[0].Id}/document")).StatusCode);
    }
}
