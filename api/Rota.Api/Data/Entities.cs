namespace Rota.Api.Data;

/// <summary>
/// Everyone in the app, with their login: officers and on-call admins (on the rota), supervisors (run a hospital's admin
/// pages, never on the rota) and masters (every hospital, no hospital of their own).
/// </summary>
public sealed class Person
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Short unique code shown to people (e.g. D001); admin may change it to a staff number.</summary>
    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>The hospital this person belongs to; null only for masters.</summary>
    public Guid? FacilityId { get; set; }

    public AccountRole Role { get; set; } = AccountRole.Officer;

    /// <summary>Sign-in email, stored lowercase. Required for anyone added or edited; null only for officers from before
    /// logins and officers were one record, who can't sign in until an admin adds it.</summary>
    public string? Email { get; set; }

    /// <summary>Null until they have been invited.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Can sign in (also needs a password and not being Left).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Set for invites and admin resets: the temporary password must be replaced at the next login.</summary>
    public bool MustChangePassword { get; set; }
    public DateTimeOffset? TempPasswordExpiresAt { get; set; }

    /// <summary>SHA-256 of the emailed reset token; the token itself is never stored.</summary>
    public string? ResetTokenHash { get; set; }
    public DateTimeOffset? ResetTokenExpiresAt { get; set; }

    /// <summary>Changes on password change, disable or role change; cookies with an old stamp are rejected.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>Officers and on-call admins are on the rota; supervisors and masters never are.</summary>
    public bool OnRota => Role is AccountRole.Officer or AccountRole.Admin;

    public bool IsAdmin => Role is AccountRole.Admin or AccountRole.Supervisor or AccountRole.Master;

    public OfficerStatus Status { get; set; } = OfficerStatus.OnCall;

    /// <summary>Why they are excluded / left (e.g. CUTI BERSALIN, PINDAH).</summary>
    public string? StatusReason { get; set; }

    /// <summary>Expected return for an Excluded officer; informational only (no automatic re-inclusion).</summary>
    public DateOnly? ExcludedUntil { get; set; }

    public Guid? ClinicId { get; set; }
    public Clinic? Clinic { get; set; }
    public string? Phone { get; set; }
    public int SortOrder { get; set; }

    /// <summary>
    /// Weekdays this officer can never be on call (e.g. works in another state where Sunday is a working day): a bitmask with
    /// bit n set for <see cref="DayOfWeek"/> n. Set by the admin; no leave points are involved. 0 = none.
    /// </summary>
    public int BlockedWeekdays { get; set; }

    public string? BlockedReason { get; set; }

    public bool ExtraShift { get; set; }
    public bool PreferWeekendHoliday { get; set; }
    public int WeekendWeight { get; set; } = 1;

    /// <summary>
    /// Admin corrections added to shifts from published rotas (history before the app, manual fixes).
    /// The tally page stores "typed value - published". null = unknown (group average is used for weekend balancing).
    /// </summary>
    public int? TallyWeekdayAdjust { get; set; }
    public int? TallyWeekendHolidayAdjust { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<LeaveDay> Leave { get; set; } = [];
    public List<PreferredDate> PreferredDates { get; set; } = [];
}

public enum OfficerStatus
{
    /// <summary>In the rota.</summary>
    OnCall,
    /// <summary>Temporarily out of the rota (maternity leave etc.); stays in the list.</summary>
    Excluded,
    /// <summary>Moved out / resigned; hidden by default, history kept.</summary>
    Left,
}

/// <summary>Top of the hierarchy: state -> district -> clinic -> officer.</summary>
public sealed class State
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = "";

    /// <summary>
    /// The state's weekly rest days (bit n = <see cref="DayOfWeek"/> n): Sat + Sun in most states, Fri + Sat in Kedah, Kelantan and
    /// Terengganu. Used for the ETAP day type (working day vs rest day).
    /// </summary>
    public int WeekendDays { get; set; } = (1 << (int)DayOfWeek.Saturday) | (1 << (int)DayOfWeek.Sunday);

    public List<District> Districts { get; set; } = [];
}

