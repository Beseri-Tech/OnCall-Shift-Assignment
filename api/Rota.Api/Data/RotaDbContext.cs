using Microsoft.EntityFrameworkCore;

namespace Rota.Api.Data;

public sealed class RotaDbContext(DbContextOptions<RotaDbContext> options) : DbContext(options)
{
    public DbSet<Person> People => Set<Person>();
    public DbSet<RotaPeriod> Periods => Set<RotaPeriod>();
    public DbSet<PublicHoliday> Holidays => Set<PublicHoliday>();
    public DbSet<LeaveDay> LeaveDays => Set<LeaveDay>();
    public DbSet<PreferredDate> PreferredDates => Set<PreferredDate>();
    public DbSet<RotaRun> Runs => Set<RotaRun>();
    public DbSet<ShiftAssignment> Assignments => Set<ShiftAssignment>();
    public DbSet<ChangeLog> ChangeLog => Set<ChangeLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Person>(e =>
        {
            e.ToTable("people");
            e.HasIndex(p => p.Code).IsUnique();
            e.HasIndex(p => p.Name).IsUnique();
            e.Property(p => p.Code).HasMaxLength(32);
            e.Property(p => p.Name).HasMaxLength(200);
            e.HasMany(p => p.Leave).WithOne().HasForeignKey(l => l.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.PreferredDates).WithOne().HasForeignKey(p => p.PersonId).OnDelete(DeleteBehavior.Cascade);
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
            e.HasIndex(r => r.PeriodId).IsUnique().HasFilter("\"IsPublished\"").HasDatabaseName("ix_rota_runs_one_published_per_period");
            e.HasMany(r => r.Assignments).WithOne().HasForeignKey(a => a.RunId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ShiftAssignment>(e =>
        {
            e.ToTable("shift_assignments");
            e.HasKey(a => new { a.RunId, a.Date });
            e.HasIndex(a => a.PersonId);
            e.HasOne(a => a.Person).WithMany().HasForeignKey(a => a.PersonId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ChangeLog>(e =>
        {
            e.ToTable("change_log");
            e.Property(c => c.Action).HasMaxLength(64);
            e.Property(c => c.Detail).HasColumnType("jsonb");
            e.HasIndex(c => c.At);
        });
    }
}
