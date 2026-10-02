using System.Net;
using System.Net.Http.Json;
using Rota.Api.Data;

namespace Rota.Api.Tests;

/// <summary>Admin changes land in the audit log with who, what and the values before and after, and can be filtered.</summary>
public class AuditTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static async Task<T> Read<T>(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.ReasonPhrase}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<T>(ApiFixture.Json))!;
    }

    [Fact]
    public async Task Officer_edits_are_logged_with_before_and_after_values()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);

        var created = await Read<SavePersonResult>(await admin.PostAsJsonAsync("/api/admin/people",
            new UpsertPersonRequest("Dr Aminah", null), ApiFixture.Json));
        (await admin.PutAsJsonAsync($"/api/admin/people/{created.Id}", new UpsertPersonRequest("Dr Aminah", "D001",
            OfficerStatus.Excluded, StatusReason: "CUTI BERSALIN"), ApiFixture.Json)).EnsureSuccessStatusCode();

        var log = await Read<AuditPageDto>(await admin.GetAsync($"/api/admin/audit?personId={created.Id}"));
        Assert.Equal(["person.update", "person.create"], log.Items.Select(i => i.Action));

        var update = log.Items[0];
        Assert.Equal(("person", created.Id.ToString(), "Dr Aminah"), (update.Entity, update.EntityId, update.PersonName));
        Assert.Equal(ApiFixture.AdminEmail, update.ActorEmail);
        Assert.Equal("OnCall", update.Before!.Value.GetProperty("status").GetString());
        Assert.Equal("Excluded", update.After!.Value.GetProperty("status").GetString());
        Assert.Equal("CUTI BERSALIN", update.After!.Value.GetProperty("statusReason").GetString());

        var create = log.Items[1];
        Assert.Null(create.Before);
        Assert.Equal("Dr Aminah", create.After!.Value.GetProperty("name").GetString());
    }

    [Fact]
    public async Task The_log_filters_by_kind_action_actor_and_date()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var kangar = (await Read<List<StateDto>>(await admin.GetAsync("/api/admin/states"))).Single().Districts.Single(d => d.Name == "Kangar");
        var clinicId = await Read<Guid>(await admin.PostAsJsonAsync("/api/admin/clinics", new UpsertClinicRequest("KP Beseri", kangar.Id)));
        (await admin.PutAsJsonAsync($"/api/admin/clinics/{clinicId}", new UpsertClinicRequest("KP Beseri Baru", kangar.Id)))
            .EnsureSuccessStatusCode();
        var period = await Read<PeriodDto>(await admin.PostAsJsonAsync("/api/admin/periods",
            new UpsertPeriodRequest("Oct 2026", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null), ApiFixture.Json));
        (await admin.PutAsJsonAsync($"/api/admin/periods/{period.Id}/days",
            new PeriodDayDto(new DateOnly(2026, 10, 20), PeriodDayKind.Holiday, "Deepavali"), ApiFixture.Json)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.DeleteAsync($"/api/admin/periods/{period.Id}/days/Holiday/2026-10-21")).StatusCode);
        (await admin.DeleteAsync($"/api/admin/periods/{period.Id}/days/Holiday/2026-10-20")).EnsureSuccessStatusCode();

        var clinics = await Read<AuditPageDto>(await admin.GetAsync("/api/admin/audit?entity=clinic"));
        Assert.Equal(["clinic.update", "clinic.create"], clinics.Items.Select(i => i.Action));
        Assert.Equal("KP Beseri", clinics.Items[0].Before!.Value.GetProperty("name").GetString());
        Assert.Equal("KP Beseri Baru", clinics.Items[0].After!.Value.GetProperty("name").GetString());
        Assert.Equal("Kangar", clinics.Items[0].After!.Value.GetProperty("district").GetString());

        // Deleting a day that isn't there changes nothing, so it isn't logged.
        var days = await Read<AuditPageDto>(await admin.GetAsync("/api/admin/audit?action=period-day.delete"));
        var deleted = Assert.Single(days.Items);
        Assert.Equal("Deepavali", deleted.Before!.Value.GetProperty("name").GetString());
        Assert.Null(deleted.After);
        Assert.Equal("Oct 2026", deleted.Detail!.Value.GetProperty("period").GetString());

        var filters = await Read<AuditFiltersDto>(await admin.GetAsync("/api/admin/audit/filters"));
        var me = Assert.Single(filters.Actors);
        Assert.Equal(ApiFixture.AdminEmail, me.Email);
        Assert.Contains("clinic", filters.Entities);
        Assert.Contains("period-day.upsert", filters.Actions);

        var mine = await Read<AuditPageDto>(await admin.GetAsync($"/api/admin/audit?accountId={me.AccountId}&pageSize=2"));
        Assert.Equal((5, 2), (mine.Total, mine.Items.Count));
        Assert.Equal(0, (await Read<AuditPageDto>(await admin.GetAsync($"/api/admin/audit?accountId={Guid.NewGuid()}"))).Total);
        Assert.Equal(0, (await Read<AuditPageDto>(await admin.GetAsync("/api/admin/audit?to=2020-01-01"))).Total);
        Assert.Equal(5, (await Read<AuditPageDto>(await admin.GetAsync("/api/admin/audit?from=2020-01-01"))).Total);
    }

    [Fact]
    public async Task Officers_cannot_read_the_log()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var created = await Read<SavePersonResult>(await admin.PostAsJsonAsync("/api/admin/people",
            new UpsertPersonRequest("Dr Badrul", null), ApiFixture.Json));
        var officer = await ApiFixture.OfficerClientAsync(app, admin, created.Id);

        Assert.Equal(HttpStatusCode.Forbidden, (await officer.GetAsync("/api/admin/audit")).StatusCode);
    }
}
