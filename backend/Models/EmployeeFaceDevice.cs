namespace FaceAccessController.Api.Models;

/// <summary>Which face readers an employee may use; enrollment and access sync are per device.</summary>
public class EmployeeFaceDevice
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int FaceDeviceId { get; set; }
    public FaceDevice FaceDevice { get; set; } = null!;
    /// <summary>When true, the person is enrolled / allowed on this reader.</summary>
    public bool AccessAllowed { get; set; } = true;
    public string? FaceId { get; set; }
    public DateTime? SyncedAtUtc { get; set; }
}
