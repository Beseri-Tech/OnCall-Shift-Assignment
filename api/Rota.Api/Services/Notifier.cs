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
        Published = "published", PeriodOpen = "period-open", Points = "points";

    private readonly RotaDbContext _db;
    private readonly Mailer _mailer;
    private readonly IHttpContextAccessor _http;

    public Notifier(RotaDbContext db, Mailer mailer, IHttpContextAccessor http) => (_db, _mailer, _http) = (db, mailer, http);

    /// <summary>Officers without an (enabled) account are skipped.</summary>
    public async Task ToPeopleAsync(IEnumerable<Guid> personIds, string kind, string title, string? body, string? link, bool email,
        CancellationToken ct)
    {
        var ids = personIds.Distinct().ToList();
        if (ids.Count == 0) return;
        var accounts = await _db.Accounts.Where(a => a.Enabled && a.PersonId != null && ids.Contains(a.PersonId.Value)).ToListAsync(ct);
        Add(accounts, kind, title, body, link, email);
    }

    public async Task ToOnCallOfficersAsync(string kind, string title, string? body, string? link, bool email, CancellationToken ct)
    {
        var accounts = await _db.Accounts
            .Where(a => a.Enabled && a.Person != null && a.Person.Status == OfficerStatus.OnCall).ToListAsync(ct);
        Add(accounts, kind, title, body, link, email);
    }

    public async Task ToAdminsAsync(string kind, string title, string? body, string? link, CancellationToken ct)
    {
        var accounts = await _db.Accounts.Where(a => a.Enabled && a.Role != AccountRole.Officer).ToListAsync(ct);
        Add(accounts, kind, title, body, link, email: false);
    }

    private void Add(List<Account> accounts, string kind, string title, string? body, string? link, bool email)
    {
        foreach (var a in accounts)
        {
            _db.Notifications.Add(new Notification { AccountId = a.Id, Kind = kind, Title = title, Body = body, Link = link });
            if (email && _mailer.IsConfigured)
                _mailer.Enqueue(_db, a.Email, title, $"{body}\n\nOpen On-Call Rota: {BaseUrl()}{link ?? "/"}".TrimStart());
        }
    }

    private string BaseUrl() => _http.HttpContext is { } http ? _mailer.BaseUrl(http) : "";
}
