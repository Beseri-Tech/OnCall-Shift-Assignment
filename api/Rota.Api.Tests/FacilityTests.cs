using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Rota.Api.Data;

namespace Rota.Api.Tests;

/// <summary>Each hospital sees only its own officers, periods, rotas and holidays; masters see and switch between all.</summary>
public class FacilityTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static async Task<T> Read<T>(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.ReasonPhrase}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<T>(ApiFixture.Json))!;
    }

    [Fact]
    public async Task Hospitals_are_kept_apart_and_masters_switch_between_them()
    {
        await using var app = fixture.CreateApp();
        var master = await ApiFixture.AdminClientAsync(app);   // working in HTF

        // HTF (Perlis): an officer, a period and a Perlis holiday.
        var htfPerson = await Read<SavePersonResult>(await master.PostAsJsonAsync("/api/admin/people",
            new UpsertPersonRequest("Dr Same Name", null), ApiFixture.Json));
        var htfPeriod = await Read<PeriodDto>(await master.PostAsJsonAsync("/api/admin/periods",
            new UpsertPeriodRequest("HTF Jan", new(2027, 1, 1), new(2027, 1, 31), null), ApiFixture.Json));
        (await master.PutAsJsonAsync("/api/admin/holidays", new UpsertHolidayRequest(new(2027, 1, 20), "Perlis only"), ApiFixture.Json))
            .EnsureSuccessStatusCode();

        // A second hospital in another state.
        var kedah = await Read<Guid>(await master.PostAsJsonAsync("/api/admin/states", new UpsertStateRequest("Kedah"), ApiFixture.Json));
        var kotaSetar = await Read<Guid>(await master.PostAsJsonAsync("/api/admin/districts",
            new UpsertDistrictRequest("Kota Setar", kedah), ApiFixture.Json));
        var hsb = await Read<Guid>(await master.PostAsJsonAsync("/api/admin/facilities",
            new UpsertFacilityRequest("Hospital Sultanah Bahiyah", "hsb", kotaSetar), ApiFixture.Json));

        var me = await Read<MeDto>(await master.PostAsJsonAsync("/api/auth/facility", new SetFacilityRequest(hsb)));
        Assert.Equal(("Hospital Sultanah Bahiyah", "HSB"), (me.Facility!.Name, me.Facility.Code));
        Assert.Empty(await Read<List<AdminPersonDto>>(await master.GetAsync("/api/admin/people")));
        Assert.Empty(await Read<List<HolidayDto>>(await master.GetAsync("/api/admin/holidays")));

        // Same officer name is fine in another hospital.
        var hsbPerson = await Read<SavePersonResult>(await master.PostAsJsonAsync("/api/admin/people",
            new UpsertPersonRequest("Dr Same Name", null), ApiFixture.Json));
        await Read<PeriodDto>(await master.PostAsJsonAsync("/api/admin/periods",
            new UpsertPeriodRequest("HSB Jan", new(2027, 1, 1), new(2027, 1, 31), null), ApiFixture.Json));

        // HSB's supervisor.
        var invite = await Read<InviteResult>(await master.PostAsJsonAsync("/api/admin/accounts",
            new InviteRequest("sup@hsb.test", AccountRole.Supervisor, null), ApiFixture.Json));
        var sup = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        (await sup.PostAsJsonAsync("/api/auth/login", new { email = "sup@hsb.test", password = invite.TempPassword })).EnsureSuccessStatusCode();
        (await sup.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(invite.TempPassword!, "supervisor-pass-1")))
            .EnsureSuccessStatusCode();

        Assert.Equal(hsbPerson.Id, Assert.Single(await Read<List<AdminPersonDto>>(await sup.GetAsync("/api/admin/people"))).Id);
        Assert.Equal("HSB Jan", Assert.Single(await Read<List<PeriodDto>>(await sup.GetAsync("/api/periods"))).Name);
        Assert.Empty(await Read<List<HolidayDto>>(await sup.GetAsync("/api/admin/holidays")));
        var supAccounts = await Read<List<AccountDto>>(await sup.GetAsync("/api/admin/accounts"));
        Assert.Equal("sup@hsb.test", Assert.Single(supAccounts).Email);

        // HTF's things are out of reach, even by id.
        Assert.Equal(HttpStatusCode.NotFound, (await sup.PutAsJsonAsync($"/api/admin/people/{htfPerson.Id}",
            new UpsertPersonRequest("Hijacked", null), ApiFixture.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await sup.DeleteAsync($"/api/admin/periods/{htfPeriod.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await sup.PostAsJsonAsync($"/api/admin/periods/{htfPeriod.Id}/generate", new { })).StatusCode);

        // Supervisors can't make masters or hospitals.
        Assert.Equal(HttpStatusCode.BadRequest, (await sup.PostAsJsonAsync("/api/admin/accounts",
            new InviteRequest("boss@hsb.test", AccountRole.Master, null), ApiFixture.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await sup.PostAsJsonAsync("/api/admin/facilities",
            new UpsertFacilityRequest("Rogue", "RG", kotaSetar), ApiFixture.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await sup.PostAsJsonAsync("/api/admin/states",
            new UpsertStateRequest("Rogue"), ApiFixture.Json)).StatusCode);

        // Feedback to the hospital reaches its admins, not the masters; feedback about the app reaches the masters.
        (await sup.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Other, "For HSB admins", null, ToFacility: true), ApiFixture.Json)).EnsureSuccessStatusCode();
        (await sup.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Bug, "For the developers", null), ApiFixture.Json)).EnsureSuccessStatusCode();
        Assert.Equal("For HSB admins", Assert.Single((await Read<FeedbackPageDto>(await sup.GetAsync("/api/admin/feedback"))).Items).Message);
        Assert.DoesNotContain((await Read<AuditPageDto>(await sup.GetAsync("/api/admin/audit"))).Items, e => e.PersonId == htfPerson.Id);
        var bell = await Read<NotificationsDto>(await master.GetAsync("/api/notifications"));
        Assert.Equal("New app feedback: Bug", Assert.Single(bell.Items, n => n.Kind == "feedback").Title);

        // Back in HTF the master sees HTF's officer only; with no hospital picked, hospital pages ask for one.
        await Read<MeDto>(await master.PostAsJsonAsync("/api/auth/facility", new SetFacilityRequest(Facility.HtfId)));
        Assert.Equal(htfPerson.Id, Assert.Single(await Read<List<AdminPersonDto>>(await master.GetAsync("/api/admin/people"))).Id);
        await Read<MeDto>(await master.PostAsJsonAsync("/api/auth/facility", new SetFacilityRequest(null)));
        Assert.Equal(HttpStatusCode.BadRequest, (await master.GetAsync("/api/admin/people")).StatusCode);
        Assert.Equal(2, (await Read<List<FacilityDto>>(await master.GetAsync("/api/facilities"))).Count);
        Assert.Single(await Read<List<FacilityDto>>(await sup.GetAsync("/api/facilities")));
    }
}
