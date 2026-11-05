using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;

namespace Rota.Api.Services;

/// <summary>Dates the admin blocked for a whole clinic (e.g. a clinic event), looked up per officer.</summary>
public static class ClinicBlocks
{
    /// <summary>Blocked dates in the range per officer, for officers whose clinic has any. Officers without a clinic never appear.</summary>
    public static async Task<Dictionary<Guid, HashSet<DateOnly>>> ByPersonAsync(RotaDbContext db, DateOnly start, DateOnly end,
        CancellationToken ct)
    {
        var rows = await (
            from p in db.People
            join d in db.ClinicBlockedDays on p.ClinicId equals (Guid?)d.ClinicId
            where d.Date >= start && d.Date <= end
            select new { p.Id, d.Date }).ToListAsync(ct);
        return rows.GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.Select(r => r.Date).ToHashSet());
    }

    /// <summary>The reason for the block on <paramref name="date"/> if the officer's clinic is blocked that day, else null.</summary>
    public static async Task<(bool Blocked, string? Reason)> OnAsync(RotaDbContext db, Guid personId, DateOnly date, CancellationToken ct)
    {
        var day = await (
            from p in db.People
            join d in db.ClinicBlockedDays on p.ClinicId equals (Guid?)d.ClinicId
            where p.Id == personId && d.Date == date
            select new { d.Reason }).FirstOrDefaultAsync(ct);
        return (day is not null, day?.Reason);
    }
}
