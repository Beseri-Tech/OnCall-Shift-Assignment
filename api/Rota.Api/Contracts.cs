using System.Text.Json;
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

/// <param name="Email">The officer's sign-in email, if they have an account.</param>
/// <param name="InvitePending">Invited but hasn't signed in and set their own password yet.</param>
public sealed record AdminPersonDto(
    Guid Id, string Code, string Name, OfficerStatus Status, string? StatusReason, DateOnly? ExcludedUntil,
    Guid? ClinicId, string? ClinicName, Guid? DistrictId, string? District, Guid? StateId, string? State, string? Phone, int SortOrder,
    bool ExtraShift, bool PreferWeekendHoliday, int WeekendWeight, string? Email, AccountRole? Role, bool InvitePending,
    bool AccountEnabled);

/// <param name="Email">Invites the officer in the same step (Role: Officer or Admin). Only used while the officer has no
/// account; change an existing account's email on the Accounts tab.</param>
public sealed record UpsertPersonRequest(
    string Name, string? Code, OfficerStatus Status = OfficerStatus.OnCall, bool ExtraShift = false, bool PreferWeekendHoliday = false,
    int WeekendWeight = 1, string? StatusReason = null, DateOnly? ExcludedUntil = null, Guid? ClinicId = null, string? Phone = null,
    string? Email = null, AccountRole Role = AccountRole.Officer);

/// <summary>Invite is set when an email was given.</summary>
public sealed record SavePersonResult(Guid Id, InviteResult? Invite);

/// <summary>Shifts done so far. Published* = from published rotas; the rest is the admin's adjustment.</summary>
public sealed record TallyRowDto(
    Guid PersonId, string Code, string Name, OfficerStatus Status, string? ClinicName, string? District,
    int Weekday, int WeekendHoliday, int Total, int PublishedWeekday, int PublishedWeekendHoliday, bool Known);

public sealed record TallyUpdate(Guid PersonId, int Weekday, int WeekendHoliday);

// State -> district -> clinic -> officer.

public sealed record StateDto(Guid Id, string Name, IReadOnlyList<DistrictDto> Districts);

public sealed record DistrictDto(Guid Id, string Name, int Clinics);

public sealed record UpsertStateRequest(string Name);

public sealed record UpsertDistrictRequest(string Name, Guid StateId);

public sealed record ClinicDto(Guid Id, string Name, Guid DistrictId, string District, string State, int People);

public sealed record UpsertClinicRequest(string Name, Guid DistrictId);

public sealed record BulkAddRequest(string Names);

/// <summary>CSV import of officers. Commit = false only checks the file; nothing is saved unless every row is valid.</summary>
public sealed record ImportPeopleRequest(string Csv, bool Commit);

/// <param name="Line">Line number in the file (the header is line 1).</param>
public sealed record ImportRowDto(
    int Line, string Name, string? Code, string? Email, AccountRole? Role, string? Phone, string? State, string? District,
    string? Clinic, IReadOnlyList<string> Errors);

/// <param name="Passwords">Temporary passwords of invites whose email couldn't be sent, for the admin to pass on.</param>
public sealed record ImportResultDto(
    IReadOnlyList<ImportRowDto> Rows, IReadOnlyList<string> FileErrors, bool Committed, int Added, int Invited,
    IReadOnlyList<IssuedPasswordDto> Passwords);

public sealed record IssuedPasswordDto(string Name, string Email, string TempPassword);

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

public sealed record PeriodDayDto(DateOnly Date, PeriodDayKind Kind, string Name);

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


/// <summary>One audit entry. Detail, Before and After are the JSON the change was logged with.</summary>
public sealed record AuditEntryDto(
    long Id, DateTimeOffset At, Guid? AccountId, string? ActorEmail, string? ActorName, string Action, string? Entity,
    string? EntityId, Guid? PersonId, string? PersonName, JsonElement? Detail, JsonElement? Before, JsonElement? After,
    string? Ip, string? UserAgent);

public sealed record AuditPageDto(IReadOnlyList<AuditEntryDto> Items, int Total, int Page, int PageSize);

public sealed record AuditActorDto(Guid AccountId, string Email, string? Name);

/// <summary>What the audit log can be filtered by: who has made changes, and the actions and kinds of thing logged.</summary>
public sealed record AuditFiltersDto(IReadOnlyList<AuditActorDto> Actors, IReadOnlyList<string> Actions, IReadOnlyList<string> Entities);
