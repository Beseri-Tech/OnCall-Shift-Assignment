using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Rota.Api.Data;
using Rota.Api.Reports;
using Rota.Api.Storage;

namespace Rota.Api.Tests;

/// <summary>The published-rota PDF report: only for published rotas, and it renders a real PDF for a rota with swaps and overrides.</summary>
public class ReportTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static async Task<T> Read<T>(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.ReasonPhrase}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<T>(ApiFixture.Json))!;
    }

    private const string Names = "Dr Aisyah Rahman\nDr Benjamin Tan\nDr Chong Wei Ling\nDr Dinesh Kumar\nDr Elena Ahmad\nDr Farid Ismail\n" +
        "Dr Grace Lim\nDr Hafiz Omar\nDr Intan Zulkifli\nDr Jason Lee\nDr Kavita Nair\nDr Lutfi Hakim\nDr Mei Ling Ong\nDr Nurul Huda";

    [Fact]
    public async Task Report_is_only_for_published_rotas_and_is_a_pdf()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        (await admin.PostAsJsonAsync("/api/admin/people/bulk", new { names = Names })).EnsureSuccessStatusCode();
        var people = await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"));
        var period = await Read<PeriodDto>(await admin.PostAsJsonAsync("/api/admin/periods",
            new UpsertPeriodRequest("Dec 2026", new DateOnly(2026, 12, 15), new DateOnly(2026, 12, 28), null), ApiFixture.Json));
        (await admin.PutAsJsonAsync($"/api/admin/periods/{period.Id}/days",
            new PeriodDayDto(new DateOnly(2026, 12, 25), PeriodDayKind.Holiday, "Christmas Day"), ApiFixture.Json)).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/admin/periods/{period.Id}/days",
            new PeriodDayDto(new DateOnly(2026, 12, 24), PeriodDayKind.Peak, "Christmas Eve"), ApiFixture.Json)).EnsureSuccessStatusCode();

        // Earlier weekend/holiday duty, leave, preferences and a points grant for the report to explain.
        (await admin.PutAsJsonAsync("/api/admin/tally", people.Select((p, i) => new TallyUpdate(p.Id, 3, i % 4)).ToList(), ApiFixture.Json))
            .EnsureSuccessStatusCode();
        Task Entries(Guid who, DateOnly[] leave, DateOnly[] preferred) => admin.PutAsJsonAsync(
            $"/api/people/{who}/entries?periodId={period.Id}",
            new EntriesDto(leave.Select(d => new LeaveEntryDto(d, "Family")).ToList(), preferred), ApiFixture.Json);
        await Entries(people[0].Id, [new(2026, 12, 18)], [new(2026, 12, 19)]);
        await Entries(people[1].Id, [], [new(2026, 12, 25), new(2026, 12, 26)]);
        await Entries(people[2].Id, [new(2026, 12, 20)], []);
        await Entries(people[3].Id, [], [new(2026, 12, 22)]);
        (await admin.PutAsJsonAsync($"/api/admin/periods/{period.Id}/points/{people[4].Id}", new PointGrantRequest(2, "Outstation duty"),
            ApiFixture.Json)).EnsureSuccessStatusCode();
        var officerA = await ApiFixture.OfficerClientAsync(app, admin, people[0].Id);
        var officerB = await ApiFixture.OfficerClientAsync(app, admin, people[1].Id);

        (await admin.PostAsync($"/api/admin/periods/{period.Id}/lock", null)).EnsureSuccessStatusCode();
        var run = await Read<RunDetailDto>(await admin.PostAsJsonAsync($"/api/admin/periods/{period.Id}/generate", new GenerateRequest(7)));

        // Not published yet: no report.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.GetAsync($"/api/admin/runs/{run.Run.Id}/report.pdf")).StatusCode);

        (await admin.PostAsJsonAsync($"/api/admin/runs/{run.Run.Id}/review", new ReviewRequest(null))).EnsureSuccessStatusCode();
        DateOnly DayOf(Guid person) => run.Days.First(x => x.PersonId == person).Date;
        var swap = await Read<SwapDto>(await officerA.PostAsJsonAsync("/api/swaps",
            new CreateSwapRequest(run.Run.Id, DayOf(people[0].Id), DayOf(people[1].Id), "Wedding that weekend")));
        (await officerB.PostAsync($"/api/swaps/{swap.Id}/accept", null)).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/admin/runs/{run.Run.Id}/assignments/{DayOf(people[5].Id):yyyy-MM-dd}", new OverrideRequest(people[6].Id)))
            .EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/admin/runs/{run.Run.Id}/publish", null)).EnsureSuccessStatusCode();

        // Officers can't download it.
        Assert.Equal(HttpStatusCode.Forbidden, (await officerA.GetAsync($"/api/admin/runs/{run.Run.Id}/report.pdf")).StatusCode);

        var res = await admin.GetAsync($"/api/admin/runs/{run.Run.Id}/report.pdf");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/pdf", res.Content.Headers.ContentType?.MediaType);
        byte[] pdf = await res.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
        Assert.True(pdf.Length > 10_000);

        // Set ROTA_REPORT_SAMPLE to a file path to keep the PDF (used for the sample report).
        if (Environment.GetEnvironmentVariable("ROTA_REPORT_SAMPLE") is { Length: > 0 } path) await File.WriteAllBytesAsync(path, pdf);
    }

    [Fact]
    public async Task Publishing_saves_the_report_to_storage_and_admins_can_list_and_download_it()
    {
        var storage = new DocumentTests.FakeStorage();
        await using var app = fixture.CreateApp().WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<IDocumentStorage>(storage)));
        var admin = await ApiFixture.AdminClientAsync(app);
        (await admin.PostAsJsonAsync("/api/admin/people/bulk", new { names = "Dr A\nDr B\nDr C" })).EnsureSuccessStatusCode();
        var people = await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"));
        var period = await Read<PeriodDto>(await admin.PostAsJsonAsync("/api/admin/periods",
            new UpsertPeriodRequest("Jan 2027", new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 10), null), ApiFixture.Json));
        var officer = await ApiFixture.OfficerClientAsync(app, admin, people[0].Id);
        (await admin.PostAsync($"/api/admin/periods/{period.Id}/lock", null)).EnsureSuccessStatusCode();
        var run = await Read<RunDetailDto>(await admin.PostAsJsonAsync($"/api/admin/periods/{period.Id}/generate", new GenerateRequest(3)));

        var before = await Read<ReportArchiveDto>(await admin.GetAsync("/api/admin/reports"));
        Assert.True(before.StorageEnabled);
        Assert.Empty(before.Reports);

        (await admin.PostAsync($"/api/admin/runs/{run.Run.Id}/publish", null)).EnsureSuccessStatusCode();
        var after = await Read<ReportArchiveDto>(await admin.GetAsync("/api/admin/reports"));
        var saved = Assert.Single(after.Reports);
        Assert.Equal("Jan 2027", saved.PeriodName);
        Assert.Empty(after.Unarchived);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(storage.Objects.Single(o => o.Key.StartsWith("reports/")).Value, 0, 5));

        // Admins get a short-lived link; officers get nothing.
        (await admin.GetAsync($"/api/documents/{saved.DocumentId}/download-url")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await officer.GetAsync($"/api/documents/{saved.DocumentId}/download-url")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.GetAsync("/api/admin/reports")).StatusCode);

        // A fresh snapshot can be saved again; unpublished rotas can't.
        (await admin.PostAsync($"/api/admin/runs/{run.Run.Id}/report/archive", null)).EnsureSuccessStatusCode();
        Assert.Equal(2, (await Read<ReportArchiveDto>(await admin.GetAsync("/api/admin/reports"))).Reports.Count);
        (await admin.PostAsync($"/api/admin/runs/{run.Run.Id}/unpublish", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/admin/runs/{run.Run.Id}/report/archive", null)).StatusCode);
        Assert.All((await Read<ReportArchiveDto>(await admin.GetAsync("/api/admin/reports"))).Reports, r => Assert.False(r.RunIsPublished));
    }
}
