namespace Rota.Api.Services;

/// <summary>"Today" in the clinic's time zone, for leave deadlines (and the audit log's date filter).</summary>
public sealed class LocalClock(TimeProvider time, IConfiguration config)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(config["App:TimeZone"] ?? "Asia/Kuala_Lumpur");

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), _zone).DateTime);

    /// <summary>Midnight at the start of <paramref name="day"/> in the clinic's time zone, as UTC.</summary>
    public DateTimeOffset StartOf(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, _zone.GetUtcOffset(local)).ToUniversalTime();
    }
}
