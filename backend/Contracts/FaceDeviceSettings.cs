namespace FaceAccessController.Api.Contracts;

/// <summary>Extended registration / controller settings stored as JSON on <see cref="Models.FaceDevice"/>.</summary>
public sealed class FaceDeviceSettings
{
    public string? SiteControl { get; set; }
    public int UnitNo { get; set; } = 1;
    public string? DestIp { get; set; }
    public string Direction { get; set; } = "Entry";
    public bool DefaultEnrollerFingerprintOrFace { get; set; }
    public string TimeZone { get; set; } = "GMT+8";
    public int ReleaseTimeMs { get; set; } = 500;
    public int RecognitionScore { get; set; } = 80;
    public bool StrangerDetection { get; set; } = true;
    public int StrangerThreshold { get; set; } = 3;
    public string VoiceMode { get; set; } = "NoVoice";
    public string DisplayMode { get; set; } = "DisplayName";
    public bool LivenessEnabled { get; set; } = true;
    public bool EnableTransaction { get; set; } = true;
    public string MultiFaceDetection { get; set; } = "Multiple";
    public string StrangerVoiceMode { get; set; } = "StrangerAlarm";
    public string DisplayCustomization { get; set; } = "{name}";
    public string WiegandOutput { get; set; } = "WG26";
    public bool EnableFaceRecognitionInterval { get; set; }
    public int FaceRecognitionIntervalMs { get; set; } = 2000;
    public string QrVerificationMode { get; set; } = "ThirdParty";
    public string PhotoDisplay { get; set; } = "OnSite";
    /// <summary>When true, saved settings are pushed to the terminal via middleware <c>set-config</c>.</summary>
    public bool PushConfigToDevice { get; set; } = true;
}
