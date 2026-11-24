using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Rota.Api.Data;

namespace Rota.Api.Tests;

/// <summary>The on-call case log: one hospital reads it, the recorder and admins change it, patient data stays out of logs.</summary>
public class CaseTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static async Task<T> Read<T>(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.ReasonPhrase}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<T>(ApiFixture.Json))!;
    }

    private static CaseInput Case(string date, string start, string finish, string name = "ALI BIN ABU", string idNo = "900101-09-1234",
        Guid? officer = null, string? dsa = "Kak Ros") =>
        new(DateOnly.Parse(date), TimeOnly.Parse(start), TimeOnly.Parse(finish), null, name, idNo, "male", "34", "MALAY", "ED", "OMFS",
            "Fractured mandible", "IMF", "Review in clinic", officer, "Dr HO Tagger", dsa, "called and attended", false);

    [Fact]
    public async Task Officers_record_cases_everyone_in_the_hospital_reads_them_and_only_the_recorder_or_admins_change_them()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        await ApiFixture.AddPeopleAsync(admin, "Dr One\nDr Two");
        var people = await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"));
        var one = await ApiFixture.OfficerClientAsync(app, admin, people[0].Id);
        var two = await ApiFixture.OfficerClientAsync(app, admin, people[1].Id);

        // 02:00 on the 6th belongs to the 5th's on-call shift; 23:00 -> 01:00 runs past midnight.
        var night = await Read<IdDto>(await one.PostAsJsonAsync("/api/cases", Case("2026-10-06", "02:00", "03:30"), ApiFixture.Json));
        await Read<IdDto>(await one.PostAsJsonAsync("/api/cases", Case("2026-10-05", "23:00", "01:00", "SITI", "A1234567"), ApiFixture.Json));
        Assert.Equal(HttpStatusCode.BadRequest, (await one.PostAsJsonAsync("/api/cases",
            Case("2026-10-05", "20:00", "21:00", name: " "), ApiFixture.Json)).StatusCode);
        // The DSA on call is always named.
        Assert.Equal(HttpStatusCode.BadRequest, (await one.PostAsJsonAsync("/api/cases",
            Case("2026-10-05", "20:00", "21:00", dsa: null), ApiFixture.Json)).StatusCode);

        var res = await two.GetAsync("/api/cases?month=2026-10");
        Assert.Equal("no-store", res.Headers.CacheControl?.ToString());
        var log = await Read<List<CaseDto>>(res);
        Assert.Equal(2, log.Count);
        Assert.All(log, c => Assert.Equal(new DateOnly(2026, 10, 5), c.OnCallDate));
        var first = log.Single(c => c.Id == night.Id);
        Assert.Equal(("ALI BIN ABU", "900101091234", "MALE", 90, false), (first.PatientName, first.PatientIdNo, first.Gender, first.Minutes, first.CanEdit));
        // With no rota, the officer in charge defaults to whoever typed it in; taggers and the DSA are separate.
        Assert.Equal(("Dr One", "DR HO TAGGER", "KAK ROS", "CALLED AND ATTENDED"), (first.Officer, first.Taggers, first.DsaName, first.DsaStatus));
        Assert.Equal(120, log.Single(c => c.Id != night.Id).Minutes);

        // Someone else's case: read-only for another officer, editable by an admin.
        Assert.Equal(HttpStatusCode.Forbidden, (await two.PutAsJsonAsync($"/api/cases/{night.Id}", Case("2026-10-06", "02:00", "04:00"), ApiFixture.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await two.DeleteAsync($"/api/cases/{night.Id}")).StatusCode);
        (await admin.PutAsJsonAsync($"/api/cases/{night.Id}", Case("2026-10-06", "02:00", "04:00"), ApiFixture.Json)).EnsureSuccessStatusCode();
        (await one.DeleteAsync($"/api/cases/{night.Id}")).EnsureSuccessStatusCode();
        Assert.Single(await Read<List<CaseDto>>(await two.GetAsync("/api/cases?month=2026-10")));

        // Recorded for another officer in charge: it counts for them, and they can change it.
        var forTwo = await Read<IdDto>(await one.PostAsJsonAsync("/api/cases",
            Case("2026-10-07", "20:00", "21:00", "AH KOW", "B9", officer: people[1].Id), ApiFixture.Json));
        var mine = (await Read<List<CaseDto>>(await two.GetAsync($"/api/cases?month=2026-10&personId={people[1].Id}"))).Single();
        Assert.Equal((forTwo.Id, "Dr Two", "Dr One", true), (mine.Id, mine.Officer, mine.RecordedBy, mine.CanEdit));
        (await one.DeleteAsync($"/api/cases/{forTwo.Id}")).EnsureSuccessStatusCode();

        // The download matches the list.
        var xlsx = await two.GetAsync("/api/cases/export.xlsx?month=2026-10");
        xlsx.EnsureSuccessStatusCode();
        Assert.Equal("no-store", xlsx.Headers.CacheControl?.ToString());
        using (var wb = new XLWorkbook(await xlsx.Content.ReadAsStreamAsync()))
        {
            var ws = wb.Worksheet(1);
            Assert.Equal("SITI", ws.Cell(2, 6).GetString());
            Assert.Equal("A1234567", ws.Cell(2, 7).GetString());
            Assert.True(ws.Cell(3, 1).IsEmpty());
        }

        // Another hospital sees nothing and can't reach the case by id.
        var kedah = await Read<Guid>(await admin.PostAsJsonAsync("/api/admin/states", new UpsertStateRequest("Kedah"), ApiFixture.Json));
        var district = await Read<Guid>(await admin.PostAsJsonAsync("/api/admin/districts", new UpsertDistrictRequest("Kota Setar", kedah), ApiFixture.Json));
        var hsb = await Read<Guid>(await admin.PostAsJsonAsync("/api/admin/facilities", new UpsertFacilityRequest("Hospital B", "HB", district), ApiFixture.Json));
        var remaining = log.Single(c => c.Id != night.Id);
        await Read<MeDto>(await admin.PostAsJsonAsync("/api/auth/facility", new SetFacilityRequest(hsb)));
        Assert.Empty(await Read<List<CaseDto>>(await admin.GetAsync("/api/cases?month=2026-10")));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/cases/{remaining.Id}")).StatusCode);

        // Every look and change is in the audit log, never the patient's details.
        await Read<MeDto>(await admin.PostAsJsonAsync("/api/auth/facility", new SetFacilityRequest(null)));
        var audit = await admin.GetStringAsync("/api/admin/audit?entity=case&pageSize=200");
        foreach (var action in new[] { "case.create", "case.update", "case.delete", "case.view-list", "case.download" })
            Assert.Contains(action, audit);
        Assert.DoesNotContain("ALI BIN ABU", audit);
        Assert.DoesNotContain("SITI", audit);
        Assert.DoesNotContain("900101091234", audit);
    }

    [Fact]
    public async Task The_google_sheet_imports_with_officers_matched_by_name_and_duplicates_skipped()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        await ApiFixture.AddPeopleAsync(admin, "Dr Aminah binti Ali\nDr Wong");
        var wong = (await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"))).Single(p => p.Name == "Dr Wong");

        const string csv = """
            Timestamp,Date,TIME START,TIME FINISH,PATIENT NAME (CAPITAL LETTER),NO MYCARD/KID/PASSPORT/REG NO (NO DASH(-) FOR MYCARD),GENDER,AGE,RACE,SOURCE OF REFERRAL,DEPARTMENT,DIAGNOSIS,MANAGEMENT,PLAN, OFFICER & TAGGERS INCHARGE,DSA ONCALL
            05/10/2026 23:59:00,5/10/2026,10:30:00 PM,11:45:00 PM,ALI,900101091234,MALE,34,MALAY,ED,OMFS,Abscess,I&D,TCA,DR. AMINAH BINTI ALI / TAGGER,CALLED AND ATTENDED
            06/10/2026 03:00:00,6/10/2026,1:00:00 AM,2:00:00 AM,SITI,A123,FEMALE,5,CHINESE,WARD,PAEDS,Trauma,Suture,,someone else,NOT BEEN CALLED
            """;
        var check = await Read<CaseImportResultDto>(await admin.PostAsJsonAsync("/api/cases/import",
            new CaseImportRequest(csv, Commit: true, DefaultPersonId: null), ApiFixture.Json));
        Assert.False(check.Committed);
        Assert.Equal("Dr Aminah binti Ali", check.Rows[0].Officer);
        Assert.Contains("No officer on the rota matches", Assert.Single(check.Rows[1].Errors).Message);

        var done = await Read<CaseImportResultDto>(await admin.PostAsJsonAsync("/api/cases/import",
            new CaseImportRequest(csv, Commit: true, DefaultPersonId: wong.Id), ApiFixture.Json));
        Assert.Equal((true, 2, 0), (done.Committed, done.Added, done.Duplicates));
        var log = await Read<List<CaseDto>>(await admin.GetAsync("/api/cases?month=2026-10"));
        Assert.Equal([(new DateOnly(2026, 10, 5), "Dr Aminah binti Ali", 75), (new DateOnly(2026, 10, 5), "Dr Wong", 60)],
            log.OrderBy(c => c.Officer).Select(c => (c.OnCallDate, c.Officer, c.Minutes)));
        var aminahCase = log.Single(c => c.Officer == "Dr Aminah binti Ali");
        Assert.Equal(("DR. AMINAH BINTI ALI / TAGGER", null, "CALLED AND ATTENDED"), (aminahCase.Taggers, aminahCase.DsaName, aminahCase.DsaStatus));

        var again = await Read<CaseImportResultDto>(await admin.PostAsJsonAsync("/api/cases/import",
            new CaseImportRequest(csv, Commit: true, DefaultPersonId: wong.Id), ApiFixture.Json));
        Assert.Equal((0, 2), (again.Added, again.Duplicates));

        // Officers can't import.
        var officer = await ApiFixture.OfficerClientAsync(app, admin, wong.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.PostAsJsonAsync("/api/cases/import",
            new CaseImportRequest(csv, false, null), ApiFixture.Json)).StatusCode);
    }

    private sealed record IdDto(Guid Id);
}
