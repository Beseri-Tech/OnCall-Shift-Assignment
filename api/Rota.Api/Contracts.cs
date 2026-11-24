using System.Text.Json;
using Rota.Api.Data;

namespace Rota.Api;

// ---------------- shared ----------------

public sealed record TotalsDto(int Total, int Weekday, int WeekendHoliday);

public sealed record PeriodDto(
    Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, DateOnly? LeaveDeadline,
    PeriodStatus Status, bool IsEditable, int? PointsBudget, DateOnly? SwapDeadline,
    IReadOnlyList<Guid>? ExcludedPersonIds = null);

public sealed record HolidayDto(DateOnly Date, string Name);

/// <param name="Confirmed">Confirmed leave (approved by the department, entered by an admin): the officer can't change it.</param>
public sealed record LeaveEntryDto(DateOnly Date, string? Note, bool Confirmed = false);

/// <summary>One confirmed-leave range as its officer sees it.</summary>
public sealed record ConfirmedLeaveDto(Guid Id, DateOnly StartDate, DateOnly EndDate, string Reason, string? Reference);

public sealed record RotaDayDto(
    DateOnly Date, bool IsWeekendHoliday, string? HolidayName, Guid? PersonId, string? PersonName, bool IsManual);

// ---------------- public ----------------

public sealed record PersonSummaryDto(Guid Id, string Code, string Name);

public sealed record PersonProfileDto(Guid Id, string Code, string Name, TotalsDto Totals, bool TotalsKnown,
    IReadOnlyList<int> BlockedWeekdays, string? BlockedReason, IReadOnlyList<ClinicBlockedDayDto>? ClinicBlockedDays = null);

/// <summary>A date nobody from the clinic can be on call.</summary>
public sealed record ClinicBlockedDayDto(Guid ClinicId, string ClinicName, DateOnly Date, string? Reason);

public sealed record AddClinicBlockedDaysRequest(IReadOnlyList<DateOnly> Dates, string? Reason);

/// <summary>One of an officer's shifts in a published rota.</summary>
/// <param name="InReview">From a rota still in review: it can change before it's published.</param>
public sealed record ShiftDto(DateOnly Date, bool IsWeekendHoliday, string? HolidayName, Guid PeriodId, string PeriodName, bool InReview,
    Guid RunId);

/// <param name="Leave">The officer's own leave (confirmed leave is listed separately and ignored when saving).</param>
/// <param name="Confirmed">Confirmed leave overlapping the period; read only.</param>
public sealed record EntriesDto(IReadOnlyList<LeaveEntryDto> Leave, IReadOnlyList<DateOnly> Preferred,
    IReadOnlyList<ConfirmedLeaveDto>? Confirmed = null, bool Excluded = false);

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

/// <param name="Facility">The hospital the user works in (for a master: the one they picked, or null).</param>
public sealed record MeDto(Guid AccountId, string Email, AccountRole Role, Guid? PersonId, string? PersonName, bool MustChangePassword,
    FacilityRef? Facility);

/// <param name="WeekendDays">The state's weekly rest days (0 = Sunday ... 6 = Saturday).</param>
public sealed record FacilityRef(Guid Id, string Name, string Code, IReadOnlyList<int>? WeekendDays = null);

public sealed record SetFacilityRequest(Guid? FacilityId);

/// <param name="Configured">SMTP (or a pickup directory) is set up on the server.</param>
/// <param name="Enabled">The master's switch: when off, nothing is emailed (invites show the temporary password instead).</param>
public sealed record EmailSettingsDto(bool Configured, bool Enabled);

public sealed record EmailSettingsRequest(bool Enabled);

public sealed record FacilityDto(Guid Id, string Name, string Code, Guid DistrictId, string District, Guid StateId, string State,
    int Officers, int Accounts, string? CurrentPeriod, PeriodStatus? CurrentPeriodStatus);

