using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Rota.Api.Data;
using Rota.Api.Endpoints;
using Rota.Api.Storage;

namespace Rota.Api.Tests;

/// <summary>ETAP: day types, the 4-hour rule on summed case time, rates, swap reports, surat arahan and the PDF.</summary>
public class EtapTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static async Task<T> Read<T>(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.ReasonPhrase}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<T>(ApiFixture.Json))!;
    }

    private static CaseInput Case(string date, string start, string finish, string name) =>
        new(DateOnly.Parse(date), TimeOnly.Parse(start), TimeOnly.Parse(finish), null, name, name + "-ID", "FEMALE", "30", "MALAY", "ED",
            "OMFS", "Abscess", "I&D", null, null, null, "KAK ROS", "CALLED AND ATTENDED", false);

    private static ProfileInput Profile(DateOnly? specialist = null) => new("Aminah binti Ali", "900101091234", "Pegawai Pergigian", "UD10",
        specialist, "Jabatan Pergigian, Hospital Tuanku Fauziah", "No 1, Jalan Kangar, 01000 Kangar, Perlis", 12.5m, "123456", "04-9761234",
        "012-3456789", "1234567890", "Maybank Kangar");

    private sealed record Setup(WebApplicationFactory<Program> App, HttpClient Admin, HttpClient One, HttpClient Two, Guid OneId, Guid TwoId,
        DocumentTests.FakeStorage Storage);

    /// <summary>Two officers; a published October rota with Dr One on 5, 7, 8 and 10 Oct and Dr Two on every other day; 8 Oct a holiday.</summary>
    private async Task<Setup> SetupAsync()
    {
        var storage = new DocumentTests.FakeStorage();
        var app = fixture.CreateApp().WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<IDocumentStorage>(storage)));
        var admin = await ApiFixture.AdminClientAsync(app);
        (await admin.PutAsJsonAsync("/api/admin/holidays", new UpsertHolidayRequest(new DateOnly(2026, 10, 8), "Test holiday"), ApiFixture.Json))
            .EnsureSuccessStatusCode();
        await ApiFixture.AddPeopleAsync(admin, "Dr One\nDr Two");
        var people = await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"));
        Guid one = people.Single(p => p.Name == "Dr One").Id, two = people.Single(p => p.Name == "Dr Two").Id;
        var period = await Read<PeriodDto>(await admin.PostAsJsonAsync("/api/admin/periods",
            new UpsertPeriodRequest("Oct 2026", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null), ApiFixture.Json));
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RotaDbContext>();
            var run = new RotaRun { PeriodId = period.Id, IsPublished = true, PublishedAt = DateTimeOffset.UtcNow };
            int[] oneDays = [5, 7, 8, 10];
            for (int d = 1; d <= 31; d++)
                run.Assignments.Add(new ShiftAssignment { Date = new DateOnly(2026, 10, d), PersonId = oneDays.Contains(d) ? one : two });
            db.Runs.Add(run);
            await db.SaveChangesAsync();
        }
        return new(app, admin, await ApiFixture.OfficerClientAsync(app, admin, one), await ApiFixture.OfficerClientAsync(app, admin, two),
            one, two, storage);
    }

    [Fact]
    public async Task The_claim_adds_up_case_time_per_shift_and_prices_each_day()
    {
        var s = await SetupAsync();
        await using var _ = s.App;

        // 5 Oct (Mon): two patients of 2 h = 4 h -> over 4 h. 10 Oct (Sat): 3 h 59 min -> under. A 01:00 case belongs to the 5th.
        await Read<object>(await s.One.PostAsJsonAsync("/api/cases", Case("2026-10-05", "20:00", "22:00", "P ONE"), ApiFixture.Json));
        await Read<object>(await s.One.PostAsJsonAsync("/api/cases", Case("2026-10-06", "01:00", "03:00", "P TWO"), ApiFixture.Json));
        await Read<object>(await s.One.PostAsJsonAsync("/api/cases", Case("2026-10-10", "10:00", "13:59", "P THREE"), ApiFixture.Json));

        var empty = await Read<EtapDto>(await s.One.GetAsync("/api/etap?month=2026-10"));
        Assert.Empty(empty.Rows);
        Assert.Equal([5, 7, 8, 10], empty.RotaDates.Select(d => d.Day));
        var weekday = empty.UnclaimedRotaRows.Single(r => r.Date.Day == 5);
        var saturday = empty.UnclaimedRotaRows.Single(r => r.Date.Day == 10);
        Assert.Equal((new TimeOnly(17, 0), new DateOnly(2026, 10, 6), new TimeOnly(8, 0)), (weekday.FromTime, weekday.ToDate, weekday.ToTime));
        Assert.Equal((new TimeOnly(8, 0), new DateOnly(2026, 10, 11)), (saturday.FromTime, saturday.ToDate));

        // The officer also claims 6 Oct, which the rota gives to Dr Two (a swap made outside the app).
        var sixth = new EtapRowInput(new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 6), new TimeOnly(17, 0), new DateOnly(2026, 10, 7), new TimeOnly(8, 0));
        Assert.Equal(HttpStatusCode.BadRequest, (await s.One.PutAsJsonAsync("/api/etap?month=2026-10",
            new EtapSaveRequest([sixth with { Date = new DateOnly(2026, 11, 1) }]), ApiFixture.Json)).StatusCode);
        var claim = await Read<EtapDto>(await s.One.PutAsJsonAsync("/api/etap?month=2026-10",
            new EtapSaveRequest([.. empty.UnclaimedRotaRows, sixth]), ApiFixture.Json));

        Assert.Equal(["5:2a:Working:240", "6:3a:Working:0", "7:3a:Working:0", "8:3b:Holiday:0", "10:3b:Rest:239"],
            claim.Rows.Select(r => $"{r.Date.Day}:{r.Category}:{r.DayType}:{r.CaseMinutes}"));
        var notMine = claim.Rows.Single(r => r.Date.Day == 6);
        Assert.Equal((false, "Dr Two"), (notMine.InRota, notMine.RotaOfficer?.Name));
        Assert.Equal(900, claim.Rows[0].Minutes);
        Assert.Equal(["2a:1:180", "2b:0:0", "3a:2:220", "3b:2:240"], claim.Lines.Select(l => $"{l.Category}:{l.Days}:{l.Amount}"));
        Assert.Equal((5, 640), (claim.TotalDays, claim.TotalAmount));
        Assert.Equal(["P ONE", "P TWO"], claim.Patients.Select(p => p.Name));

        // The PDF needs a verified profile.
        Assert.Equal(HttpStatusCode.BadRequest, (await s.One.GetAsync("/api/etap/pdf?month=2026-10")).StatusCode);
        await Read<ProfileDto>(await s.One.PutAsJsonAsync("/api/profile", Profile(), ApiFixture.Json));
        await Read<ProfileDto>(await s.One.PostAsJsonAsync("/api/profile/verify", new VerifyProfileRequest(ApiFixture.OfficerPasswordText, true)));
        var pdf = await s.One.GetAsync("/api/etap/pdf?month=2026-10");
        pdf.EnsureSuccessStatusCode();
        Assert.Equal("no-store", pdf.Headers.CacheControl?.ToString());
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        if (Environment.GetEnvironmentVariable("ETAP_PDF_OUT") is { Length: > 0 } outPath) await File.WriteAllBytesAsync(outPath, bytes);

        // A specialist is paid the specialist rates.
        await Read<ProfileDto>(await s.One.PutAsJsonAsync("/api/profile", Profile(new DateOnly(2020, 1, 1)), ApiFixture.Json));
        var specialist = await Read<EtapDto>(await s.One.GetAsync("/api/etap?month=2026-10"));
        Assert.Equal((true, 200 + 2 * 135 + 2 * 150), (specialist.Specialist, specialist.TotalAmount));
        Assert.False(specialist.ProfileVerified);   // changed since verifying

        // Supervisors and masters have no ETAP.
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Admin.GetAsync("/api/etap?month=2026-10")).StatusCode);
    }

    [Fact]
    public async Task A_swap_made_outside_the_app_is_confirmed_by_the_colleague_and_fixes_the_rota()
    {
        var s = await SetupAsync();
        await using var _ = s.App;

        // Dr One did 6 Oct (Dr Two's) and Dr Two did 7 Oct (Dr One's).
        Assert.Equal(HttpStatusCode.BadRequest, (await s.One.PostAsJsonAsync("/api/etap/shift-reports",
            new ShiftReportRequest(new DateOnly(2026, 10, 6), s.OneId, null), ApiFixture.Json)).StatusCode);
        var report = await Read<IdDto>(await s.One.PostAsJsonAsync("/api/etap/shift-reports",
            new ShiftReportRequest(new DateOnly(2026, 10, 6), s.TwoId, new DateOnly(2026, 10, 7)), ApiFixture.Json));

        var forTwo = Assert.Single(await Read<List<ShiftReportDto>>(await s.Two.GetAsync("/api/etap/shift-reports")));
        Assert.True(forTwo.CanDecide);
        Assert.False(Assert.Single(await Read<List<ShiftReportDto>>(await s.One.GetAsync("/api/etap/shift-reports"))).CanDecide);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.One.PostAsync($"/api/etap/shift-reports/{report.Id}/confirm", null)).StatusCode);
        (await s.Two.PostAsync($"/api/etap/shift-reports/{report.Id}/confirm", null)).EnsureSuccessStatusCode();

        var claim = await Read<EtapDto>(await s.One.GetAsync("/api/etap?month=2026-10"));
        Assert.Equal([5, 6, 8, 10], claim.RotaDates.Select(d => d.Day));
        var tally = await Read<List<TallyRowDto>>(await s.Admin.GetAsync("/api/admin/tally"));
        Assert.Equal(4, tally.Single(t => t.PersonId == s.OneId).PublishedWeekday + tally.Single(t => t.PersonId == s.OneId).PublishedWeekendHoliday);
    }

    [Fact]
    public async Task The_claim_uses_the_latest_surat_arahan_effective_by_the_end_of_the_month()
    {
        var s = await SetupAsync();
        await using var _ = s.App;
        async Task<Guid> Upload(string name)
        {
            byte[] bytes = "%PDF-1.7 surat arahan"u8.ToArray();
            var ticket = await Read<UploadTicket>(await s.Admin.PostAsJsonAsync("/api/documents/upload-url",
                new UploadUrlRequest(DocumentKind.SuratArahan, name, "application/pdf", bytes.Length), ApiFixture.Json));
            s.Storage.Objects[ticket.UploadUrl["https://r2.test/up/".Length..]] = bytes;
            return (await Read<DocumentDto>(await s.Admin.PostAsync($"/api/documents/{ticket.DocumentId}/complete", null))).Id;
        }

        // Officers can't upload one.
        Assert.Equal(HttpStatusCode.Forbidden, (await s.One.PostAsJsonAsync("/api/documents/upload-url",
            new UploadUrlRequest(DocumentKind.SuratArahan, "x.pdf", "application/pdf", 20), ApiFixture.Json)).StatusCode);

        await Read<IdDto>(await s.Admin.PostAsJsonAsync("/api/admin/surat-arahan",
            new AddSuratArahanRequest(await Upload("jan.pdf"), new DateOnly(2026, 1, 1), "SA 1/2026"), ApiFixture.Json));
        await Read<IdDto>(await s.Admin.PostAsJsonAsync("/api/admin/surat-arahan",
            new AddSuratArahanRequest(await Upload("dec.pdf"), new DateOnly(2026, 12, 1), "SA 9/2026"), ApiFixture.Json));

        var october = await Read<EtapDto>(await s.One.GetAsync("/api/etap?month=2026-10"));
        Assert.Equal(("jan.pdf", "SA 1/2026"), (october.SuratArahan?.FileName, october.SuratArahan?.Reference));
        Assert.Equal("dec.pdf", (await Read<EtapDto>(await s.One.GetAsync("/api/etap?month=2026-12"))).SuratArahan?.FileName);
        // Every officer of the hospital can open it.
        (await s.One.GetAsync($"/api/documents/{october.SuratArahan!.DocumentId}/download-url")).EnsureSuccessStatusCode();
    }

    private sealed record IdDto(Guid Id);
}
