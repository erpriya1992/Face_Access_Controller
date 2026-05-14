using System.Text.Json;
using FaceAccessController.Api.Contracts;
using FaceAccessController.Api.Models;

namespace FaceAccessController.Api.Services;

public static class FaceDeviceConfigMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static FaceDeviceSettings ParseSettings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new FaceDeviceSettings();
        }

        return JsonSerializer.Deserialize<FaceDeviceSettings>(json, JsonOptions) ?? new FaceDeviceSettings();
    }

    public static string SerializeSettings(FaceDeviceSettings settings) =>
        JsonSerializer.Serialize(settings, JsonOptions);

    public static FaceDeviceDetailDto ToDetailDto(FaceDevice entity)
    {
        var settings = ParseSettings(entity.SettingsJson);
        return new FaceDeviceDetailDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            DeviceIp = entity.DeviceIp,
            DevicePassword = entity.DevicePassword,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive,
            Settings = settings
        };
    }

    public static void ApplySaveRequest(FaceDevice entity, SaveFaceDeviceRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.DeviceIp = request.DeviceIp.Trim();
        if (!string.IsNullOrWhiteSpace(request.DevicePassword))
        {
            entity.DevicePassword = request.DevicePassword.Trim();
        }

        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.DepartmentLabel = string.IsNullOrWhiteSpace(request.Settings.SiteControl)
            ? null
            : request.Settings.SiteControl.Trim();
        entity.SettingsJson = SerializeSettings(request.Settings ?? new FaceDeviceSettings());
    }

    /// <summary>Maps UI settings to middleware/device <c>setConfig</c> JSON fields.</summary>
    public static DeviceUiConfig ToDeviceUiConfig(FaceDevice entity, FaceDeviceSettings settings)
    {
        return new DeviceUiConfig
        {
            DeviceName = entity.Name,
            TimeZone = settings.TimeZone,
            DoorOpenTimeout = settings.ReleaseTimeMs,
            DoorAlarmEnabled = settings.StrangerDetection,
            XXContent = settings.DisplayCustomization,
            XXType = MapDisplayMode(settings.DisplayMode),
            RecognitionScore = settings.RecognitionScore,
            StrangerDetection = settings.StrangerDetection,
            StrangerThreshold = settings.StrangerThreshold,
            VoiceMode = MapVoiceMode(settings.VoiceMode),
            DisplayMode = MapDisplayMode(settings.DisplayMode),
            LivenessEnabled = settings.LivenessEnabled,
            EnableTransaction = settings.EnableTransaction,
            MultiFaceDetection = MapMultiFace(settings.MultiFaceDetection),
            StrangerVoiceMode = MapStrangerVoiceMode(settings.StrangerVoiceMode),
            DisplayCustomization = settings.DisplayCustomization,
            WiegandOutput = MapWiegand(settings.WiegandOutput),
            EnableFaceRecognitionInterval = settings.EnableFaceRecognitionInterval,
            FaceRecognitionIntervalMs = settings.FaceRecognitionIntervalMs,
            QrVerificationMode = MapQrMode(settings.QrVerificationMode),
            PhotoDisplay = MapPhotoDisplay(settings.PhotoDisplay)
        };
    }

    private static int? MapVoiceMode(string value) => value switch
    {
        "Name" => 1,
        "NoVoice" => 0,
        _ => 0
    };

    private static int? MapDisplayMode(string value) => value switch
    {
        "DisplayName" => 0,
        "DisplayId" => 1,
        "DisplayNone" => 2,
        _ => 0
    };

    private static int? MapMultiFace(string value) => value switch
    {
        "Multiple" => 1,
        "Single" => 0,
        _ => 1
    };

    private static int? MapStrangerVoiceMode(string value) => value switch
    {
        "StrangerAlarm" => 0,
        "NoVoice" => 1,
        _ => 0
    };

    private static int? MapWiegand(string value) => value switch
    {
        "WG26" => 0,
        "WG34" => 1,
        _ => 0
    };

    private static int? MapQrMode(string value) => value switch
    {
        "ThirdParty" => 1,
        "Local" => 0,
        _ => 1
    };

    private static int? MapPhotoDisplay(string value) => value switch
    {
        "OnSite" => 0,
        "Registered" => 1,
        _ => 0
    };
}
