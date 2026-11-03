using System.Net;
using System.Net.Http.Json;
using Rota.Api.Data;

namespace Rota.Api.Tests;

/// <summary>Any signed-in user sends feedback; admins list, filter and resolve it, and both land in the audit log.</summary>
public class FeedbackTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static async Task<T> Read<T>(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            Assert.Fail($"{(int)res.StatusCode} {res.ReasonPhrase}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<T>(ApiFixture.Json))!;
    }

    [Fact]
    public async Task Officers_send_feedback_and_admins_resolve_it()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);
        var created = await Read<SavePersonResult>(await admin.PostAsJsonAsync("/api/admin/people",
            new UpsertPersonRequest("Dr Aminah", null), ApiFixture.Json));
        var officer = await ApiFixture.OfficerClientAsync(app, admin, created.Id);

        (await officer.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Bug, "  The rota page is blank on my phone.  ", "/rota"), ApiFixture.Json))
            .EnsureSuccessStatusCode();
        (await officer.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Suggestion, "Show my next shift on the home page.", null), ApiFixture.Json))
            .EnsureSuccessStatusCode();

        // Officers can't read the list.
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.GetAsync("/api/admin/feedback")).StatusCode);

        var all = await Read<FeedbackPageDto>(await admin.GetAsync("/api/admin/feedback"));
        Assert.Equal((2, 2), (all.Total, all.Open));
        var bug = all.Items.Single(i => i.Category == FeedbackCategory.Bug);
        Assert.Equal(("The rota page is blank on my phone.", "/rota", "Dr Aminah"), (bug.Message, bug.Page, bug.SenderName));
        Assert.Null(bug.ResolvedAt);

        // Admins get a bell notification for each.
        var bell = await Read<NotificationsDto>(await admin.GetAsync("/api/notifications"));
        Assert.Equal(2, bell.Items.Count(n => n.Kind == "feedback"));

        (await admin.PostAsync($"/api/admin/feedback/{bug.Id}/resolve", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/admin/feedback/{bug.Id}/resolve", null)).EnsureSuccessStatusCode();   // no-op, not logged twice

        var open = await Read<FeedbackPageDto>(await admin.GetAsync("/api/admin/feedback?status=open"));
        Assert.Equal(FeedbackCategory.Suggestion, Assert.Single(open.Items).Category);
        Assert.Equal(1, open.Open);
        var resolved = await Read<FeedbackPageDto>(await admin.GetAsync("/api/admin/feedback?status=resolved"));
        Assert.Equal(ApiFixture.AdminEmail, Assert.Single(resolved.Items).ResolvedByEmail);
        var bugs = await Read<FeedbackPageDto>(await admin.GetAsync("/api/admin/feedback?category=Bug"));
        Assert.Equal(bug.Id, Assert.Single(bugs.Items).Id);

        (await admin.PostAsync($"/api/admin/feedback/{bug.Id}/reopen", null)).EnsureSuccessStatusCode();
        Assert.Equal(2, (await Read<FeedbackPageDto>(await admin.GetAsync("/api/admin/feedback?status=open"))).Total);

        var log = await Read<AuditPageDto>(await admin.GetAsync("/api/admin/audit?entity=feedback"));
        Assert.Equal(["feedback.reopen", "feedback.resolve", "feedback.create", "feedback.create"], log.Items.Select(i => i.Action));
        Assert.Equal("Resolved", log.Items[1].After!.Value.GetProperty("status").GetString());
        Assert.Equal("Dr Aminah", log.Items[2].PersonName);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync("/api/admin/feedback/999999/resolve", null)).StatusCode);
    }

    [Fact]
    public async Task Feedback_needs_a_message_and_a_sign_in()
    {
        await using var app = fixture.CreateApp();
        var admin = await ApiFixture.AdminClientAsync(app);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Other, "   ", null), ApiFixture.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Other, new string('x', 2001), null), ApiFixture.Json)).StatusCode);

        // Supervisors (no officer record) can send feedback too.
        (await admin.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Other, "Thanks!", null), ApiFixture.Json)).EnsureSuccessStatusCode();

        var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Other, "Hello", null), ApiFixture.Json)).StatusCode);
    }
}