/// <summary>Daerah within a state (e.g. Kangar, Arau in Perlis).</summary>
public sealed class District
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid StateId { get; set; }
    public State? State { get; set; }
    public string Name { get; set; } = "";

    public List<Clinic> Clinics { get; set; } = [];
    public List<Facility> Facilities { get; set; } = [];
}

/// <summary>A hospital that runs its own on-call rota. Officers, periods and (non-master) accounts belong to one.</summary>
public sealed class Facility
{
    /// <summary>Hospital Tuanku Fauziah, Perlis: where all data before multi-facility support belongs.</summary>
    public static readonly Guid HtfId = Guid.Parse("01999a00-0000-7000-8000-0000000000f1");

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = "";

    /// <summary>Short code shown in the app, e.g. HTF.</summary>
    public string Code { get; set; } = "";

    public Guid DistrictId { get; set; }
    public District? District { get; set; }
}

public sealed class Clinic
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = "";

    public Guid DistrictId { get; set; }
    public District? District { get; set; }
}

/// <summary>
/// A date nobody from the clinic can be on call (e.g. a clinic event). Like blocked weekdays it behaves as unavailability for the
/// rota but costs no leave points and doesn't count toward anyone's daily limit.
/// </summary>
public sealed class ClinicBlockedDay
{
    public Guid ClinicId { get; set; }
    public DateOnly Date { get; set; }
    public string? Reason { get; set; }
}

public enum AccountRole
{
    /// <summary>On-call officer; edits only their own leave.</summary>
    Officer,
    /// <summary>On-call officer who also runs the admin pages.</summary>
    Admin,
    /// <summary>Runs the hospital's admin pages but is never on the rota.</summary>
    Supervisor,
    /// <summary>Sees and runs every facility; not bound to one and not on any rota.</summary>
    Master,
}

public enum PeriodStatus
{
    /// <summary>People can enter leave.</summary>
    Open,
    /// <summary>Leave frozen; admin generates drafts.</summary>
    Locked,
    /// <summary>A draft is shown to officers, who can swap dates before it is published.</summary>
    Review,
    /// <summary>A rota run is published and counts towards totals.</summary>
    Published,
}

/// <summary>An officer the admin left out of one rota period: no shifts are generated for them in it.</summary>
public sealed class PeriodExclusion
{
    public Guid PeriodId { get; set; }
    public Guid PersonId { get; set; }
}

public sealed class RotaPeriod
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = "";
    public Guid FacilityId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>Last day people can edit leave (inclusive, local time). null = until locked.</summary>
    public DateOnly? LeaveDeadline { get; set; }

    /// <summary>Last day officers can request swaps while the rota is in review (inclusive). null = until published.</summary>
    public DateOnly? SwapDeadline { get; set; }

    public PeriodStatus Status { get; set; } = PeriodStatus.Open;

    /// <summary>Leave points per officer; null = automatic (share of weekend/public holiday days).</summary>
    public int? PointsBudget { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<RotaRun> Runs { get; set; } = [];
}

