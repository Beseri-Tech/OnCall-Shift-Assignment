using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;
using Rota.Api.Services;
using Rota.Core.Leave;

namespace Rota.Api.Reports;

/// <summary>Collects what the report needs for one rota run: the rota itself, leave and preference facts, swaps and the audit trail.</summary>
public static class RotaReportBuilder
{
    /// <summary>One change of owner of a day (an admin override or an accepted swap), used to explain how a day got its final officer.</summary>
    private sealed record Event(DateTimeOffset At, string Kind, string By, string? Before, string? After);

    public static async Task<RotaReportData?> BuildAsync(RotaDbContext db, LeaveLimits limits, LocalClock clock, Guid runId,
        string generatedBy, CancellationToken ct)
    {
        var run = await db.Runs.AsNoTracking().Include(r => r.Period).Include(r => r.Assignments).FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run?.Period is not { } period) return null;

        var people = await db.People.AsNoTracking().Include(p => p.Clinic).ToDictionaryAsync(p => p.Id, ct);
        string Name(Guid? id) => id is { } i && people.TryGetValue(i, out var p) ? p.Name : "Unknown officer";
        var rules = await LeaveRules.LoadAsync(db, limits, period, ct);
        var holidays = await Endpoints.Mapping.HolidaysAsync(db, period.Id, ct);
        var peaks = await db.PeriodDays.AsNoTracking().Where(d => d.PeriodId == period.Id && d.Kind == PeriodDayKind.Peak)
            .ToDictionaryAsync(d => d.Date, d => d.Name, ct);
        var preferred = (await db.PreferredDates.AsNoTracking().Where(d => d.Date >= period.StartDate && d.Date <= period.EndDate).ToListAsync(ct))
            .GroupBy(d => d.PersonId).ToDictionary(g => g.Key, g => g.Select(d => d.Date).ToHashSet());
        var before = await Totals.ForPeopleAsync(db, excludePeriodId: period.Id, ct: ct);
        var excluded = await db.PeriodExclusions.AsNoTracking().Where(x => x.PeriodId == period.Id).Select(x => x.PersonId).ToListAsync(ct);