public sealed record UpsertFacilityRequest(string Name, string Code, Guid DistrictId);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record AccountDto(
    Guid Id, string Email, AccountRole Role, Guid? PersonId, string? PersonName, bool Enabled, bool MustChangePassword,
    DateTimeOffset? TempPasswordExpiresAt, DateTimeOffset? LastLoginAt, Guid? FacilityId = null, string? FacilityCode = null);

/// <summary>A master account: masters belong to no hospital and are added on All hospitals → Accounts.</summary>
public sealed record MasterRequest(string Name, string Email, bool Enabled = true);

/// <summary>EmailSent = queued for sending. TempPassword is only returned when email isn't set up, so the admin can pass it on.</summary>
public sealed record InviteResult(Guid AccountId, bool EmailSent, string? TempPassword);


/// <param name="Email">Sign-in email; null only for officers from before sign-in and officers were one record ("email needed").</param>
/// <param name="BlockedWeekdays">Weekdays (0 = Sunday ... 6 = Saturday) the officer can never be on call.</param>
/// <param name="InvitePending">Invited but hasn't signed in and set their own password yet.</param>
/// <param name="AccountEnabled">Can sign in: has an email, has been invited and is enabled.</param>
public sealed record AdminPersonDto(
    Guid Id, string Code, string Name, OfficerStatus Status, string? StatusReason, DateOnly? ExcludedUntil,
    Guid? ClinicId, string? ClinicName, Guid? DistrictId, string? District, Guid? StateId, string? State, string? Phone, int SortOrder,
    bool ExtraShift, bool PreferWeekendHoliday, int WeekendWeight, string? Email, AccountRole Role, bool InvitePending,
    bool AccountEnabled, IReadOnlyList<int> BlockedWeekdays, string? BlockedReason, bool Enabled = true, bool Invited = false,
    DateTimeOffset? LastLoginAt = null);

/// <param name="Email">Required. Someone added, or given an email for the first time, is invited in the same step.</param>
/// <param name="Role">Officer or Admin (on the rota) or Supervisor (runs the admin pages, never on the rota).</param>
/// <param name="Enabled">Can sign in; switching it off signs them out straight away.</param>
public sealed record UpsertPersonRequest(
    string Name, string? Code, OfficerStatus Status = OfficerStatus.OnCall, bool ExtraShift = false, bool PreferWeekendHoliday = false,
    int WeekendWeight = 1, string? StatusReason = null, DateOnly? ExcludedUntil = null, Guid? ClinicId = null, string? Phone = null,
    string? Email = null, AccountRole Role = AccountRole.Officer, IReadOnlyList<int>? BlockedWeekdays = null, string? BlockedReason = null,
    bool Enabled = true);

/// <summary>Invite is set when the person was invited by this save.</summary>
public sealed record SavePersonResult(Guid Id, InviteResult? Invite);

/// <summary>Shifts done so far. Published* = from published rotas; the rest is the admin's adjustment.</summary>
public sealed record TallyRowDto(
    Guid PersonId, string Code, string Name, OfficerStatus Status, string? ClinicName, string? District,
    int Weekday, int WeekendHoliday, int Total, int PublishedWeekday, int PublishedWeekendHoliday, bool Known);

public sealed record TallyUpdate(Guid PersonId, int Weekday, int WeekendHoliday);

// State -> district -> clinic -> officer.

/// <param name="WeekendDays">The state's weekly rest days (0 = Sunday ... 6 = Saturday).</param>
public sealed record StateDto(Guid Id, string Name, IReadOnlyList<DistrictDto> Districts, IReadOnlyList<int>? WeekendDays = null);

public sealed record DistrictDto(Guid Id, string Name, int Clinics);

/// <param name="WeekendDays">Weekly rest days (0 = Sunday ... 6 = Saturday); null keeps the current ones (Sat + Sun for a new state).</param>
public sealed record UpsertStateRequest(string Name, IReadOnlyList<int>? WeekendDays = null);

