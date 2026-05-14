namespace FaceAccessController.Api.Contracts;

/// <summary>Face reader <c>setConfig</c> payload (mirrors FaceReader_Middleware Models/DeviceConfig).</summary>
public sealed class DeviceUiConfig
{
    public int? XXType { get; set; }
    public string? XXContent { get; set; }
    public string? DeviceName { get; set; }
    public string? TimeZone { get; set; }
    public bool? DoorAlarmEnabled { get; set; }
    public int? DoorOpenTimeout { get; set; }
    public int? RecognitionScore { get; set; }
    public bool? StrangerDetection { get; set; }
    public int? StrangerThreshold { get; set; }
    public int? VoiceMode { get; set; }
    public int? DisplayMode { get; set; }
    public bool? LivenessEnabled { get; set; }
    public bool? EnableTransaction { get; set; }
    public int? MultiFaceDetection { get; set; }
    public int? StrangerVoiceMode { get; set; }
    public string? DisplayCustomization { get; set; }
    public int? WiegandOutput { get; set; }
    public bool? EnableFaceRecognitionInterval { get; set; }
    public int? FaceRecognitionIntervalMs { get; set; }
    public int? QrVerificationMode { get; set; }
    public int? PhotoDisplay { get; set; }
}

/// <summary>JSON body for FaceReader_Middleware <c>POST /api/device/set-config</c>.</summary>
public sealed class MiddlewareDeviceSetConfigRequest
{
    public string Pass { get; set; } = string.Empty;
    public string? DeviceIp { get; set; }
    public DeviceUiConfig Config { get; set; } = new();
}
