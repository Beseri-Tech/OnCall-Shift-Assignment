using Rota.Api.Services;

namespace Rota.Api.Tests;

public class DatabaseBackupTests
{
    private static readonly TimeZoneInfo Myt = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur");

    [Fact]
    public void Before_the_hour_it_runs_today()
    {
        // 02:00 MYT on 24 Nov = 18:00 UTC on 23 Nov.
        var next = DatabaseBackup.NextRun(new DateTimeOffset(2026, 11, 23, 18, 0, 0, TimeSpan.Zero), Myt, 3);
        Assert.Equal(new DateTimeOffset(2026, 11, 23, 19, 0, 0, TimeSpan.Zero), next); // 03:00 MYT on the 24th
        Assert.Equal(TimeSpan.Zero, next.Offset);
    }

    [Fact]
    public void After_the_hour_it_runs_tomorrow()
    {
        // 04:00 MYT on 24 Nov = 20:00 UTC on 23 Nov.
        var next = DatabaseBackup.NextRun(new DateTimeOffset(2026, 11, 23, 20, 0, 0, TimeSpan.Zero), Myt, 3);
        Assert.Equal(new DateTimeOffset(2026, 11, 24, 19, 0, 0, TimeSpan.Zero), next); // 03:00 MYT on the 25th
    }

    [Fact]
    public void Exactly_on_the_hour_it_runs_the_next_day()
    {
        var next = DatabaseBackup.NextRun(new DateTimeOffset(2026, 11, 23, 19, 0, 0, TimeSpan.Zero), Myt, 3);
        Assert.Equal(new DateTimeOffset(2026, 11, 24, 19, 0, 0, TimeSpan.Zero), next);
    }
}