public sealed record UpsertDistrictRequest(string Name, Guid StateId);

public sealed record ClinicDto(Guid Id, string Name, Guid DistrictId, string District, string State, int People);

public sealed record UpsertClinicRequest(string Name, Guid DistrictId);


/// <summary>CSV import of officers. Commit = false only checks the file; nothing is saved unless every row is valid.</summary>
public sealed record ImportPeopleRequest(string Csv, bool Commit);

/// <param name="Line">Line number in the file (the header is line 1).</param>
public sealed record ImportRowDto(
    int Line, string Name, string? Code, string? Email, AccountRole? Role, string? Phone, string? State, string? District,
    string? Clinic, IReadOnlyList<ImportErrorDto> Errors);

/// <summary>One problem in one cell of the file.</summary>
/// <param name="Column">The column's header as written in the file, e.g. "State" or "Negeri".</param>
/// <param name="Letter">The spreadsheet column letter (A, B, ...), so the cell is easy to find in Excel.</param>
public sealed record ImportErrorDto(string Column, string Letter, string Message);

/// <param name="Passwords">Temporary passwords of invites whose email couldn't be sent, for the admin to pass on.</param>
public sealed record ImportResultDto(
    IReadOnlyList<ImportRowDto> Rows, IReadOnlyList<string> FileErrors, bool Committed, int Added, int Invited,
    IReadOnlyList<IssuedPasswordDto> Passwords);

public sealed record IssuedPasswordDto(string Name, string Email, string TempPassword);

public sealed record ReorderRequest(IReadOnlyList<Guid> Ids);

public sealed record UpsertPeriodRequest(string Name, DateOnly StartDate, DateOnly EndDate, DateOnly? LeaveDeadline, int? PointsBudget = null,
    IReadOnlyList<Guid>? ExcludedPersonIds = null);

/// <summary>Leave limits for one officer in one period. Budget excludes Extra (admin top-up).</summary>
public sealed record LeaveRulesDto(
    int Budget, int Extra, string? ExtraReason, int WeekdayAllowance, int BusyDayCap, int WeekdayCap, int OnCall,
    int WeekendCost, int HolidayCost, int PeakCost, IReadOnlyList<HolidayDto> Peaks, IReadOnlyDictionary<DateOnly, int> OthersOff,
    int ConfirmedDays = 0, int PeriodDays = 0, int PointsDeducted = 0, int WeekdaysDeducted = 0,
    int Reward = 0, string? RewardFrom = null, string? RewardDetail = null,
    IReadOnlyDictionary<DateOnly, int>? DayCaps = null);

public sealed record PointsRowDto(
    Guid PersonId, string Code, string Name, int PointsUsed, int Budget, int Extra, string? Reason,
    int WeekdaysUsed, int WeekdayAllowance, int ConfirmedDays = 0, int PointsDeducted = 0, int WeekdaysDeducted = 0,
    int Reward = 0, string? RewardFrom = null, string? RewardDetail = null);

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

public sealed record UpsertLeaveApprovalRequest(Guid PersonId, DateOnly StartDate, DateOnly EndDate, string? Reason, string? Reference,
    LeaveKind? Kind = null);

/// <summary>Someone who could take a shift instead: on call, not on leave that day and not working the day before or after.</summary>
public sealed record ShiftCandidateDto(Guid PersonId, string Name, int Shifts);

/// <summary>A shift in a published or in-review rota that falls on the officer's confirmed leave.</summary>
public sealed record ApprovalConflictDto(
    Guid RunId, Guid PeriodId, string PeriodName, DateOnly Date, bool IsPublished, IReadOnlyList<ShiftCandidateDto> Candidates);

