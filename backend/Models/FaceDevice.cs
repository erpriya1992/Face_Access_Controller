namespace FaceAccessController.Api.Models;

/// <summary>Registered face reader terminal.</summary>
public class FaceDevice
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Informational label mirrored from <c>Settings.SiteControl</c> for legacy lists.</summary>
    public string? DepartmentLabel { get; set; }
    public string DeviceIp { get; set; } = string.Empty;
    public string? DevicePassword { get; set; }
    public string? SettingsJson { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
