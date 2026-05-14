namespace FaceAccessController.Api.Contracts;

/// <summary>Reachability of FaceReader middleware vs the physical device path (via GetRecord probe).</summary>
public record DeviceConnectivityStatus(
    bool MiddlewareOnline,
    bool DeviceOnline,
    string MiddlewareUrl,
    string FaceDeviceTarget,
    string? MiddlewareDetail,
    string? DeviceDetail,
    DateTimeOffset CheckedAtUtc);
