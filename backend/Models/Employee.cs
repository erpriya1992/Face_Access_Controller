namespace FaceAccessController.Api.Models;

public class Employee
{
    public int Id { get; set; }
    public string PersonId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    /// <summary>Face template id on the terminal when returned by middleware (photo-find).</summary>
    public string? FaceId { get; set; }
    public string? PhotoBase64 { get; set; }
    public string? IdCardNumber { get; set; }
    public string? Phone { get; set; }
    public string? Department { get; set; }

    /// <summary>When false, the person should not be granted door access (synced to terminal facePermission when supported).</summary>
    public bool DoorAccessAllowed { get; set; } = true;

    /// <summary>Face reader used for this person’s enrollment and terminal updates; null falls back to <c>ExternalApis:DeviceIp</c>.</summary>
    public int? FaceDeviceId { get; set; }
    public FaceDevice? FaceDevice { get; set; }

    public ICollection<EmployeeFaceDevice> FaceDeviceAccess { get; set; } = new List<EmployeeFaceDevice>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
