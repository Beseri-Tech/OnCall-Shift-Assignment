namespace Rota.Api.Data;

public sealed class Person
{
    /// <summary>Permanent ID; later linked to a personal login.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Short unique code shown to people (e.g. D001); admin may change it to a staff number.</summary>
    public string Code { get; set; } = "";

    public string Name { get; set; } = "";
    public OfficerStatus Status { get; set; } = OfficerStatus.OnCall;

    /// <summary>Why they are excluded / left (e.g. CUTI BERSALIN, PINDAH).</summary>
    public string? StatusReason { get; set; }

    /// <summary>Expected return for an Excluded officer; informational only (no automatic re-inclusion).</summary>
    public DateOnly? ExcludedUntil { get; set; }

    public Guid? ClinicId { get; set; }
    public Clinic? Clinic { get; set; }
    public string? Phone { get; set; }
    public int SortOrder { get; set; }

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
}

public sealed class Clinic
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = "";

    public Guid DistrictId { get; set; }
    public District? District { get; set; }
}

public enum AccountRole
{
    /// <summary>On-call officer; edits only their own leave.</summary>
    Officer,
    /// <summary>On-call officer who also runs the admin pages.</summary>
    Admin,
    /// <summary>Runs the admin pages but is not on the rota (no officer record).</summary>
    Supervisor,
}

/// <summary>A login. Officers and admins are linked to their officer record; supervisors are not.</summary>
public sealed class Account
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Stored lowercase.</summary>
    public string Email { get; set; } = "";

    public string PasswordHash { get; set; } = "";
    public AccountRole Role { get; set; }

    public Guid? PersonId { get; set; }
    public Person? Person { get; set; }

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
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsAdmin => Role is AccountRole.Admin or AccountRole.Supervisor;
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

public sealed class RotaPeriod
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = "";
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

public sealed class PublicHoliday
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>A busy weekday (e.g. the eve of Raya): leave on it costs points. Not a holiday for the rota itself.</summary>
public sealed class PeakDay
{
    public DateOnly Date { get; set; }
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

/// <summary>Trail of edits while anyone can edit anyone's leave (no personal logins yet).</summary>
public sealed class ChangeLog
{
    public long Id { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Who made the change (null for anonymous actions such as a failed password reset).</summary>
    public Guid? AccountId { get; set; }

    /// <summary>The officer the change is about.</summary>
    public Guid? PersonId { get; set; }
    public string Action { get; set; } = "";
    public string? Detail { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
}
