using System.Text.Json;
using System.Text.Json.Serialization;

namespace FaceAccessController.Api.Contracts;

public class MiddlewarePersonCreateRequest
{
    [JsonPropertyName("pass")]
    public string Pass { get; set; } = string.Empty;

    [JsonPropertyName("person")]
    public MiddlewarePersonInfo Person { get; set; } = new();

    // Face device base URL (e.g. http://192.168.1.55:8090)
    // Used by FaceReader_Middleware instead of its own DeviceSettings.
    [JsonPropertyName("deviceIp")]
    public string DeviceIp { get; set; } = string.Empty;
}

public class MiddlewarePersonInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("idcardNum")]
    public string IdCardNum { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    public string Phone { get; set; } = string.Empty;

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("facePermission")]
    public int FacePermission { get; set; } = 1;

    [JsonPropertyName("passwordPermission")]
    public int PasswordPermission { get; set; } = 0;

    [JsonPropertyName("fingerPermission")]
    public int FingerPermission { get; set; } = 0;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Device person role; 0 is typical for normal users (matches terminal person payloads).</summary>
    [JsonPropertyName("role")]
    public int Role { get; set; }
}

public class MiddlewarePhotoCreateRequest
{
    public string DeviceIp { get; set; } = string.Empty;
    public string DevicePassword { get; set; } = string.Empty;
    public string PersonId { get; set; } = string.Empty;
    public int Type { get; set; } = 1;
    public string? FaceId { get; set; }
    public string? ImgBase64 { get; set; }
    public bool IsEasyWay { get; set; } = true;
}

public class MiddlewareRecordResponse
{
    public bool Success { get; set; }
    public MiddlewareRecordData? Data { get; set; }
}

public class MiddlewareRecordData
{
    public List<MiddlewareRecordItem> Records { get; set; } = [];
}

public class MiddlewareRecordItem
{
    [JsonPropertyName("personId")]
    public string PersonId { get; set; } = string.Empty;

    // Device can return `time` as either unix milliseconds number or string.
    [JsonPropertyName("time")]
    public JsonElement Time { get; set; }

    [JsonPropertyName("deviceSn")]
    public string DeviceSn { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public int Model { get; set; }
}
