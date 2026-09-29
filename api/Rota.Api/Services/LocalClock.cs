namespace Rota.Api.Services;

/// <summary>"Today" in the clinic's time zone, for leave deadlines.</summary>
public sealed class LocalClock(TimeProvider time, IConfiguration config)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(config["App:TimeZone"] ?? "Asia/Kuala_Lumpur");

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), _zone).DateTime);
}