public sealed record LeaveApprovalDto(
    Guid Id, Guid PersonId, string PersonCode, string PersonName, DateOnly StartDate, DateOnly EndDate, string Reason,
    string? Reference, string? CreatedBy, DateTimeOffset CreatedAt, IReadOnlyList<ApprovalConflictDto> Conflicts,
    LeaveKind Kind = LeaveKind.Other, Guid? DocumentId = null, string? DocumentName = null);

/// <param name="Warnings">Days now over the leave limit, ordinary leave that became confirmed, and similar notes for the admin.</param>
public sealed record DateRangeRequest(DateOnly StartDate, DateOnly EndDate);
public sealed record BatchLeaveApprovalRequest(Guid PersonId, List<DateRangeRequest>? Ranges, string? Reason, string? Reference,
    LeaveKind? Kind = null);
public sealed record LeaveApprovalBatchResultDto(IReadOnlyList<LeaveApprovalDto> Approvals, IReadOnlyList<string> Warnings);
public sealed record LeaveApprovalResultDto(LeaveApprovalDto Approval, IReadOnlyList<string> Warnings);


/// <summary>One audit entry. Detail, Before and After are the JSON the change was logged with.</summary>
public sealed record AuditEntryDto(
    long Id, DateTimeOffset At, Guid? AccountId, string? ActorEmail, string? ActorName, string Action, string? Entity,
    string? EntityId, Guid? PersonId, string? PersonName, JsonElement? Detail, JsonElement? Before, JsonElement? After,
    string? Ip, string? UserAgent, string? FacilityCode = null);

public sealed record AuditPageDto(IReadOnlyList<AuditEntryDto> Items, int Total, int Page, int PageSize);

public sealed record AuditActorDto(Guid AccountId, string Email, string? Name);

/// <summary>What the audit log can be filtered by: who has made changes, and the actions and kinds of thing logged.</summary>
public sealed record AuditFiltersDto(IReadOnlyList<AuditActorDto> Actors, IReadOnlyList<string> Actions, IReadOnlyList<string> Entities);

/// <summary>Page is the app path the user was on (optional).</summary>
/// <param name="ToFacility">True: to the sender's hospital admins. False: about the app, to the masters.</param>
public sealed record SubmitFeedbackRequest(FeedbackCategory Category, string Message, string? Page, bool ToFacility = false);

public sealed record FeedbackDto(
    long Id, DateTimeOffset CreatedAt, FeedbackCategory Category, string Message, string? Page, Guid? AccountId,
    string? SenderEmail, string? SenderName, DateTimeOffset? ResolvedAt, string? ResolvedByEmail);

/// <summary>Open counts every unresolved item, whatever the filters, for the tab's badge.</summary>
public sealed record FeedbackPageDto(IReadOnlyList<FeedbackDto> Items, int Total, int Page, int PageSize, int Open);


/// <summary>One range in a leave request as the officer sends it. DocumentId is a finished upload (kind LeaveDocument) from /api/documents.</summary>
public sealed record LeaveRequestRangeInput(DateOnly StartDate, DateOnly EndDate, LeaveKind Kind, string? Reason, Guid? DocumentId = null);
public sealed record LeaveRequestInput(List<LeaveRequestRangeInput>? Ranges);

public sealed record LeaveRequestRangeDto(
    Guid Id, Guid RequestId, Guid PersonId, string PersonCode, string PersonName, DateOnly StartDate, DateOnly EndDate,
    LeaveKind Kind, string Reason, LeaveRequestStatus Status, Guid? DocumentId, string? DocumentName, long? DocumentSize, DateTimeOffset SubmittedAt,
    string? DecisionNote, string? DecidedBy, DateTimeOffset? DecidedAt, IReadOnlyList<string> Overlaps, bool AddedByAdmin = false);

public sealed record DecideLeaveRequestRequest(string? Note);
public sealed record DecideLeaveRequestResult(LeaveRequestRangeDto Range, IReadOnlyList<string> Warnings);

// ---------------- on-call case log ----------------

