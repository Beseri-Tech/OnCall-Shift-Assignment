using Rota.Api.Data;

namespace Rota.Api;

// ---------------- shared ----------------

public sealed record TotalsDto(int Total, int Weekday, int WeekendHoliday);

public sealed record PeriodDto(
    Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, DateOnly? LeaveDeadline,
    PeriodStatus Status, bool IsEditable, int? PointsBudget, DateOnly? SwapDeadline);

public sealed record HolidayDto(DateOnly Date, string Name);

public sealed record LeaveEntryDto(DateOnly Date, string? Note);

public sealed record RotaDayDto(
    DateOnly Date, bool IsWeekendHoliday, string? HolidayName, Guid? PersonId, string? PersonName, bool IsManual);

// ---------------- public ----------------

public sealed record PersonSummaryDto(Guid Id, string Code, string Name);

public sealed record PersonProfileDto(Guid Id, string Code, string Name, TotalsDto Totals, bool TotalsKnown);

/// <summary>One of an officer's shifts in a published rota.</summary>
/// <param name="InReview">From a rota still in review: it can change before it's published.</param>
public sealed record ShiftDto(DateOnly Date, bool IsWeekendHoliday, string? HolidayName, Guid PeriodId, string PeriodName, bool InReview,
    Guid RunId);

public sealed record EntriesDto(IReadOnlyList<LeaveEntryDto> Leave, IReadOnlyList<DateOnly> Preferred);

public sealed record ParseRequest(string Text, DateOnly Reference);

public sealed record ParseResponse(IReadOnlyList<DateOnly> Dates, string? Error);

public sealed record OverviewDay(DateOnly Date, bool IsWeekendHoliday, string? HolidayName, int Available);

public sealed record OverviewPerson(
    Guid Id, string Code, string Name, IReadOnlyList<LeaveEntryDto> Leave, IReadOnlyList<DateOnly> Preferred);

public sealed record OverviewDto(PeriodDto Period, IReadOnlyList<OverviewDay> Days, IReadOnlyList<OverviewPerson> People);

/// <param name="InReview">A draft shown for review (swaps allowed), not yet published.</param>
public sealed record PublishedRotaDto(PeriodDto Period, Guid RunId, IReadOnlyList<RotaDayDto> Days, bool InReview);

public sealed record SwapDto(
    Guid Id, Guid RunId, string PeriodName, Guid FromPersonId, string FromName, DateOnly FromDate,
    Guid ToPersonId, string ToName, DateOnly ToDate, string? Note, SwapStatus Status, DateTimeOffset CreatedAt,
    IReadOnlyList<string> Warnings);

public sealed record CreateSwapRequest(Guid RunId, DateOnly FromDate, DateOnly ToDate, string? Note);

public sealed record NotificationDto(long Id, string Kind, string Title, string? Body, string? Link, DateTimeOffset CreatedAt, bool Read);

public sealed record NotificationsDto(IReadOnlyList<NotificationDto> Items, int Unread);

// ---------------- admin ----------------

public sealed record LoginRequest(string Email, string Password);

public sealed record MeDto(Guid AccountId, string Email, AccountRole Role, Guid? PersonId, string? PersonName, bool MustChangePassword);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record AccountDto(
    Guid Id, string Email, AccountRole Role, Guid? PersonId, string? PersonName, bool Enabled, bool MustChangePassword,
    DateTimeOffset? TempPasswordExpiresAt, DateTimeOffset? LastLoginAt);

public sealed record InviteRequest(string Email, AccountRole Role, Guid? PersonId);

/// <summary>EmailSent = queued for sending. TempPassword is only returned when email isn't set up, so the admin can pass it on.</summary>
public sealed record InviteResult(Guid AccountId, bool EmailSent, string? TempPassword);

public sealed record UpdateAccountRequest(string Email, AccountRole Role, Guid? PersonId, bool Enabled);

public sealed record AdminPersonDto(
    Guid Id, string Code, string Name, OfficerStatus Status, string? StatusReason, DateOnly? ExcludedUntil,
    Guid? ClinicId, string? ClinicName, string? Area, string? Phone, int SortOrder,
    bool ExtraShift, bool PreferWeekendHoliday, int WeekendWeight);

public sealed record UpsertPersonRequest(
    string Name, string? Code, OfficerStatus Status = OfficerStatus.OnCall, bool ExtraShift = false, bool PreferWeekendHoliday = false,
    int WeekendWeight = 1, string? StatusReason = null, DateOnly? ExcludedUntil = null, Guid? ClinicId = null, string? Phone = null);

/// <summary>Shifts done so far. Published* = from published rotas; the rest is the admin's adjustment.</summary>
public sealed record TallyRowDto(
    Guid PersonId, string Code, string Name, OfficerStatus Status, string? ClinicName, string? Area,
    int Weekday, int WeekendHoliday, int Total, int PublishedWeekday, int PublishedWeekendHoliday, bool Known);

public sealed record TallyUpdate(Guid PersonId, int Weekday, int WeekendHoliday);

public sealed record ClinicDto(Guid Id, string Name, string Area, int People);

public sealed record UpsertClinicRequest(string Name, string Area);

public sealed record BulkAddRequest(string Names);

public sealed record ReorderRequest(IReadOnlyList<Guid> Ids);

public sealed record UpsertPeriodRequest(string Name, DateOnly StartDate, DateOnly EndDate, DateOnly? LeaveDeadline, int? PointsBudget = null);

/// <summary>Leave limits for one officer in one period. Budget excludes Extra (admin top-up).</summary>
public sealed record LeaveRulesDto(
    int Budget, int Extra, string? ExtraReason, int WeekdayAllowance, int BusyDayCap, int WeekdayCap, int OnCall,
    int WeekendCost, int HolidayCost, int PeakCost, IReadOnlyList<HolidayDto> Peaks, IReadOnlyDictionary<DateOnly, int> OthersOff);

public sealed record PointsRowDto(
    Guid PersonId, string Code, string Name, int PointsUsed, int Budget, int Extra, string? Reason,
    int WeekdaysUsed, int WeekdayAllowance);

public sealed record PointGrantRequest(int Points, string? Reason);

public sealed record UpsertHolidayRequest(DateOnly Date, string Name);

public sealed record BulkHolidayRequest(string Text, DateOnly Reference, string Name);

public sealed record GenerateRequest(int? Seed);

public sealed record ReviewRequest(DateOnly? SwapDeadline);

public sealed record RunStatDto(
    Guid PersonId, string Code, string Name, int Total, int Weekday, int WeekendHoliday,
    int LeaveDays, int? PriorWeekendHoliday);

public sealed record RunDto(
    Guid Id, Guid PeriodId, DateTimeOffset CreatedAt, int Seed, bool IsPublished, bool IsInReview,
    IReadOnlyList<string> Warnings, int Unassigned, int ConsecutivePairs, int ManualChanges);

public sealed record RunDetailDto(RunDto Run, IReadOnlyList<RotaDayDto> Days, IReadOnlyList<RunStatDto> Stats);

public sealed record OverrideRequest(Guid? PersonId);

public sealed record OverrideResponse(RotaDayDto Day, IReadOnlyList<string> Warnings);

