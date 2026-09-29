using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;
using Rota.Api.Services;

namespace Rota.Api.Endpoints;

internal static class Mapping
{
    public static bool IsEditable(this RotaPeriod p, DateOnly today) =>
        p.Status == PeriodStatus.Open && (p.LeaveDeadline is null || today <= p.LeaveDeadline);

    public static PeriodDto ToDto(this RotaPeriod p, DateOnly today) =>
        new(p.Id, p.Name, p.StartDate, p.EndDate, p.LeaveDeadline, p.Status, p.IsEditable(today), p.PointsBudget);

    public static TotalsDto ToDto(this PersonTotals t) => new(t.Total, t.Weekday, t.WeekendHoliday);

    public static bool IsWeekend(DateOnly d) => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static IEnumerable<DateOnly> Days(DateOnly start, DateOnly end)
    {
        for (var d = start; d <= end; d = d.AddDays(1)) yield return d;
    }

    public static async Task<Dictionary<DateOnly, string>> HolidaysAsync(RotaDbContext db, DateOnly start, DateOnly end, CancellationToken ct) =>
        await db.Holidays.Where(h => h.Date >= start && h.Date <= end).ToDictionaryAsync(h => h.Date, h => h.Name, ct);

    public static void Log(RotaDbContext db, HttpContext http, string action, Guid? personId, object? detail)
    {
        db.ChangeLog.Add(new ChangeLog
        {
            Action = action,
            PersonId = personId,
            Detail = detail is null ? null : JsonSerializer.Serialize(detail),
            Ip = http.Connection.RemoteIpAddress?.ToString(),
            UserAgent = http.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua[..Math.Min(ua.Length, 300)] : null,
        });
    }

    public static List<RotaDayDto> ToDays(IEnumerable<ShiftAssignment> assignments, IReadOnlyDictionary<Guid, string> names,
        IReadOnlyDictionary<DateOnly, string> holidays) =>
        assignments.OrderBy(a => a.Date)
            .Select(a => new RotaDayDto(
                a.Date, a.IsWeekendHoliday, holidays.GetValueOrDefault(a.Date),
                a.PersonId, a.PersonId is { } id ? names.GetValueOrDefault(id) : null, a.IsManual))
            .ToList();
}
