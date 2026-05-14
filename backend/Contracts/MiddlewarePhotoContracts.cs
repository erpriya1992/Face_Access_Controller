namespace FaceAccessController.Api.Contracts;

public sealed class MiddlewarePhotoFindRequest
{
    public string Pass { get; set; } = string.Empty;

    public string PersonId { get; set; } = string.Empty;

    /// <summary>Optional: 1 = no base64, 2 = include base64 (device-dependent).</summary>
    public int? Base64Enable { get; set; }

    /// <summary>When set, middleware queries this reader instead of MainGate.</summary>
    public string? DeviceIp { get; set; }
}

public sealed class MiddlewarePhotoUpdateBase64Request
{
    public string Pass { get; set; } = string.Empty;

    public string PersonId { get; set; } = string.Empty;

    public string FaceId { get; set; } = string.Empty;

    public string ImgBase64 { get; set; } = string.Empty;

    public bool IsEasyWay { get; set; } = true;

    /// <summary>When set, middleware updates this reader instead of MainGate.</summary>
    public string? DeviceIp { get; set; }
}
