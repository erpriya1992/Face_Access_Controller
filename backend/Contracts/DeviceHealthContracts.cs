namespace FaceAccessController.Api.Contracts;

public record FaceDeviceHealthDto(
    int Id,
    string Name,
    string DeviceIp,
    bool IsActive,
    bool Reachable,
    string? ProbeDetail,
    int PendingConfigRetries,
    DateTime? LastConfigPushAtUtc,
    string? LastConfigPushError);
