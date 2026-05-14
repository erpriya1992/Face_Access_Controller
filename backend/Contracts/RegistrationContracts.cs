namespace FaceAccessController.Api.Contracts;

public class RegisterFaceRequest
{
    public string PersonId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? IdCardNumber { get; set; }
    public string? Phone { get; set; }
    public string? Department { get; set; }
    public string ImageBase64 { get; set; } = string.Empty;

    /// <summary>
    /// <c>web</c> (default): upload or webcam image is pushed to the terminal.
    /// <c>device</c>: person is created on the terminal and face is captured on the reader camera (no web image).
    /// </summary>
    public string EnrollmentMode { get; set; } = "web";

    /// <summary>Legacy single-device selection; used when <see cref="FaceDeviceAccess"/> is empty.</summary>
    public int? FaceDeviceId { get; set; }

    /// <summary>Per-reader access during enrollment. Checked devices are enrolled on the terminal.</summary>
    public List<FaceDeviceAccessSelection>? FaceDeviceAccess { get; set; }
}

public sealed class FaceDeviceAccessSelection
{
    public int FaceDeviceId { get; set; }
    public bool AccessAllowed { get; set; }
}

/// <summary>Replace face photo for an existing employee already enrolled on the terminal.</summary>
public class ReplaceFaceRequest : RegisterFaceRequest
{
    /// <summary>Must be <see langword="true"/> to confirm intentional overwrite of the stored face image.</summary>
    public bool ConfirmReplace { get; set; }
}
