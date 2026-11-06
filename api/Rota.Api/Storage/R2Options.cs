namespace Rota.Api.Storage;

/// <summary>Cloudflare R2 settings (config section "R2"). Storage is switched off until all four required values are set.</summary>
public sealed class R2Options
{
    public const string Section = "R2";

    public string AccountId { get; set; } = "";
    public string BucketName { get; set; } = "";
    public string AccessKeyId { get; set; } = "";
    public string SecretAccessKey { get; set; } = "";

    /// <summary>Lifetime of the one-off links a browser uses to upload to / download from R2.</summary>
    public int UploadUrlExpiryMinutes { get; set; } = 5;
    public int DownloadUrlExpiryMinutes { get; set; } = 5;

    /// <summary>Largest file accepted, in MiB.</summary>
    public int MaxFileSizeMb { get; set; } = 20;

    /// <summary>Largest user manual accepted, in MiB (the manual is a big PDF with screenshots).</summary>
    public int ManualMaxFileSizeMb { get; set; } = 100;

    /// <summary>Uploads never confirmed after this many hours are deleted from R2.</summary>
    public int QuarantineHours { get; set; } = 24;

    /// <summary>Documents of rejected leave requests are deleted this many days after the decision (withdrawn ones go at once).</summary>
    public int RejectedRetentionDays { get; set; } = 30;

    public bool IsConfigured =>
        AccountId.Length > 0 && BucketName.Length > 0 && AccessKeyId.Length > 0 && SecretAccessKey.Length > 0;

    public string Endpoint => $"https://{AccountId}.r2.cloudflarestorage.com";
}