/// <summary>
/// Master list for one state (state holidays differ): copied into new periods of that state's hospitals as
/// <see cref="PeriodDay"/>s; the rota itself uses the period's days.
/// </summary>
public sealed class PublicHoliday
{
    public Guid StateId { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>
/// Master list of busy weekdays (e.g. the eve of Raya): leave on it costs points. Not a holiday for the rota itself.
/// Copied into new periods as <see cref="PeriodDay"/>s.
/// </summary>
public sealed class PeakDay
{
    public Guid StateId { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = "";
}

public enum PeriodDayKind
{
    /// <summary>Counts as a weekend shift when balancing the rota.</summary>
    Holiday,
    /// <summary>Leave on it costs points; not a holiday for the rota itself.</summary>
    Peak,
}

/// <summary>
/// A public holiday or peak day applied to one rota period. A new period starts with the master lists
/// (<see cref="PublicHoliday"/>, <see cref="PeakDay"/>) in its date range; the admin can then add or remove days
/// for that period alone. Always inside the period's dates.
/// </summary>
public sealed class PeriodDay
{
    public Guid PeriodId { get; set; }
    public DateOnly Date { get; set; }
    public PeriodDayKind Kind { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>Extra leave points the admin gives one officer for one period (outstation, family matters, ...).</summary>
public sealed class PointGrant
{
    public Guid PeriodId { get; set; }
    public Guid PersonId { get; set; }
    public int Points { get; set; }
    public string? Reason { get; set; }
}

public sealed class LeaveDay
{
    public Guid PersonId { get; set; }
    public DateOnly Date { get; set; }
    public string? Note { get; set; }

    /// <summary>Set for confirmed leave: the approval this day belongs to. Only admins change these days.</summary>
    public Guid? ApprovalId { get; set; }
}

/// <summary>
/// Confirmed leave: an absence the department has already approved (e.g. an overseas trip), entered by an admin.
/// Its days are ordinary <see cref="LeaveDay"/> rows linked by ApprovalId, so the rota, swaps and overrides treat them as
/// leave. They cost no points and are never refused, but they reduce the officer's points and weekday allowance by the
/// share of the period they cover, and they count toward a day being full for other officers.
/// </summary>
public sealed class LeaveApproval
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid PersonId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = "";

    /// <summary>Approval letter or memo number, if any.</summary>
    public string? Reference { get; set; }

    /// <summary>Why the officer is away (trip, CR, relief duty...). Every kind keeps the officer off call.</summary>
    public LeaveKind Kind { get; set; } = LeaveKind.Other;

    public Guid? CreatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum LeaveKind { Trip, Cr, Relief, Other }

/// <summary>Removed: approved, then the admin removed the confirmed leave.</summary>
public enum LeaveRequestStatus { Pending, Approved, Rejected, Cancelled, Removed }

/// <summary>An officer's request for confirmed leave: one or more date ranges, each with its own reason and optional document.</summary>
public sealed class LeaveRequest
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid PersonId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<LeaveRequestRange> Ranges { get; set; } = [];
}

/// <summary>One date range of a request. The admin approves or rejects each range on its own.</summary>
public sealed class LeaveRequestRange
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RequestId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public LeaveKind Kind { get; set; }
    public string Reason { get; set; } = "";
    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Pending;

    /// <summary>The supporting document (a stored <see cref="DocumentRecord"/> attached to this range), if the officer added one.</summary>
    public Guid? DocumentId { get; set; }

    public string? DecisionNote { get; set; }
    public Guid? DecidedByAccountId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }

    /// <summary>The confirmed leave created when the range was approved (cleared if the admin later removes it).</summary>
    public Guid? ApprovalId { get; set; }
}

public sealed class PreferredDate
{
    public Guid PersonId { get; set; }
    public DateOnly Date { get; set; }
}

public sealed class RotaRun
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid PeriodId { get; set; }
    public RotaPeriod? Period { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int Seed { get; set; }
    public double Objective { get; set; }

    public bool IsPublished { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Shown to officers for review (swaps allowed); at most one per period.</summary>
    public bool IsInReview { get; set; }

    public List<string> Warnings { get; set; } = [];

    public List<ShiftAssignment> Assignments { get; set; } = [];
}

public enum SwapStatus { Pending, Accepted, Declined, Cancelled, Expired }

/// <summary>During review: FromPerson offers their FromDate for ToPerson's ToDate. Accepting swaps the two shifts.</summary>
public sealed class SwapRequest
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RunId { get; set; }
    public Guid FromPersonId { get; set; }
    public DateOnly FromDate { get; set; }
    public Guid ToPersonId { get; set; }
    public DateOnly ToDate { get; set; }
    public string? Note { get; set; }
    public SwapStatus Status { get; set; } = SwapStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RespondedAt { get; set; }
}

/// <summary>An email waiting to be sent (or sent / given up), processed by the Mailer background service.</summary>
public sealed class OutboxEmail
{
    public long Id { get; set; }
    public string To { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";

    /// <summary>HTML version, sent alongside the plain-text Body when present.</summary>
    public string? Html { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Not sent before this time (retry backoff).</summary>
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;

    public int Attempts { get; set; }
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>Set when retries are exhausted or the failure is permanent; the email is not tried again.</summary>
    public DateTimeOffset? FailedAt { get; set; }

    public string? LastError { get; set; }
}

/// <summary>In-app notification for one account (some are also emailed).</summary>
public sealed class Notification
{
    public long Id { get; set; }
    public Guid AccountId { get; set; }
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Body { get; set; }

    /// <summary>App path to open, e.g. /my-oncall.</summary>
    public string? Link { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
}

public sealed class ShiftAssignment
{
    public Guid RunId { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>null = nobody could be assigned.</summary>
    public Guid? PersonId { get; set; }
    public Person? Person { get; set; }

    /// <summary>Fixed when the run is created so later holiday edits don't rewrite history.</summary>
    public bool IsWeekendHoliday { get; set; }

    public bool IsManual { get; set; }
}

/// <summary>Audit trail: who changed what and when, with the values before and after where there are any.</summary>
public sealed class ChangeLog
{
    public long Id { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The hospital the change happened in; null for changes outside any (e.g. a master adding a hospital).</summary>
    public Guid? FacilityId { get; set; }

    /// <summary>Who made the change (null for anonymous actions such as a failed password reset).</summary>
    public Guid? AccountId { get; set; }

    /// <summary>The officer the change is about.</summary>
    public Guid? PersonId { get; set; }
    public string Action { get; set; } = "";

    /// <summary>What kind of thing changed (person, clinic, period, run, ...): the action up to its first dot.</summary>
    public string? Entity { get; set; }

    /// <summary>Which one, e.g. its id or a date.</summary>
    public string? EntityId { get; set; }

    /// <summary>Extra context (JSON), e.g. the period a change belongs to.</summary>
    public string? Detail { get; set; }

    /// <summary>The changed thing's values before and after (JSON); null when it was created or deleted.</summary>
    public string? Before { get; set; }
    public string? After { get; set; }

    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
}

/// <summary>An app-wide setting a master changes at runtime (overrides the config default once set).</summary>
public sealed class AppSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

/// <summary>
/// One patient seen on call (the "Buku Oncall"): replaces the Google Form. Everyone in the hospital can read the log; the
/// recorder and admins change it. Patient details never go into URLs, notifications, emails or the audit log.
/// </summary>
public sealed class OnCallCase
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid FacilityId { get; set; }

    /// <summary>The on-call officer in charge (the case counts for them, e.g. on their ETAP): by default whoever the rota has on call.</summary>
    public Guid PersonId { get; set; }

    /// <summary>Who typed it in (usually the officer themself).</summary>
    public Guid RecordedById { get; set; }

    /// <summary>The on-call shift it belongs to: a case at 02:00 on the 6th is part of the 5th's shift.</summary>
    public DateOnly OnCallDate { get; set; }

    public DateOnly Date { get; set; }
    public TimeOnly TimeStart { get; set; }

    /// <summary>Earlier than (or equal to) TimeStart = the next day.</summary>
    public TimeOnly TimeFinish { get; set; }

    public string PatientName { get; set; } = "";

    /// <summary>MyKad (no dashes), KID, passport or registration number.</summary>
    public string PatientIdNo { get; set; } = "";

    public string Gender { get; set; } = "";
    public string? Age { get; set; }
    public string? Race { get; set; }
    public string? ReferralSource { get; set; }
    public string? Department { get; set; }
    public string? Diagnosis { get; set; }
    public string? Management { get; set; }
    public string? Plan { get; set; }

    /// <summary>The taggers (housemen / trainees) with the officer, as typed.</summary>
    public string? Taggers { get; set; }

    /// <summary>The dental surgery assistant on call.</summary>
    public string? DsaName { get; set; }

    /// <summary>CALLED AND ATTENDED, CALLED BUT FTA, CALLED BUT NO ANSWER or NOT BEEN CALLED.</summary>
    public string? DsaStatus { get; set; }

    /// <summary>Patient details are confidential: the ETAP form leaves out the name and registration number.</summary>
    public bool Confidential { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Removed (kept for the audit trail, never shown).</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Length of the case; a finish at or before the start runs past midnight.</summary>
    public int Minutes => Duration(TimeStart, TimeFinish);

    public static int Duration(TimeOnly start, TimeOnly finish)
    {
        int m = (int)(finish - start).TotalMinutes;   // TimeOnly subtraction wraps around midnight
        return m == 0 ? 24 * 60 : m;
    }
}

/// <summary>
/// The personal details an officer's ETAP claim form needs (section A). Only the officer reads or changes it. They verify
/// it themselves in the app; any later change (including their phone or email) makes it unverified until they verify again,
/// and the ETAP form can't be made from an unverified profile.
/// </summary>
public sealed class OfficerProfile
{
    public Guid PersonId { get; set; }

    /// <summary>Full name as in the IC.</summary>
    public string? FullName { get; set; }
    public string? IcNo { get; set; }
    public string? Jawatan { get; set; }
    public string? Gred { get; set; }

    /// <summary>Gazetted as a specialist on: specialist rates apply.</summary>
    public DateOnly? TarikhPewartaanPakar { get; set; }

    public string? UnitTempatBertugas { get; set; }
    public string? AlamatRumah { get; set; }
    public decimal? JarakKm { get; set; }
    public string? NoGaji { get; set; }
    public string? NoTelefonPejabat { get; set; }
    public string? NoAkaunBank { get; set; }
    public string? NamaCawanganBank { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? VerifiedAt { get; set; }

    /// <summary>SHA-256 of exactly what was verified (these fields + phone + email): differs as soon as anything changes.</summary>
    public string? VerifiedHash { get; set; }
}

/// <summary>A surat arahan uploaded by an admin. An ETAP claim for a month uses the latest one effective by the end of that month.</summary>
public sealed class SuratArahan
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid FacilityId { get; set; }
    public Guid DocumentId { get; set; }
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>The letter's reference number, if any.</summary>
    public string? Reference { get; set; }

    public Guid? CreatedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>An officer's ETAP claim for one month, kept as a draft they come back to. Rows hold what they typed.</summary>
public sealed class EtapClaim
{
    public Guid PersonId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>JSON array of <c>EtapRowInput</c>.</summary>
    public string Rows { get; set; } = "[]";

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum FeedbackCategory { Bug, Suggestion, Other }

/// <summary>Feedback about the app from a signed-in user; admins read it and mark it resolved.</summary>
public sealed class Feedback
{
    public long Id { get; set; }

    /// <summary>Who sent it; null once their account is gone.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Set when sent to the sender's hospital admins; null = about the app (read by masters).</summary>
    public Guid? FacilityId { get; set; }

    public FeedbackCategory Category { get; set; }
    public string Message { get; set; } = "";

    /// <summary>The app page they were on when they sent it, e.g. /rota.</summary>
    public string? Page { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedByAccountId { get; set; }
}

public enum DocumentKind
{
    /// <summary>The user manual PDF: admins upload it, every signed-in user can read it.</summary>
    Manual,
    /// <summary>A supporting document an officer attaches to a confirmed-leave request: the officer and admins can read it.</summary>
    LeaveDocument,
    /// <summary>Anything else an officer uploads: the uploader and admins can read it.</summary>
    Other,
    /// <summary>The PDF report of a published rota, saved when it is published. Admins only.</summary>
    RotaReport,
    /// <summary>The hospital's surat arahan (on-call instruction letter): admins upload it, everyone in the hospital reads it.</summary>
    SuratArahan,
}

public enum DocumentStatus
{
    /// <summary>Upload link issued; the file has not been confirmed. Never downloadable.</summary>
    Pending,
    /// <summary>Checked by the server and downloadable.</summary>
    Available,
    /// <summary>Removed (or rejected on upload); the bytes are deleted from R2.</summary>
    Deleted,
}

/// <summary>Metadata for a file kept in the private R2 bucket. The bytes never touch the database; presigned links are never stored.</summary>
public sealed class DocumentRecord
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DocumentKind Kind { get; set; }

    /// <summary>The hospital the document belongs to; null for the user manual (shared by all).</summary>
    public Guid? FacilityId { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    /// <summary>Server-generated R2 key. Quarantine key while Pending, then a new permanent key once checked.</summary>
    public string ObjectKey { get; set; } = "";

    /// <summary>Display name only; never used as a key.</summary>
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }

    public Guid? UploadedByAccountId { get; set; }

    /// <summary>The officer the document belongs to (they can read and delete it), if the uploader has one.</summary>
    public Guid? OwnerPersonId { get; set; }

    /// <summary>What the document is attached to, e.g. "ConfirmedLeaveRequest" and its id. Null until attached.</summary>
    public string? ParentType { get; set; }
    public string? ParentId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UploadedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
