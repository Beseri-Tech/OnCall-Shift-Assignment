using Rota.Api.Data;
using Rota.Core.Names;

namespace Rota.Api;

// ---------------- shared ----------------

public sealed record TotalsDto(int Total, int Weekday, int WeekendHoliday);

public sealed record PeriodDto(
    Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, DateOnly? LeaveDeadline,
    PeriodStatus Status, bool IsEditable);

public sealed record HolidayDto(DateOnly Date, string Name);

public sealed record LeaveEntryDto(DateOnly Date, string? Note);

public sealed record RotaDayDto(
    DateOnly Date, bool IsWeekendHoliday, string? HolidayName, Guid? PersonId, string? PersonName, bool IsManual);

// ---------------- public ----------------

public sealed record PersonSummaryDto(Guid Id, string Code, string Name);

public sealed record PersonProfileDto(Guid Id, string Code, string Name, TotalsDto Totals);

public sealed record EntriesDto(IReadOnlyList<LeaveEntryDto> Leave, IReadOnlyList<DateOnly> Preferred);

public sealed record ParseRequest(string Text, DateOnly Reference);

public sealed record ParseResponse(IReadOnlyList<DateOnly> Dates, string? Error);

public sealed record OverviewDay(DateOnly Date, bool IsWeekendHoliday, string? HolidayName, int Available);

public sealed record OverviewPerson(
    Guid Id, string Code, string Name, IReadOnlyList<LeaveEntryDto> Leave, IReadOnlyList<DateOnly> Preferred);

public sealed record OverviewDto(PeriodDto Period, IReadOnlyList<OverviewDay> Days, IReadOnlyList<OverviewPerson> People);

public sealed record PublishedRotaDto(PeriodDto Period, Guid RunId, IReadOnlyList<RotaDayDto> Days);

// ---------------- admin ----------------

public sealed record LoginRequest(string Password);

public sealed record AdminPersonDto(
    Guid Id, string Code, string Name, bool Active, int SortOrder,
    bool ExtraShift, bool PreferWeekendHoliday, int WeekendWeight,
    int? OpeningTotal, int? OpeningWeekday, int? OpeningWeekendHoliday,
    TotalsDto Totals, bool TotalsKnown);

public sealed record UpsertPersonRequest(
    string Name, string? Code, bool Active = true, bool ExtraShift = false, bool PreferWeekendHoliday = false,
    int WeekendWeight = 1, int? OpeningTotal = null, int? OpeningWeekday = null, int? OpeningWeekendHoliday = null);

public sealed record BulkAddRequest(string Names);

public sealed record ReorderRequest(IReadOnlyList<Guid> Ids);

public sealed record UpsertPeriodRequest(string Name, DateOnly StartDate, DateOnly EndDate, DateOnly? LeaveDeadline);

public sealed record UpsertHolidayRequest(DateOnly Date, string Name);

public sealed record BulkHolidayRequest(string Text, DateOnly Reference, string Name);

public sealed record GenerateRequest(int? Seed);

public sealed record RunStatDto(
    Guid PersonId, string Code, string Name, int Total, int Weekday, int WeekendHoliday,
    int LeaveDays, int? PriorWeekendHoliday);

public sealed record RunDto(
    Guid Id, Guid PeriodId, DateTimeOffset CreatedAt, int Seed, bool IsPublished,
    IReadOnlyList<string> Warnings, int Unassigned, int ConsecutivePairs, int ManualChanges);

public sealed record RunDetailDto(RunDto Run, IReadOnlyList<RotaDayDto> Days, IReadOnlyList<RunStatDto> Stats);

public sealed record OverrideRequest(Guid? PersonId);

public sealed record OverrideResponse(RotaDayDto Day, IReadOnlyList<string> Warnings);

public sealed record LeaveImportRow(
    string RawName, Guid? PersonId, string? PersonName, NameMatchType MatchType,
    IReadOnlyList<LeaveEntryDto> Leave, IReadOnlyList<string> Warnings);

public sealed record LeaveImportPreview(IReadOnlyList<DateOnly> Months, IReadOnlyList<LeaveImportRow> Rows);

public enum ImportAction { Match, New, Skip }

public sealed record LeaveImportCommitRow(string RawName, ImportAction Action, Guid? PersonId, IReadOnlyList<LeaveEntryDto> Leave);

public sealed record LeaveImportCommit(IReadOnlyList<LeaveImportCommitRow> Rows);

public sealed record ImportResult(int Updated, int Added, int Skipped, IReadOnlyList<string> Warnings);

public sealed record OpeningImportRow(string RawName, int? Value, Guid? PersonId, string? PersonName, NameMatchType MatchType);

public sealed record OpeningImportPreview(IReadOnlyList<OpeningImportRow> Rows, IReadOnlyList<string> PeopleWithoutRow);

public sealed record OpeningCommitRow(Guid PersonId, int? OpeningWeekendHoliday);

public sealed record OpeningImportCommit(IReadOnlyList<OpeningCommitRow> Rows);
