namespace FaceAccessController.Api.Models;

public class DeviceConfigPushJob
{
    public long Id { get; set; }
    public int? FaceDeviceId { get; set; }
    public string DeviceIp { get; set; } = string.Empty;
    public string DevicePassword { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? LastSuccessAtUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
}
