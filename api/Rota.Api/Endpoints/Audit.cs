using Microsoft.EntityFrameworkCore;
using Rota.Api.Data;

namespace Rota.Api.Endpoints;

/// <summary>
/// What the audit log records as a thing's values before and after a change. Names rather than ids where the admin
/// would otherwise see a GUID, so the log reads on its own.
/// </summary>
internal static class Audit
{
    public static async Task<object> PersonAsync(RotaDbContext db, Person p, CancellationToken ct) => new
    {
        p.Name, p.Code, p.Status, p.StatusReason, p.ExcludedUntil,
        Clinic = await ClinicNameAsync(db, p.ClinicId, ct),
        p.Phone, p.ExtraShift, p.PreferWeekendHoliday, p.WeekendWeight,
    };

    public static object Period(RotaPeriod p) => new
    {
        p.Name, p.StartDate, p.EndDate, p.LeaveDeadline, p.PointsBudget, p.Status, p.SwapDeadline,
    };

    public static async Task<object> AccountAsync(RotaDbContext db, Account a, CancellationToken ct) => new
    {
        a.Email, a.Role, Officer = await PersonNameAsync(db, a.PersonId, ct), a.Enabled,
    };

    public static async Task<object> DistrictAsync(RotaDbContext db, District d, CancellationToken ct) => new
    {
        d.Name, State = await db.States.Where(s => s.Id == d.StateId).Select(s => s.Name).FirstOrDefaultAsync(ct),
    };

    public static async Task<object> ClinicAsync(RotaDbContext db, Clinic c, CancellationToken ct) => new
    {
        c.Name, District = await db.Districts.Where(d => d.Id == c.DistrictId).Select(d => d.Name).FirstOrDefaultAsync(ct),
    };

    public static async Task<string?> PersonNameAsync(RotaDbContext db, Guid? id, CancellationToken ct) =>
        id is { } pid ? await db.People.Where(p => p.Id == pid).Select(p => p.Name).FirstOrDefaultAsync(ct) : null;

    private static async Task<string?> ClinicNameAsync(RotaDbContext db, Guid? id, CancellationToken ct) =>
        id is { } cid ? await db.Clinics.Where(c => c.Id == cid).Select(c => c.Name).FirstOrDefaultAsync(ct) : null;
}
