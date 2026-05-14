namespace FaceAccessController.Api.Contracts;

public record FaceDeviceListDto(
    int Id,
    string Name,
    string? SiteControl,
    string DeviceIp,
    string Direction,
    bool IsActive,
    int SortOrder);

public sealed class SaveFaceDeviceRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DeviceIp { get; set; } = string.Empty;
    public string? DevicePassword { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public FaceDeviceSettings Settings { get; set; } = new();
}

public sealed class FaceDeviceDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DeviceIp { get; set; } = string.Empty;
    public string? DevicePassword { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public FaceDeviceSettings Settings { get; set; } = new();
}

public sealed class ProbeFaceDeviceRequest
{
    public string DeviceIp { get; set; } = string.Empty;
}

public record FaceDeviceProbeDto(bool Reachable, string? DeviceKey, string? Detail);

public record FaceDeviceSaveResultDto(
    FaceDeviceDetailDto Device,
    bool ConfigPushedToDevice,
    string? ConfigPushWarning);
