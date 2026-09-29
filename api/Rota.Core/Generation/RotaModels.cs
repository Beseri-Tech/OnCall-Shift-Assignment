namespace Rota.Core.Generation;

/// <summary>A person as the generator sees them for one rota.</summary>
/// <param name="PriorWeekendShifts">Weekend/public holiday shifts done before this rota; null = unknown (group average used).</param>
public sealed record RotaPerson(
    Guid Id,
    string Name,
    IReadOnlyCollection<DateOnly> Leave,
    IReadOnlyCollection<DateOnly> Preferred,
    bool ExtraShift = false,
    bool PreferWeekendHoliday = false,
    int WeekendWeight = 1,
    int? PriorWeekendShifts = null);

public sealed record RotaInput(
    IReadOnlyList<RotaPerson> People,
    DateOnly Start,
    DateOnly End,
    IReadOnlyCollection<DateOnly> Holidays,
    int? Seed = null);

/// <param name="PersonId">null = nobody could be assigned (everyone on leave).</param>
public sealed record ShiftAssignment(DateOnly Date, Guid? PersonId, bool IsWeekendHoliday);

public sealed record PersonStats(
    Guid PersonId,
    int TargetTotal,
    int TargetWeekendHoliday,
    int Total,
    int Weekday,
    int WeekendHoliday);

public sealed record RotaResult(
    IReadOnlyList<ShiftAssignment> Assignments,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<PersonStats> Stats,
    int Seed,
    double Objective);
