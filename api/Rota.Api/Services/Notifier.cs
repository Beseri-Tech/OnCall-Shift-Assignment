using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;

namespace Rota.Api.Services;

/// <summary>
/// Adds in-app notifications (and, where marked, outbox emails) to the current DbContext, so they are saved
/// together with the change they describe: a failed request never notifies about something that didn't happen.
/// </summary>
public sealed class Notifier
{
    public const string Review = "review", SwapRequest = "swap-request", SwapAccepted = "swap-accepted",
        SwapDeclined = "swap-declined", SwapCancelled = "swap-cancelled", ShiftChanged = "shift-changed",
        Published = "published", PeriodOpen = "period-open", Points = "points",
        Feedback = "feedback", ConfirmedLeave = "confirmed-leave", ShiftReport = "shift-report";

    private readonly RotaDbContext _db;
    private readonly Mailer _mailer;
    private readonly IHttpContextAccessor _http;

    public Notifier(RotaDbContext db, Mailer mailer, IHttpContextAccessor http) => (_db, _mailer, _http) = (db, mailer, http);

    /// <summary>People who can't sign in (no email, never invited, disabled) are skipped.</summary>
    public async Task ToPeopleAsync(IEnumerable<Guid> personIds, string kind, string title, string? body, string? link, bool email,
        CancellationToken ct)
    {
        var ids = personIds.Distinct().ToList();
        if (ids.Count == 0) return;
        var accounts = await CanSignIn(_db.People).Where(a => ids.Contains(a.Id)).ToListAsync(ct);
        Add(accounts, kind, title, body, link, email);
    }

    public async Task ToOnCallOfficersAsync(string kind, string title, string? body, string? link, bool email, CancellationToken ct,
        IReadOnlyCollection<Guid>? except = null)
    {
        var accounts = await CanSignIn(_db.Officers).Where(a => a.Status == OfficerStatus.OnCall).ToListAsync(ct);
        if (except is { Count: > 0 }) accounts = accounts.Where(a => !except.Contains(a.Id)).ToList();
        Add(accounts, kind, title, body, link, email);
    }

    public async Task ToAdminsAsync(string kind, string title, string? body, string? link, CancellationToken ct)
    {
        // The query filter keeps this to the current hospital's admins (masters belong to none).
        var accounts = await CanSignIn(_db.People).Where(a => a.Role != AccountRole.Officer).ToListAsync(ct);
        Add(accounts, kind, title, body, link, email: false);
    }

    public async Task ToMastersAsync(string kind, string title, string? body, string? link, CancellationToken ct)
    {
        var accounts = await CanSignIn(_db.People.IgnoreQueryFilters()).Where(a => a.Role == AccountRole.Master).ToListAsync(ct);
        Add(accounts, kind, title, body, link, email: false);
    }

    private static IQueryable<Person> CanSignIn(IQueryable<Person> q) =>
        q.Where(a => a.Enabled && a.Email != null && a.PasswordHash != null && a.Status != OfficerStatus.Left);

    private void Add(List<Person> accounts, string kind, string title, string? body, string? link, bool email)
    {
        foreach (var a in accounts)
        {
            _db.Notifications.Add(new Notification { AccountId = a.Id, Kind = kind, Title = title, Body = body, Link = link });
            if (email && _mailer.CanSend(_db) && _http.HttpContext is { } http)
                _mailer.Enqueue(_db, a.Email!, new EmailContent(
                    Subject: title,
                    Heading: title,
                    Paragraphs: (body ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    ButtonText: "Open On-Call Rota",
                    ButtonUrl: $"{_mailer.BaseUrl(http)}{link ?? "/"}"), http);
        }
    }
}
