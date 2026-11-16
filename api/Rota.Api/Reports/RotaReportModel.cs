namespace Rota.Api.Reports;

/// <summary>
/// Everything the published-rota report shows, already resolved to names and text. The PDF renderer only lays this out;
/// <see cref="RotaReportBuilder"/> fills it from the database.
/// </summary>
public sealed record RotaReportData(
    string PeriodName,
    DateOnly Start,
    DateOnly End,
    string Status,
    string GeneratedOn,         // when this PDF was produced (local time, already formatted)
    string GeneratedBy,         // admin who downloaded it
    string RunId,
    int Seed,
    double Objective,
    IReadOnlyList<ReportStage> Timeline,
    IReadOnlyList<ReportFact> Summary,
    IReadOnlyList<string> GeneratorNotes,
    IReadOnlyList<ReportDayInfo> SpecialDays,
    IReadOnlyList<ReportOfficer> Officers,
    IReadOnlyList<string> LeftOut,
    IReadOnlyList<ReportDay> Days,
    IReadOnlyList<ReportSwap> Swaps,
    IReadOnlyList<ReportChange> Changes,
    IReadOnlyList<ReportFact> Rules);

/// <summary>One step in the life of the rota (generated, sent for review, published...).</summary>
public sealed record ReportStage(string When, string What, string By);

public sealed record ReportFact(string Label, string Value);

public sealed record ReportDayInfo(DateOnly Date, string Kind, string Name);

/// <summary>One officer's totals and leave-point position for the period.</summary>
public sealed record ReportOfficer(
    string Code,
    string Name,
    string? Clinic,
    int Total,
    int Weekday,
    int WeekendHoliday,
    int? PriorWeekendHoliday,
    int LeaveDays,
    int ConfirmedDays,
    int PointsUsed,
    int PointsBudget,
    string BudgetDetail,
    int PreferredTotal,
    int PreferredHonoured,
    string EarnedReward,
    string? Flags);

/// <summary>
/// One day of the rota with the officer who covers it and why.
/// <see cref="Path"/> lists every owner of the day in order, so a swap or override is visible next to the final name.
/// </summary>
public sealed record ReportDay(
    DateOnly Date,
    string DayKind,             // "Weekday", "Weekend", "Public holiday: X", "Peak day: X"
    bool IsWeekendHoliday,
    string? Officer,
    string Source,              // "Generator", "Swap", "Admin override", "Unassigned"
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> Path);

public sealed record ReportSwap(
    string Requested,
    string Requester,
    string GivesDate,
    string With,
    string TakesDate,
    string Status,
    string Answered,
    string? Note);

/// <summary>An entry from the audit trail about this rota: who did what and when.</summary>
public sealed record ReportChange(string When, string By, string What, string? Detail);
