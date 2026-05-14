namespace FaceAccessController.Api.Contracts;

/// <summary>Employee row for Settings / access control (no photo payload).</summary>
public record EmployeeSettingsRow(
    int Id,
    string PersonId,
    string FullName,
    string? Department,
    string? Phone,
    string? IdCardNumber,
    bool DoorAccessAllowed,
    DateTime CreatedAt,
    int? FaceDeviceId,
    string? FaceDeviceName);

public sealed class UpdateDoorAccessRequest
{
    /// <summary>When false, face permission on the terminal is set to 0 (typically no door access).</summary>
    public bool DoorAccessAllowed { get; set; }
}