        var swaps = await db.SwapRequests.AsNoTracking().Where(s => s.RunId == runId).OrderBy(s => s.CreatedAt).ToListAsync(ct);
        var swapIds = swaps.Select(s => s.Id.ToString()).ToList();
        string runKey = runId.ToString();
        var logs = await db.ChangeLog.AsNoTracking()
            .Where(c => (c.Entity == "run" && c.EntityId == runKey) || (c.Entity == "swap" && c.EntityId != null && swapIds.Contains(c.EntityId)))
            .OrderBy(c => c.At).ToListAsync(ct);
        var accountIds = logs.Where(l => l.AccountId != null).Select(l => l.AccountId!.Value).Distinct().ToList();
        var accounts = await db.People.IgnoreQueryFilters().AsNoTracking().Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);
        string Who(Guid? accountId) => accountId is { } a && accounts.TryGetValue(a, out var acc) ? acc.Name : "System";

        // ---- how each day changed hands after the generator ----
        var events = new Dictionary<DateOnly, List<Event>>();
        void AddEvent(DateOnly date, Event e)
        {
            if (!events.TryGetValue(date, out var list)) events[date] = list = [];
            list.Add(e);
        }
        foreach (var l in logs.Where(l => l.Action == "run.override"))
            if (Str(l.Detail, "date") is { } ds && DateOnly.TryParse(ds, CultureInfo.InvariantCulture, out var date))
                AddEvent(date, new Event(l.At, "Admin override", Who(l.AccountId), Str(l.Before, "officer"), Str(l.After, "officer")));
        foreach (var s in swaps.Where(s => s.Status == SwapStatus.Accepted))
        {
            var at = s.RespondedAt ?? s.CreatedAt;
            AddEvent(s.FromDate, new Event(at, "Swap", Name(s.ToPersonId), Name(s.FromPersonId), Name(s.ToPersonId)));
            AddEvent(s.ToDate, new Event(at, "Swap", Name(s.FromPersonId), Name(s.ToPersonId), Name(s.FromPersonId)));
        }

        // ---- availability ----
        var onCall = rules.LeaveByPerson.Keys.ToList();
        var assignedPeople = run.Assignments.Where(a => a.PersonId != null).Select(a => a.PersonId!.Value).Distinct();
        var roster = onCall.Union(assignedPeople).ToList();
        List<string> Unavailable(DateOnly date) => roster.Where(id =>
                rules.LeaveByPerson.GetValueOrDefault(id)?.Contains(date) == true
                || BlockedDays.IsBlocked(rules.BlockedWeekdays.GetValueOrDefault(id), date)
                || rules.ClinicBlocked.GetValueOrDefault(id)?.Contains(date) == true)
            .Select(id => Name(id)).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

        var assigned = run.Assignments.Where(a => a.PersonId != null).ToList();
        var shiftsByPerson = assigned.GroupBy(a => a.PersonId!.Value).ToDictionary(g => g.Key, g => g.Select(a => a.Date).ToList());
        var holidaySet = holidays.Keys.ToHashSet();
        var peakSet = peaks.Keys.ToHashSet();
        var knownPrior = before.Where(kv => kv.Value.Known && onCall.Contains(kv.Key)).Select(kv => kv.Value.WeekendHoliday).ToList();
        double priorAverage = knownPrior.Count > 0 ? knownPrior.Average() : 0;

        // ---- day by day ----
        var days = new List<ReportDay>();
        foreach (var a in run.Assignments.OrderBy(a => a.Date))
        {
            string kind = holidays.TryGetValue(a.Date, out var hn) ? $"Public holiday: {hn}"
                : a.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? "Weekend"
                : peaks.TryGetValue(a.Date, out var pn) ? $"Peak day: {pn}"
                : "Weekday";
            var evs = events.GetValueOrDefault(a.Date)?.OrderBy(e => e.At).ToList() ?? [];
            string source = a.PersonId is null ? "Unassigned" : evs.Count > 0 ? evs[^1].Kind : "Generator";
            var off = Unavailable(a.Date);
            var reasons = new List<string>();

            if (evs.Count > 0)
            {
                var last = evs[^1];
                reasons.Add(last.Kind == "Swap"
                    ? $"Swapped on {Time(clock, last.At)}: {last.Before} and {last.After} traded dates in review, and both accepted."
                    : $"Admin override by {last.By} on {Time(clock, last.At)}: {last.Before ?? "nobody"} was replaced by {last.After ?? "nobody"}.");
            }

            if (a.PersonId is not { } pid)
            {
                reasons.Add($"Nobody could be assigned: all {roster.Count} officers were unavailable ({off.Count} on leave, blocked or clinic-blocked).");
            }
            else
            {
                var person = people[pid];
                bool wanted = preferred.GetValueOrDefault(pid)?.Contains(a.Date) == true;
                var others = preferred.Where(kv => kv.Key != pid && kv.Value.Contains(a.Date)).Select(kv => Name(kv.Key)).ToList();
                if (wanted)
                    reasons.Add("Preferred date: the officer asked to work this day." + (others.Count > 0 ? $" Also requested by {Join(others, 3)}." : ""));
                else if (others.Count > 0)
                    reasons.Add($"Requested by {Join(others, 3)}; the officer did not ask for this date.");

                if (a.IsWeekendHoliday)
                {
                    var t = before.GetValueOrDefault(pid, PersonTotals.Unknown);
                    string priorText = t.Known
                        ? $"had {t.WeekendHoliday} weekend/public holiday shift(s) before this rota (average {priorAverage:0.0})"
                        : "has no earlier tally, so the group average is used";
                    reasons.Add($"Weekend/public holiday duty is shared fairly: {person.Name} {priorText}.");
                }
                else reasons.Add("Ordinary weekday shift, shared so everyone gets the same number of shifts.");

                var prefs = new List<string>();
                if (person.PreferWeekendHoliday) prefs.Add("asked for weekend/public holiday duty");
                if (person.ExtraShift) prefs.Add("is marked to take an extra shift");
                if (person.WeekendWeight > 1) prefs.Add($"has weekend weight {person.WeekendWeight}");
                if (prefs.Count > 0) reasons.Add($"Officer setting: {string.Join("; ", prefs)}.");

                reasons.Add($"Available: {roster.Count - off.Count} of {roster.Count} officers" + (off.Count > 0 ? $" (off: {Join(off, 5)})." : "."));

                var mine = shiftsByPerson[pid];
                reasons.Add(mine.Any(d => Math.Abs(d.DayNumber - a.Date.DayNumber) == 1)
                    ? "Back-to-back: the officer also works the day before or after."
                    : "Not back-to-back with the officer's other shift(s); not on leave, blocked or clinic-blocked.");

                if (a.IsWeekendHoliday || peaks.ContainsKey(a.Date))
                {
                    int pts = holidays.ContainsKey(a.Date) ? limits.HolidayReward
                        : a.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? limits.WeekendReward : limits.PeakReward;
                    reasons.Add($"Earns +{pts} leave points for the next rota.");
                }
            }

            var path = new List<string>();
            if (evs.Count > 0)
            {
                path.Add($"{evs[0].Before ?? "nobody"} (generated)");
                path.AddRange(evs.Select(e => $"{e.After ?? "nobody"} ({e.Kind.ToLowerInvariant()}, {ShortTime(e.At, clock)})"));
            }
            days.Add(new ReportDay(a.Date, kind, a.IsWeekendHoliday, a.PersonId is null ? null : Name(a.PersonId), source, reasons, path));
        }

        // ---- officers ----
        var officers = new List<ReportOfficer>();
        foreach (var id in roster.OrderBy(i => people[i].SortOrder).ThenBy(i => people[i].Name))
        {
            var p = people[id];
            var mine = shiftsByPerson.GetValueOrDefault(id, []);
            int weekend = assigned.Count(a => a.PersonId == id && a.IsWeekendHoliday);
            var t = before.GetValueOrDefault(id, PersonTotals.Unknown);
            var prefs = preferred.GetValueOrDefault(id) ?? [];
            var reward = ShiftRewardPolicy.For(limits, mine, holidaySet, peakSet);
            int grant = rules.Grants.GetValueOrDefault(id)?.Points ?? 0;
            int rewardIn = rules.Reward(id).Points;
            int deducted = rules.PointsDeducted(id);
            var parts = new List<string> { $"base {rules.BaseBudget}" };
            if (grant > 0) parts.Add($"+{grant} granted");
            if (rewardIn > 0) parts.Add($"+{rewardIn} reward");
            if (deducted > 0) parts.Add($"-{deducted} confirmed leave");

            var flags = new List<string>();
            if (p.BlockedWeekdays != 0) flags.Add($"Blocked: {BlockedDays.Describe(p.BlockedWeekdays)}");
            if (p.ExtraShift) flags.Add("Extra shift");
            if (p.PreferWeekendHoliday) flags.Add("Prefers weekend/holiday");
            if (p.Status != OfficerStatus.OnCall) flags.Add($"Status: {p.Status}");

            officers.Add(new ReportOfficer(p.Code, p.Name, p.Clinic?.Name, mine.Count, mine.Count - weekend, weekend,
                t.Known ? t.WeekendHoliday : null,
                rules.LeaveByPerson.GetValueOrDefault(id)?.Count ?? 0, rules.ConfirmedDays(id),
                rules.Policy.Points(rules.OwnLeave(id)), rules.OwnBudget(id), string.Join(" ", parts),
                prefs.Count, prefs.Count(d => mine.Contains(d)),
                reward.Points > 0 ? $"+{reward.Points} ({reward.Describe()})" : "none",
                flags.Count > 0 ? string.Join("; ", flags) : null));
        }

        // ---- swaps ----
        var swapRows = swaps.Select(s => new ReportSwap(
            clock.Format(s.CreatedAt), Name(s.FromPersonId), Day(s.FromDate), Name(s.ToPersonId), Day(s.ToDate),
            s.Status.ToString(), s.RespondedAt is { } r ? clock.Format(r) : "-", s.Note)).ToList();

        // ---- timeline and audit trail ----
        var timeline = new List<ReportStage>();
        var changes = new List<ReportChange>();
        foreach (var l in logs)
        {
            var swap = swaps.FirstOrDefault(s => s.Id.ToString() == l.EntityId);
            string by = Who(l.AccountId);
            (string what, string? detail)? row = l.Action switch
            {
                "run.generate" => ("Rota generated", $"Seed {run.Seed}"),
                "run.override" => ($"Override on {(Str(l.Detail, "date") is { } d && DateOnly.TryParse(d, CultureInfo.InvariantCulture, out var dd) ? Day(dd) : "a day")}",
                    $"{Str(l.Before, "officer") ?? "nobody"} replaced by {Str(l.After, "officer") ?? "nobody"}"),
                "run.review" => ("Sent to officers for review", Str(l.Detail, "swapDeadline") is { } sd ? $"Swap deadline {sd}" : "Open until published"),
                "run.withdraw-review" => ("Taken back to draft", null),
                "run.publish" => ("Published", null),
                "run.unpublish" => ("Unpublished", null),
                "swap.request" => ("Swap requested", swap is null ? null : SwapText(swap, Name)),
                "swap.accept" => ("Swap accepted", swap is null ? null : SwapText(swap, Name)),
                "swap.decline" => ("Swap declined", swap is null ? null : SwapText(swap, Name)),
                "swap.cancel" => ("Swap withdrawn", swap is null ? null : SwapText(swap, Name)),
                _ => null,
            };
            if (row is null) continue;
            changes.Add(new ReportChange(clock.Format(l.At), by, row.Value.what, row.Value.detail));
            if (l.Action is "run.generate" or "run.review" or "run.withdraw-review" or "run.publish" or "run.unpublish")
                timeline.Add(new ReportStage(clock.Format(l.At), row.Value.what, by));
        }
        if (timeline.All(t => t.What != "Rota generated")) timeline.Insert(0, new ReportStage(clock.Format(run.CreatedAt), "Rota generated", "n/a"));
        if (run.PublishedAt is { } pub && timeline.All(t => t.What != "Published")) timeline.Add(new ReportStage(clock.Format(pub), "Published", "n/a"));

        // ---- summary ----
        var assignedDays = run.Assignments.Count(a => a.PersonId != null);
        int backToBack = assigned.GroupBy(a => a.PersonId).Sum(g => g.OrderBy(a => a.Date).Zip(g.OrderBy(a => a.Date).Skip(1))
            .Count(pair => pair.Second.Date.DayNumber - pair.First.Date.DayNumber == 1));
        int overrides = logs.Count(l => l.Action == "run.override");
        int totalDays = period.EndDate.DayNumber - period.StartDate.DayNumber + 1;
        var summary = new List<ReportFact>
        {
            new("Rota period", period.Name),
            new("Dates", $"{Day(period.StartDate)} to {Day(period.EndDate)} {period.EndDate.Year}"),
            new("Days in rota", totalDays.ToString()),
            new("Officers on the rota", roster.Count.ToString()),
            new("Days assigned", $"{assignedDays} of {run.Assignments.Count}"),
            new("Unassigned days", (run.Assignments.Count - assignedDays).ToString()),
            new("Weekend / holiday days", run.Assignments.Count(a => a.IsWeekendHoliday).ToString()),
            new("Back-to-back shifts", backToBack.ToString()),
            new("Days changed by hand", run.Assignments.Count(a => a.IsManual).ToString()),
            new("Admin overrides", overrides.ToString()),
            new("Swaps accepted", $"{swaps.Count(s => s.Status == SwapStatus.Accepted)} of {swaps.Count} requested"),
            new("Published", run.PublishedAt is { } pa ? clock.Format(pa) : "n/a"),
            new("Leave deadline", period.LeaveDeadline is { } ld ? Day(ld) + $" {ld.Year}" : "until locked"),
            new("Swap deadline", period.SwapDeadline is { } sdl ? Day(sdl) + $" {sdl.Year}" : "until published"),
            new("Generator seed", run.Seed.ToString()),
        };

        var rulesFacts = new List<ReportFact>
        {
            new("Leave points", $"{Pct(limits.PointsPercent)} of weekend, holiday and peak days (base {rules.BaseBudget})"),
            new("Weekday allowance", $"{Pct(limits.WeekdayPercent)} of ordinary weekdays ({rules.Policy.WeekdayAllowance})"),
            new("Point cost", $"weekend {limits.WeekendCost}, public holiday {limits.HolidayCost}, peak {limits.PeakCost}"),
            new("Busy-day cap", $"{Pct(limits.BusyDayCapPercent)} of officers off"),
            new("Weekday cap", $"{Pct(limits.WeekdayCapPercent)} of officers off"),
            new("Confirmed leave", $"{Pct(limits.ConfirmedLeavePercent)} of its cost is deducted"),
            new("Rewards for next rota", $"holiday +{limits.HolidayReward}, weekend +{limits.WeekendReward}, peak +{limits.PeakReward}"),
            new("Rewards received here", rules.RewardFrom is { } rf ? $"from {rf}" : "none (first rota)"),
        };

        return new RotaReportData(
            period.Name, period.StartDate, period.EndDate, run.IsPublished ? "Published" : "Not published",
            clock.Now, generatedBy, runId.ToString(), run.Seed, run.Objective,
            timeline, summary, run.Warnings,
            holidays.OrderBy(h => h.Key).Select(h => new ReportDayInfo(h.Key, "Public holiday", h.Value))
                .Concat(peaks.OrderBy(h => h.Key).Select(h => new ReportDayInfo(h.Key, "Peak day", h.Value)))
                .OrderBy(x => x.Date).ToList(),
            officers, excluded.Select(id => Name(id)).OrderBy(n => n).ToList(), days, swapRows, changes, rulesFacts);
    }

    private static string SwapText(SwapRequest s, Func<Guid?, string> name) =>
        $"{name(s.FromPersonId)} gives {Day(s.FromDate)}, {name(s.ToPersonId)} gives {Day(s.ToDate)}";

    private static string Pct(double share) => $"{Math.Round(share * 100)}%";

    private static string Day(DateOnly d) => d.ToString("ddd d MMM", CultureInfo.InvariantCulture);
    private static string Time(LocalClock clock, DateTimeOffset t) => clock.Format(t);
    private static string ShortTime(DateTimeOffset t, LocalClock clock) => clock.Format(t).Split(',')[0];

    private static string Join(IReadOnlyList<string> names, int max) =>
        names.Count <= max ? string.Join(", ", names) : $"{string.Join(", ", names.Take(max))} and {names.Count - max} more";

    /// <summary>A string property from an audit entry's JSON, or null.</summary>
    private static string? Str(string? json, string property)
    {
        if (json is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty(property, out var v)
                   && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
}
