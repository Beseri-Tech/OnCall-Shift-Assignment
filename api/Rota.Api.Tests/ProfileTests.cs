using System.Net;
using System.Net.Http.Json;
using Rota.Api.Data;

namespace Rota.Api.Tests;

/// <summary>The officer's ETAP profile: theirs alone, verified with their password, unverified again by any change.</summary>
public class ProfileTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static async Task<T> Read<T>(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.ReasonPhrase}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<T>(ApiFixture.Json))!;
    }

    private static ProfileInput Full(string ic = "900101-09-1234") => new("Aminah binti Ali", ic, "Pegawai Pergigian", "UD10", null,
        "Klinik Pergigian Kangar", "No 1, Jalan Kangar, 01000 Kangar, Perlis", 12.5m, "123456", "04-9761234", "012-3456789",
        "1234 5678 9012", "Maybank Kangar");

    [Fact]
    public async Task An_officer_verifies_their_own_profile_and_any_change_unverifies_it()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        await ApiFixture.AddPeopleAsync(admin, "Dr Aminah");
        var person = (await Read<List<AdminPersonDto>>(await admin.GetAsync("/api/admin/people"))).Single();
        var officer = await ApiFixture.OfficerClientAsync(app, admin, person.Id);

        var empty = await Read<ProfileDto>(await officer.GetAsync("/api/profile"));
        Assert.False(empty.Verified);
        Assert.Contains("icNo", empty.Missing);
        Assert.Equal(HttpStatusCode.BadRequest, (await officer.PostAsJsonAsync("/api/profile/verify",
            new VerifyProfileRequest(ApiFixture.OfficerPasswordText, true))).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await officer.PutAsJsonAsync("/api/profile", Full(ic: "12-34"), ApiFixture.Json)).StatusCode);
        var saved = await Read<ProfileDto>(await officer.PutAsJsonAsync("/api/profile", Full(), ApiFixture.Json));
        Assert.Equal(("AMINAH BINTI ALI", "900101091234", "012-3456789", false), (saved.FullName, saved.IcNo, saved.NoTelefonBimbit, saved.Verified));
        Assert.Empty(saved.Missing);

        // Verifying needs the box ticked and the officer's own password.
        Assert.Equal(HttpStatusCode.BadRequest, (await officer.PostAsJsonAsync("/api/profile/verify",
            new VerifyProfileRequest(ApiFixture.OfficerPasswordText, false))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await officer.PostAsJsonAsync("/api/profile/verify",
            new VerifyProfileRequest("wrong-password", true))).StatusCode);
        Assert.True((await Read<ProfileDto>(await officer.PostAsJsonAsync("/api/profile/verify",
            new VerifyProfileRequest(ApiFixture.OfficerPasswordText, true)))).Verified);

        // Any change - their own edit, or the admin changing their phone - needs a new verification.
        Assert.False((await Read<ProfileDto>(await officer.PutAsJsonAsync("/api/profile", Full() with { Gred = "UD12" }, ApiFixture.Json))).Verified);
        await Read<ProfileDto>(await officer.PostAsJsonAsync("/api/profile/verify", new VerifyProfileRequest(ApiFixture.OfficerPasswordText, true)));
        (await admin.PutAsJsonAsync($"/api/admin/people/{person.Id}",
            new UpsertPersonRequest(person.Name, person.Code, Email: person.Email, Phone: "019-9999999"), ApiFixture.Json)).EnsureSuccessStatusCode();
        Assert.False((await Read<ProfileDto>(await officer.GetAsync("/api/profile"))).Verified);

        // Nobody else reaches it, and its values never reach the audit log.
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/profile")).StatusCode);   // a master: not on the rota
        Assert.Equal("no-store", (await officer.GetAsync("/api/profile")).Headers.CacheControl?.ToString());
        await Read<MeDto>(await admin.PostAsJsonAsync("/api/auth/facility", new SetFacilityRequest(null)));
        var audit = await admin.GetStringAsync("/api/admin/audit?entity=profile&pageSize=200");
        Assert.Contains("profile.verify", audit);
        Assert.DoesNotContain("900101091234", audit);
        Assert.DoesNotContain("1234 5678 9012", audit);
    }
}