/// <param name="OnCallDate">The shift the case belongs to; default: the case date, or the day before when it starts before 08:00.</param>
/// <param name="TimeFinish">At or before TimeStart = the next day.</param>
/// <param name="OfficerId">The on-call officer in charge; default: the officer recording it.</param>
/// <param name="Taggers">Free text: the taggers with the officer.</param>
/// <param name="DsaName">The DSA on call (required).</param>
/// <param name="DsaStatus">CALLED AND ATTENDED, CALLED BUT FTA, CALLED BUT NO ANSWER or NOT BEEN CALLED (required).</param>
/// <param name="CaseType">TRAUMA (P/W) or NON-TRAUMA (C/O).</param>
/// <param name="Presentation">The P/W or C/O text.</param>
/// <param name="Investigations">Ix done, comma-separated.</param>
/// <param name="Prescription">Rx given.</param>
/// <param name="EdZone">GREEN, YELLOW or RED: required on the form when the referral is ED, dropped otherwise.</param>
public sealed record CaseInput(
    DateOnly Date, TimeOnly TimeStart, TimeOnly TimeFinish, DateOnly? OnCallDate, string PatientName, string PatientIdNo, string Gender,
    string? Age, string? Race, string? ReferralSource, string? Department, string? Diagnosis, string? Management, string? Plan,
    Guid? OfficerId, string? Taggers, string? DsaName, string? DsaStatus, bool Confidential,
    string? SpokenTo = null, string? SeenBy = null, string? CaseType = null, string? Presentation = null, string? Investigations = null,
    string? Prescription = null, string? EdZone = null);

/// <summary>Whoever the rota (published, or in review) has on call on a date (shown on ETAP rows that aren't the officer's).</summary>
public sealed record OnCallOfficerDto(Guid PersonId, string Name);

/// <param name="Minutes">Length of the case (over midnight when the finish is at or before the start).</param>
/// <param name="CanEdit">The signed-in user recorded it, or is an admin.</param>
/// <param name="CanShare">The signed-in user recorded it or is its officer in charge: they can make its WhatsApp passover.</param>
/// <param name="PersonId">The on-call officer in charge (Officer is their name).</param>
public sealed record CaseDto(
    Guid Id, Guid PersonId, string Officer, string RecordedBy, DateOnly OnCallDate, DateOnly Date, TimeOnly TimeStart, TimeOnly TimeFinish,
    int Minutes, string PatientName, string PatientIdNo, string Gender, string? Age, string? Race, string? ReferralSource, string? Department,
    string? Diagnosis, string? Management, string? Plan, string? Taggers, string? DsaName, string? DsaStatus, bool Confidential, bool CanEdit,
    bool CanShare, DateTimeOffset UpdatedAt, string? SpokenTo, string? SeenBy, string? CaseType, string? Presentation, string? Investigations,
    string? Prescription, string? EdZone);

/// <summary>The case as a WhatsApp passover message for the department group.</summary>
public sealed record CaseTextDto(string Text);

/// <param name="Csv">The Google Form's response sheet, exported as CSV.</param>
/// <param name="DefaultPersonId">Officer in charge for rows whose "officer & taggers" column names nobody on the rota.</param>
public sealed record CaseImportRequest(string Csv, bool Commit, Guid? DefaultPersonId);

/// <param name="Duplicate">Already in the log (same officer, date, start time and patient number): skipped.</param>
public sealed record CaseImportRowDto(int Line, DateOnly? Date, string? Officer, bool Duplicate, IReadOnlyList<ImportErrorDto> Errors);

public sealed record CaseImportResultDto(
    IReadOnlyList<CaseImportRowDto> Rows, IReadOnlyList<string> FileErrors, bool Committed, int Added, int Duplicates);

// ---------------- officer ETAP profile ----------------

