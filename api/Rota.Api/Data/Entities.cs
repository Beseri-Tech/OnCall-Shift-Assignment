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

    /// <summary>Shifts done before this app (entered once by the admin). null = unknown.</summary>
    public int? OpeningTotal { get; set; }
    public int? OpeningWeekday { get; set; }
    public int? OpeningWeekendHoliday { get; set; }

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

public sealed class Clinic
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = "";

    /// <summary>Kawasan the clinic belongs to (e.g. Arau, Kangar).</summary>
    public string Area { get; set; } = "";
}

public enum PeriodStatus
{
    /// <summary>People can enter leave.</summary>
    Open,
    /// <summary>Leave frozen; admin generates drafts.</summary>
    Locked,
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

    public List<string> Warnings { get; set; } = [];

    public List<ShiftAssignment> Assignments { get; set; } = [];
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
    public Guid? PersonId { get; set; }
    public string Action { get; set; } = "";
    public string? Detail { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
}
