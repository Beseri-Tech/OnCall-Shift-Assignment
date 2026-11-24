namespace Rota.Api.Services;

/// <summary>"Today" in the clinic's time zone, for leave deadlines (and the audit log's date filter).</summary>
public sealed class LocalClock(TimeProvider time, IConfiguration config)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(config["App:TimeZone"] ?? "Asia/Kuala_Lumpur");

    /// <summary>The clinic's time zone (App:TimeZone).</summary>
    public TimeZoneInfo Zone => _zone;

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), _zone).DateTime);

    /// <summary>A moment as clinic-local text, e.g. "3 Dec 2026, 14:05".</summary>
    public string Format(DateTimeOffset moment) =>
        TimeZoneInfo.ConvertTime(moment, _zone).ToString("d MMM yyyy, HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    public string Now => Format(time.GetUtcNow());

    /// <summary>Midnight at the start of <paramref name="day"/> in the clinic's time zone, as UTC.</summary>
    public DateTimeOffset StartOf(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, _zone.GetUtcOffset(local)).ToUniversalTime();
    }
}