/// <param name="NoTelefonBimbit">Mobile phone (also the officer's phone on the Officers tab).</param>
public sealed record ProfileInput(
    string? FullName, string? IcNo, string? Jawatan, string? Gred, DateOnly? TarikhPewartaanPakar, string? UnitTempatBertugas,
    string? AlamatRumah, decimal? JarakKm, string? NoGaji, string? NoTelefonPejabat, string? NoTelefonBimbit, string? NoAkaunBank,
    string? NamaCawanganBank);

/// <param name="Verified">Verified by the officer and unchanged since (phone and email included).</param>
/// <param name="Missing">Required fields still empty.</param>
public sealed record ProfileDto(
    string? FullName, string? IcNo, string? Jawatan, string? Gred, DateOnly? TarikhPewartaanPakar, string? UnitTempatBertugas,
    string? AlamatRumah, decimal? JarakKm, string? NoGaji, string? NoTelefonPejabat, string? NoTelefonBimbit, string? Email,
    string? NoAkaunBank, string? NamaCawanganBank, bool Verified, DateTimeOffset? VerifiedAt, IReadOnlyList<string> Missing);

public sealed record VerifyProfileRequest(string? Password, bool Confirm);

// ---------------- ETAP claim ----------------

/// <summary>One on-call day as the officer typed it: the on-call date and when the duty started and ended.</summary>
public sealed record EtapRowInput(DateOnly Date, DateOnly FromDate, TimeOnly FromTime, DateOnly ToDate, TimeOnly ToTime);

public enum EtapDayType { Working, Rest, Holiday }

/// <param name="CaseMinutes">Total length of the officer's cases on this on-call date (4 h or more = category 2).</param>
/// <param name="Category">2a / 2b (passive call melebihi 4 jam) or 3a / 3b (kurang 4 jam); a = working day, b = rest day or holiday.</param>
/// <param name="InRota">The published rota has this officer on call that date.</param>
/// <param name="RotaOfficer">When not: who the rota has instead.</param>
public sealed record EtapRowDto(
    DateOnly Date, DateOnly FromDate, TimeOnly FromTime, DateOnly ToDate, TimeOnly ToTime, int Minutes, EtapDayType DayType,
    string? HolidayName, int CaseMinutes, int CaseCount, string Category, bool InRota, OnCallOfficerDto? RotaOfficer);

public sealed record EtapLineDto(string Category, string Label, int Days, int Rate, int Amount);

/// <param name="Name">Null for a confidential patient (the form leaves name and registration number out).</param>
public sealed record EtapPatientDto(DateOnly Date, string? Name, string? RegNo, int Minutes, string? DiagnosisAndTreatment, string? Notes,
    bool Confidential);

/// <param name="ProfileVerified">The ETAP PDF needs a verified profile.</param>
/// <param name="Specialist">Specialist rates (the profile has a tarikh pewartaan pakar).</param>
/// <param name="RotaDates">The officer's on-call dates in the published rota this month.</param>
/// <param name="Saved">A draft is saved for this month.</param>
/// <param name="UnclaimedRotaRows">Rota dates not in the claim yet, with the usual times (for "use my rota dates").</param>
public sealed record EtapDto(
    string Month, bool ProfileVerified, bool Specialist, IReadOnlyList<EtapRowDto> Rows, IReadOnlyList<DateOnly> RotaDates,
    IReadOnlyList<EtapLineDto> Lines, int TotalDays, int TotalAmount, IReadOnlyList<EtapPatientDto> Patients, SuratArahanDto? SuratArahan,
    bool Saved, IReadOnlyList<EtapRowInput> UnclaimedRotaRows);

public sealed record EtapSaveRequest(IReadOnlyList<EtapRowInput> Rows);

public sealed record SuratArahanDto(Guid Id, Guid DocumentId, string FileName, DateOnly EffectiveFrom, string? Reference, DateTimeOffset CreatedAt);

/// <param name="DocumentId">An uploaded document of kind SuratArahan.</param>
public sealed record AddSuratArahanRequest(Guid DocumentId, DateOnly EffectiveFrom, string? Reference);
