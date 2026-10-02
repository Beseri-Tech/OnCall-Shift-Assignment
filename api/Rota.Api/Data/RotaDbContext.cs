using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Rota.Api.Data;

public sealed class RotaDbContext(DbContextOptions<RotaDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Person> People => Set<Person>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<State> States => Set<State>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<RotaPeriod> Periods => Set<RotaPeriod>();
    public DbSet<PublicHoliday> Holidays => Set<PublicHoliday>();
    public DbSet<PeakDay> PeakDays => Set<PeakDay>();
    public DbSet<PeriodDay> PeriodDays => Set<PeriodDay>();
    public DbSet<PointGrant> PointGrants => Set<PointGrant>();
    public DbSet<LeaveDay> LeaveDays => Set<LeaveDay>();
    public DbSet<PreferredDate> PreferredDates => Set<PreferredDate>();
    public DbSet<RotaRun> Runs => Set<RotaRun>();
    public DbSet<ShiftAssignment> Assignments => Set<ShiftAssignment>();
    public DbSet<ChangeLog> ChangeLog => Set<ChangeLog>();
    public DbSet<SwapRequest> SwapRequests => Set<SwapRequest>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OutboxEmail> OutboxEmails => Set<OutboxEmail>();

    /// <summary>Keys that encrypt the admin cookie; stored here so logins survive redeploys.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Person>(e =>
        {
            e.ToTable("people");
            e.HasIndex(p => p.Code).IsUnique();
            e.HasIndex(p => p.Name).IsUnique();
            e.Property(p => p.Code).HasMaxLength(32);
            e.Property(p => p.Name).HasMaxLength(200);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(p => p.StatusReason).HasMaxLength(200);
            e.Property(p => p.Phone).HasMaxLength(32);
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

        b.Entity<RotaPeriod>(e =>
        {
            e.ToTable("rota_periods");
            e.Property(p => p.Name).HasMaxLength(100);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            e.HasMany(p => p.Runs).WithOne(r => r.Period).HasForeignKey(r => r.PeriodId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PublicHoliday>(e =>
        {
            e.ToTable("public_holidays");
            e.HasKey(h => h.Date);
            e.Property(h => h.Name).HasMaxLength(100);
        });

        b.Entity<PeakDay>(e =>
        {
            e.ToTable("peak_days");
            e.HasKey(h => h.Date);
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

        b.Entity<DataProtectionKey>().ToTable("data_protection_keys");

        b.Entity<ChangeLog>(e =>
        {
            e.ToTable("change_log");
            e.Property(c => c.Action).HasMaxLength(64);
            e.Property(c => c.Detail).HasColumnType("jsonb");
            e.HasIndex(c => c.At);
        });
    }
}
