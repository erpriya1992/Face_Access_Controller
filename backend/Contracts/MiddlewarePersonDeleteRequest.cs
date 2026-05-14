namespace FaceAccessController.Api.Contracts;

/// <summary>JSON body for FaceReader_Middleware <c>POST /api/person/delete</c>.</summary>
public sealed class MiddlewarePersonDeleteRequest
{
    public string Pass { get; set; } = string.Empty;

    /// <summary>Person ID or comma-separated IDs, or "-1" for all (middleware contract).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Forwarded to middleware so delete hits the correct reader.</summary>
    public string? DeviceIp { get; set; }
}
