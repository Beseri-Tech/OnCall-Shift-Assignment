using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Rota.Api.Auth;
using Rota.Api.Endpoints;

namespace Rota.Api.Data;

/// <summary>
/// Every query is scoped to the signed-in user's hospital (<see cref="FacilityId"/>) by global query filters, so an endpoint
/// can't show another hospital's data by forgetting a Where. No facility (a master who hasn't picked one, login, background
/// services) = no filter; every non-master account has a facility, so only masters and system code see across hospitals.
/// </summary>
public sealed class RotaDbContext(DbContextOptions<RotaDbContext> options, IHttpContextAccessor? http = null)
    : DbContext(options), IDataProtectionKeyContext
{
    /// <summary>The hospital this request works in, from the login cookie.</summary>
    public Guid? FacilityId => http?.HttpContext?.User.FacilityId();

    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<State> States => Set<State>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<ClinicBlockedDay> ClinicBlockedDays => Set<ClinicBlockedDay>();
    public DbSet<RotaPeriod> Periods => Set<RotaPeriod>();
    public DbSet<PublicHoliday> Holidays => Set<PublicHoliday>();
    public DbSet<PeakDay> PeakDays => Set<PeakDay>();
    public DbSet<PeriodDay> PeriodDays => Set<PeriodDay>();
    public DbSet<PeriodExclusion> PeriodExclusions => Set<PeriodExclusion>();
    public DbSet<PointGrant> PointGrants => Set<PointGrant>();
    public DbSet<LeaveDay> LeaveDays => Set<LeaveDay>();
    public DbSet<LeaveApproval> LeaveApprovals => Set<LeaveApproval>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveRequestRange> LeaveRequestRanges => Set<LeaveRequestRange>();
    public DbSet<PreferredDate> PreferredDates => Set<PreferredDate>();
    public DbSet<RotaRun> Runs => Set<RotaRun>();
    public DbSet<ShiftAssignment> Assignments => Set<ShiftAssignment>();
    public DbSet<ChangeLog> ChangeLog => Set<ChangeLog>();
    public DbSet<SwapRequest> SwapRequests => Set<SwapRequest>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OutboxEmail> OutboxEmails => Set<OutboxEmail>();
    public DbSet<Feedback> Feedback => Set<Feedback>();
    public DbSet<DocumentRecord> Documents => Set<DocumentRecord>();

    /// <summary>Keys that encrypt the admin cookie; stored here so logins survive redeploys.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Person>(e =>
        {
            e.ToTable("people");
            e.HasIndex(p => new { p.FacilityId, p.Code }).IsUnique();
            e.HasIndex(p => new { p.FacilityId, p.Name }).IsUnique();
            e.HasOne<Facility>().WithMany().HasForeignKey(p => p.FacilityId).OnDelete(DeleteBehavior.Restrict);
            e.Property(p => p.Code).HasMaxLength(32);
            e.Property(p => p.Name).HasMaxLength(200);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(p => p.StatusReason).HasMaxLength(200);
            e.Property(p => p.Phone).HasMaxLength(32);
            e.Property(p => p.BlockedReason).HasMaxLength(200);
            e.HasOne(p => p.Clinic).WithMany().HasForeignKey(p => p.ClinicId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(p => p.Leave).WithOne().HasForeignKey(l => l.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.PreferredDates).WithOne().HasForeignKey(p => p.PersonId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Account>(e =>
        {
            e.ToTable("accounts");
            e.HasIndex(a => a.Email).IsUnique();
            e.HasIndex(a => a.PersonId).IsUnique();
            e.Property(a => a.Email).HasMaxLength(254);
            e.Property(a => a.Role).HasConversion<string>().HasMaxLength(16);
            e.Property(a => a.SecurityStamp).HasMaxLength(64);
            e.Property(a => a.ResetTokenHash).HasMaxLength(64);
            e.HasOne(a => a.Person).WithMany().HasForeignKey(a => a.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(a => a.IsAdmin);
            e.HasOne<Facility>().WithMany().HasForeignKey(a => a.FacilityId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_accounts_facility", "\"Role\" = 'Master' OR \"FacilityId\" IS NOT NULL"));
        });

        b.Entity<State>(e =>
        {
            e.ToTable("states");
            e.HasIndex(s => s.Name).IsUnique();
            e.Property(s => s.Name).HasMaxLength(50);
            e.HasMany(s => s.Districts).WithOne(d => d.State).HasForeignKey(d => d.StateId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<District>(e =>
        {
            e.ToTable("districts");
            e.HasIndex(d => new { d.StateId, d.Name }).IsUnique();
            e.Property(d => d.Name).HasMaxLength(50);
            e.HasMany(d => d.Clinics).WithOne(c => c.District).HasForeignKey(c => c.DistrictId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Clinic>(e =>
        {
            e.ToTable("clinics");
            e.HasIndex(c => c.Name).IsUnique();
            e.Property(c => c.Name).HasMaxLength(100);
        });

        b.Entity<ClinicBlockedDay>(e =>
        {
            e.ToTable("clinic_blocked_days");
            e.HasKey(d => new { d.ClinicId, d.Date });
            e.HasIndex(d => d.Date);
            e.Property(d => d.Reason).HasMaxLength(200);
            e.HasOne<Clinic>().WithMany().HasForeignKey(d => d.ClinicId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RotaPeriod>(e =>
        {
            e.ToTable("rota_periods");
            e.Property(p => p.Name).HasMaxLength(100);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(p => p.FacilityId);
            e.HasOne<Facility>().WithMany().HasForeignKey(p => p.FacilityId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(p => p.Runs).WithOne(r => r.Period).HasForeignKey(r => r.PeriodId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PublicHoliday>(e =>
        {
            e.ToTable("public_holidays");
            e.HasKey(h => new { h.StateId, h.Date });
            e.HasOne<State>().WithMany().HasForeignKey(h => h.StateId).OnDelete(DeleteBehavior.Cascade);
            e.Property(h => h.Name).HasMaxLength(100);
        });

        b.Entity<PeakDay>(e =>
        {
            e.ToTable("peak_days");
            e.HasKey(h => new { h.StateId, h.Date });
            e.HasOne<State>().WithMany().HasForeignKey(h => h.StateId).OnDelete(DeleteBehavior.Cascade);
            e.Property(h => h.Name).HasMaxLength(100);
        });

        b.Entity<PeriodDay>(e =>
        {
            e.ToTable("period_days");
            e.HasKey(d => new { d.PeriodId, d.Kind, d.Date });
            e.Property(d => d.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(d => d.Name).HasMaxLength(100);
            e.HasOne<RotaPeriod>().WithMany().HasForeignKey(d => d.PeriodId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PeriodExclusion>(e =>
        {
            e.ToTable("period_exclusions");
            e.HasKey(x => new { x.PeriodId, x.PersonId });
            e.HasOne<RotaPeriod>().WithMany().HasForeignKey(x => x.PeriodId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PointGrant>(e =>
        {
            e.ToTable("point_grants");
            e.HasKey(g => new { g.PeriodId, g.PersonId });
            e.Property(g => g.Reason).HasMaxLength(200);
            e.HasOne<RotaPeriod>().WithMany().HasForeignKey(g => g.PeriodId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Person>().WithMany().HasForeignKey(g => g.PersonId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LeaveDay>(e =>
        {
            e.ToTable("leave_days");
            e.HasKey(l => new { l.PersonId, l.Date });
            e.HasIndex(l => l.Date);
            e.Property(l => l.Note).HasMaxLength(200);
            e.HasIndex(l => l.ApprovalId);
            e.HasOne<LeaveApproval>().WithMany().HasForeignKey(l => l.ApprovalId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LeaveApproval>(e =>
        {
            e.ToTable("leave_approvals");
            e.HasIndex(a => new { a.PersonId, a.StartDate });
            e.Property(a => a.Reason).HasMaxLength(200);
            e.Property(a => a.Reference).HasMaxLength(100);
            e.Property(a => a.Kind).HasConversion<string>().HasMaxLength(16);
            e.HasOne<Person>().WithMany().HasForeignKey(a => a.PersonId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LeaveRequest>(e =>
        {
            e.ToTable("leave_requests");
            e.HasIndex(r => new { r.PersonId, r.CreatedAt });
            e.HasOne<Person>().WithMany().HasForeignKey(r => r.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(r => r.Ranges).WithOne().HasForeignKey(r => r.RequestId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LeaveRequestRange>(e =>
        {
            e.ToTable("leave_request_ranges");
            e.HasIndex(r => r.Status);
            e.Property(r => r.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.Reason).HasMaxLength(200);
            e.Property(r => r.DecisionNote).HasMaxLength(200);
        });

        b.Entity<PreferredDate>(e =>
        {
            e.ToTable("preferred_dates");
            e.HasKey(p => new { p.PersonId, p.Date });
        });

        b.Entity<RotaRun>(e =>
        {
            e.ToTable("rota_runs");
            // At most one published run per period.
            e.HasIndex(r => r.PeriodId, "ix_rota_runs_one_published_per_period").IsUnique().HasFilter("\"IsPublished\"");
            e.HasIndex(r => r.PeriodId, "ix_rota_runs_one_in_review_per_period").IsUnique().HasFilter("\"IsInReview\"");
            e.HasMany(r => r.Assignments).WithOne().HasForeignKey(a => a.RunId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ShiftAssignment>(e =>
        {
            e.ToTable("shift_assignments");
            e.HasKey(a => new { a.RunId, a.Date });
            e.HasIndex(a => a.PersonId);
            e.HasOne(a => a.Person).WithMany().HasForeignKey(a => a.PersonId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<SwapRequest>(e =>
        {
            e.ToTable("swap_requests");
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(s => s.Note).HasMaxLength(200);
            e.HasIndex(s => new { s.RunId, s.Status });
            e.HasOne<RotaRun>().WithMany().HasForeignKey(s => s.RunId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Person>().WithMany().HasForeignKey(s => s.FromPersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Person>().WithMany().HasForeignKey(s => s.ToPersonId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<OutboxEmail>(e =>
        {
            e.ToTable("outbox_emails");
            e.Property(o => o.To).HasMaxLength(254);
            e.Property(o => o.Subject).HasMaxLength(200);
            e.Property(o => o.LastError).HasMaxLength(500);
            // The sender only looks at unsent mail; SentAt also feeds the rolling daily cap.
            e.HasIndex(o => o.NextAttemptAt).HasFilter("\"SentAt\" IS NULL AND \"FailedAt\" IS NULL");
            e.HasIndex(o => o.SentAt);
        });

        b.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.Property(n => n.Kind).HasMaxLength(32);
            e.Property(n => n.Title).HasMaxLength(200);
            e.Property(n => n.Body).HasMaxLength(1000);
            e.Property(n => n.Link).HasMaxLength(200);
            e.HasIndex(n => new { n.AccountId, n.CreatedAt });
            e.HasOne<Account>().WithMany().HasForeignKey(n => n.AccountId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Feedback>(e =>
        {
            e.ToTable("feedback");
            e.Property(f => f.Category).HasConversion<string>().HasMaxLength(16);
            e.Property(f => f.Message).HasMaxLength(FeedbackEndpoints.MaxMessageLength);
            e.Property(f => f.Page).HasMaxLength(200);
            e.HasIndex(f => f.CreatedAt);
            e.HasIndex(f => f.FacilityId);
            e.HasOne<Facility>().WithMany().HasForeignKey(f => f.FacilityId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Account>().WithMany().HasForeignKey(f => f.AccountId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Account>().WithMany().HasForeignKey(f => f.ResolvedByAccountId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<DocumentRecord>(e =>
        {
            e.ToTable("documents");
            e.Property(d => d.Kind).HasConversion<string>().HasMaxLength(24);
            e.Property(d => d.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(d => d.ObjectKey).HasMaxLength(200);
            e.Property(d => d.FileName).HasMaxLength(200);
            e.Property(d => d.ContentType).HasMaxLength(100);
            e.Property(d => d.ParentType).HasMaxLength(40);
            e.Property(d => d.ParentId).HasMaxLength(64);
            e.HasIndex(d => d.ObjectKey).IsUnique();
            e.HasIndex(d => new { d.ParentType, d.ParentId });
            e.HasIndex(d => new { d.Kind, d.Status });
            e.HasIndex(d => d.FacilityId);
            e.HasOne<Account>().WithMany().HasForeignKey(d => d.UploadedByAccountId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Person>().WithMany().HasForeignKey(d => d.OwnerPersonId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<DataProtectionKey>().ToTable("data_protection_keys");

        b.Entity<Facility>(e =>
        {
            e.ToTable("facilities");
            e.HasIndex(f => f.Code).IsUnique();
            e.HasIndex(f => f.Name).IsUnique();
            e.Property(f => f.Name).HasMaxLength(100);
            e.Property(f => f.Code).HasMaxLength(16);
            e.HasOne(f => f.District).WithMany(d => d.Facilities).HasForeignKey(f => f.DistrictId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AppSetting>(e =>
        {
            e.ToTable("app_settings");
            e.HasKey(s => s.Key);
            e.Property(s => s.Key).HasMaxLength(64);
            e.Property(s => s.Value).HasMaxLength(200);
        });

        FacilityFilters(b);

        b.Entity<ChangeLog>(e =>
        {
            e.ToTable("change_log");
            e.Property(c => c.Action).HasMaxLength(64);
            e.Property(c => c.Entity).HasMaxLength(32);
            e.Property(c => c.EntityId).HasMaxLength(64);
            e.Property(c => c.Detail).HasColumnType("jsonb");
            e.Property(c => c.Before).HasColumnType("jsonb");
            e.Property(c => c.After).HasColumnType("jsonb");
            e.HasIndex(c => c.At);
            e.HasIndex(c => new { c.Entity, c.At });
            e.HasIndex(c => new { c.AccountId, c.At });
            e.HasIndex(c => new { c.PersonId, c.At });
            e.HasIndex(c => new { c.FacilityId, c.At });
        });
    }

    private void FacilityFilters(ModelBuilder b)
    {
        // EF reads FacilityId afresh for every query. Children are scoped through their officer or period, so a lookup by
        // their own id (a run, a leave approval, a swap...) can't reach another hospital either.
        b.Entity<Person>().HasQueryFilter(p => FacilityId == null || p.FacilityId == FacilityId);
        b.Entity<RotaPeriod>().HasQueryFilter(p => FacilityId == null || p.FacilityId == FacilityId);
        b.Entity<Account>().HasQueryFilter(a => FacilityId == null || a.FacilityId == FacilityId);
        b.Entity<ChangeLog>().HasQueryFilter(c => FacilityId == null || c.FacilityId == FacilityId);
        b.Entity<Feedback>().HasQueryFilter(f => FacilityId == null || f.FacilityId == FacilityId);
        b.Entity<DocumentRecord>().HasQueryFilter(d => FacilityId == null || d.FacilityId == null || d.FacilityId == FacilityId);

        b.Entity<PublicHoliday>().HasQueryFilter(h => FacilityId == null ||
            Set<Facility>().Any(f => f.Id == FacilityId && f.District!.StateId == h.StateId));
        b.Entity<PeakDay>().HasQueryFilter(h => FacilityId == null ||
            Set<Facility>().Any(f => f.Id == FacilityId && f.District!.StateId == h.StateId));

        b.Entity<RotaRun>().HasQueryFilter(r => FacilityId == null ||
            Set<RotaPeriod>().Any(p => p.Id == r.PeriodId && p.FacilityId == FacilityId));
        b.Entity<ShiftAssignment>().HasQueryFilter(a => FacilityId == null ||
            Set<RotaRun>().Any(r => r.Id == a.RunId && Set<RotaPeriod>().Any(p => p.Id == r.PeriodId && p.FacilityId == FacilityId)));
        b.Entity<PeriodDay>().HasQueryFilter(d => FacilityId == null ||
            Set<RotaPeriod>().Any(p => p.Id == d.PeriodId && p.FacilityId == FacilityId));
        b.Entity<PeriodExclusion>().HasQueryFilter(x => FacilityId == null ||
            Set<Person>().Any(p => p.Id == x.PersonId && p.FacilityId == FacilityId));
        b.Entity<PointGrant>().HasQueryFilter(x => FacilityId == null ||
            Set<Person>().Any(p => p.Id == x.PersonId && p.FacilityId == FacilityId));
        b.Entity<LeaveDay>().HasQueryFilter(x => FacilityId == null ||
            Set<Person>().Any(p => p.Id == x.PersonId && p.FacilityId == FacilityId));
        b.Entity<PreferredDate>().HasQueryFilter(x => FacilityId == null ||
            Set<Person>().Any(p => p.Id == x.PersonId && p.FacilityId == FacilityId));
        b.Entity<LeaveApproval>().HasQueryFilter(x => FacilityId == null ||
            Set<Person>().Any(p => p.Id == x.PersonId && p.FacilityId == FacilityId));
        b.Entity<LeaveRequest>().HasQueryFilter(x => FacilityId == null ||
            Set<Person>().Any(p => p.Id == x.PersonId && p.FacilityId == FacilityId));
        b.Entity<LeaveRequestRange>().HasQueryFilter(x => FacilityId == null ||
            Set<LeaveRequest>().Any(r => r.Id == x.RequestId && Set<Person>().Any(p => p.Id == r.PersonId && p.FacilityId == FacilityId)));
        b.Entity<SwapRequest>().HasQueryFilter(x => FacilityId == null ||
            Set<Person>().Any(p => p.Id == x.FromPersonId && p.FacilityId == FacilityId));
    }

    /// <summary>New rows get the current hospital (and its state, for holidays) unless the code set one.</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        if (FacilityId is { } fid)
        {
            Guid? stateId = null;
            foreach (var e in ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
            {
                switch (e.Entity)
                {
                    case Person p when p.FacilityId == Guid.Empty: p.FacilityId = fid; break;
                    case RotaPeriod p when p.FacilityId == Guid.Empty: p.FacilityId = fid; break;
                    case ChangeLog c when c.FacilityId is null: c.FacilityId = fid; break;
                    case DocumentRecord d when d.FacilityId is null && d.Kind != DocumentKind.Manual: d.FacilityId = fid; break;
                    case PublicHoliday h when h.StateId == Guid.Empty: h.StateId = stateId ??= await StateOfAsync(fid, ct); break;
                    case PeakDay h when h.StateId == Guid.Empty: h.StateId = stateId ??= await StateOfAsync(fid, ct); break;
                }
            }
        }
        return await base.SaveChangesAsync(ct);
    }

    public Task<Guid> StateOfAsync(Guid facilityId, CancellationToken ct) =>
        Facilities.Where(f => f.Id == facilityId).Select(f => f.District!.StateId).FirstAsync(ct);
}
