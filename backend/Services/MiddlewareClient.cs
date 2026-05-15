using FaceAccessController.Api.Contracts;
using FaceAccessController.Api.Models;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FaceAccessController.Api.Services;

public class MiddlewareClient(HttpClient httpClient, IConfiguration configuration, ILogger<MiddlewareClient> logger)
{
    private readonly IConfiguration _configuration = configuration;
    /// <summary>Configured API root, may end with <c>/api/person</c> or host only.</summary>
    private readonly string _baseUrl = (configuration["ExternalApis:PersonApiBaseUrl"]
                                        ?? configuration["FacereaderMiddleware:BaseUrl"]
                                        ?? "http://localhost:5184/api/person").TrimEnd('/');

    // This is the "pass" parameter for FaceReader_Middleware endpoints.
    private readonly string _personApiPass = configuration["ExternalApis:PersonApiPass"]
                                            ?? configuration["FacereaderMiddleware:DevicePassword"]
                                            ?? "Admin@123";

    // Optional person password (set on device) during registration
    private readonly string _defaultPersonPassword = configuration["ExternalApis:DefaultPersonPassword"] ?? "1234";
    private readonly bool _enablePersonPassword = bool.TryParse(configuration["ExternalApis:EnablePersonPassword"], out var enabled) && enabled;
    private readonly int _facePermission = int.TryParse(configuration["ExternalApis:FacePermission"], out var fp) ? fp : 1;
    private readonly int _passwordPermission = int.TryParse(configuration["ExternalApis:PasswordPermission"], out var pp) ? pp : 1;
    private readonly int _fingerPermission = int.TryParse(configuration["ExternalApis:FingerPermission"], out var fgp) ? fgp : 1;

    /// <summary>Relative to <c>/api/person/</c>; FaceReader_Middleware exposes <c>update</c> (POST <c>/api/person/update</c>).</summary>
    private readonly string _personUpdatePath = (configuration["ExternalApis:PersonUpdatePath"] ?? "update").Trim().Trim('/');

    // Actual face device address used by FaceReader_Middleware when forwarding requests
    private readonly string _faceDeviceIp = (configuration["ExternalApis:DeviceIp"]
                                             ?? configuration["FacereaderMiddleware:FaceDeviceIp"]
                                             ?? string.Empty).TrimEnd('/');

    private readonly string _devicePassword = configuration["ExternalApis:DevicePassword"]
                                              ?? configuration["FacereaderMiddleware:DevicePassword"]
                                              ?? "Admin@123";

    /// <summary>Pushes controller settings to a specific terminal via middleware <c>set-config</c>.</summary>
    public async Task<(bool Success, string? Warning)> TryPushFaceDeviceConfigAsync(
        string deviceIp,
        string devicePassword,
        DeviceUiConfig config,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(deviceIp))
        {
            return (false, "Device IP is required to push settings.");
        }

        if (string.IsNullOrWhiteSpace(devicePassword))
        {
            return (false, "Device password is required to push settings.");
        }

        try
        {
            var url = BuildMiddlewareUrl("/api/device/set-config");
            var payload = new MiddlewareDeviceSetConfigRequest
            {
                Pass = devicePassword.Trim(),
                DeviceIp = deviceIp.Trim(),
                Config = config
            };
            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var response = await httpClient.PostAsJsonAsync(url, payload, jsonOptions, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                return (false, $"Terminal set-config failed (HTTP {(int)response.StatusCode}): {body}");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Face device set-config push failed for {DeviceIp}", deviceIp);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// After a successful enrollment, optionally pushes <c>setConfig</c> fields (e.g. <c>XXContent</c>) to the
    /// face reader via FaceReader_Middleware <c>POST /api/device/set-config</c>.
    /// Returns <see langword="true"/> only when the middleware returns success (2xx).
    /// </summary>
    public async Task<bool> TryApplyDeviceUiConfigAsync(CancellationToken ct)
    {
        if (!_configuration.GetValue("FaceDeviceUi:ApplyAfterRegistration", false))
        {
            return false;
        }

        if (!TryBuildDeviceUiConfig(out var deviceCfg))
        {
            logger.LogWarning(
                "FaceDeviceUi:ApplyAfterRegistration is true but no device UI config was supplied (set FaceDeviceUi:CustomConfigJson or XXContent/XXType).");
            return false;
        }

        try
        {
            var url = BuildMiddlewareUrl("/api/device/set-config");
            var payload = new MiddlewareDeviceSetConfigRequest { Pass = _personApiPass, Config = deviceCfg };
            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var response = await httpClient.PostAsJsonAsync(url, payload, jsonOptions, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning(
                    "Face device set-config failed: {Status} {Body}. Check middleware is running, PersonApiPass matches device password, and DeviceSettings:Devices:MainGate in FaceReader_Middleware points at the reader.",
                    (int)response.StatusCode,
                    body);
                return false;
            }

            logger.LogInformation("Face device set-config succeeded (HTTP {Status}).", (int)response.StatusCode);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Face device set-config request failed.");
            return false;
        }
    }

    private bool TryBuildDeviceUiConfig(out DeviceUiConfig config)
    {
        config = new DeviceUiConfig();

        var custom = _configuration["FaceDeviceUi:CustomConfigJson"];
        if (!string.IsNullOrWhiteSpace(custom))
        {
            var parsed = JsonSerializer.Deserialize<DeviceUiConfig>(custom, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (parsed is null || !HasAnyDeviceUiConfig(parsed))
            {
                return false;
            }

            config = parsed;
            return true;
        }

        config = new DeviceUiConfig
        {
            XXType = _configuration.GetValue<int?>("FaceDeviceUi:XXType"),
            XXContent = _configuration["FaceDeviceUi:XXContent"],
            DeviceName = _configuration["FaceDeviceUi:DeviceName"],
            TimeZone = _configuration["FaceDeviceUi:TimeZone"],
            DoorAlarmEnabled = _configuration.GetValue<bool?>("FaceDeviceUi:DoorAlarmEnabled"),
            DoorOpenTimeout = _configuration.GetValue<int?>("FaceDeviceUi:DoorOpenTimeout")
        };

        if (!HasAnyDeviceUiConfig(config))
        {
            return false;
        }

        return true;
    }

    private static bool HasAnyDeviceUiConfig(DeviceUiConfig c) =>
        c.XXType.HasValue
        || !string.IsNullOrWhiteSpace(c.XXContent)
        || !string.IsNullOrWhiteSpace(c.DeviceName)
        || !string.IsNullOrWhiteSpace(c.TimeZone)
        || c.DoorAlarmEnabled.HasValue
        || c.DoorOpenTimeout.HasValue;

  /// <summary>Calls middleware <c>GET /api/device/device-key</c> for the given LAN address.</summary>
    public async Task<FaceDeviceProbeDto> ProbeDeviceAsync(string deviceIp, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(deviceIp))
        {
            return new FaceDeviceProbeDto(false, null, "Device IP is required.");
        }

        var query = $"deviceIp={Uri.EscapeDataString(deviceIp.Trim())}";
        var url = BuildMiddlewareUrl("/api/device/device-key", query);

        try
        {
            using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            probeCts.CancelAfter(TimeSpan.FromSeconds(15));
            var response = await httpClient.GetAsync(url, probeCts.Token).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(probeCts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new FaceDeviceProbeDto(
                    false,
                    null,
                    $"Terminal returned HTTP {(int)response.StatusCode}{(string.IsNullOrWhiteSpace(body) ? "" : $": {body}")}");
            }

            return new FaceDeviceProbeDto(true, string.IsNullOrWhiteSpace(body) ? null : body.Trim(), null);
        }
        catch (Exception ex)
        {
            return new FaceDeviceProbeDto(false, null, ex.Message);
        }
    }

    /// <summary>Probes middleware host then device path (same record query as live sync).</summary>
    public async Task<DeviceConnectivityStatus> GetConnectivityStatusAsync(CancellationToken ct)
    {
        var root = GetMiddlewareRoot();
        var deviceDisplay = string.IsNullOrWhiteSpace(_faceDeviceIp)
            ? "(not configured)"
            : _faceDeviceIp;
        var checkedAt = DateTimeOffset.UtcNow;

        string? middlewareErr = null;
        var middlewareOk = false;

        try
        {
            using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            pingCts.CancelAfter(TimeSpan.FromSeconds(5));
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{root}/");
            await httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, pingCts.Token).ConfigureAwait(false);
            middlewareOk = true;
        }
        catch (Exception ex)
        {
            middlewareErr = ex.Message;
        }

        if (!middlewareOk)
        {
            return new DeviceConnectivityStatus(
                false,
                false,
                root,
                deviceDisplay,
                middlewareErr,
                null,
                checkedAt);
        }

        try
        {
            using var devCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            devCts.CancelAfter(TimeSpan.FromSeconds(12));
            _ = await FetchLatestRecordsAsync(devCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new DeviceConnectivityStatus(
                true,
                false,
                root,
                deviceDisplay,
                null,
                ex.Message,
                checkedAt);
        }

        return new DeviceConnectivityStatus(true, true, root, deviceDisplay, null, null, DateTimeOffset.UtcNow);
    }

    private string GetMiddlewareRoot()
    {
        var rootBase = _baseUrl.EndsWith("/api/person", StringComparison.OrdinalIgnoreCase)
            ? _baseUrl[..^"/api/person".Length]
            : _baseUrl;
        return rootBase.TrimEnd('/');
    }

    private string BuildMiddlewareUrl(string relativePathFromRoot, string? query = null)
    {
        // Supports both:
        // 1) http://host:port
        // 2) http://host:port/api/person
        var rootBase = _baseUrl.EndsWith("/api/person", StringComparison.OrdinalIgnoreCase)
            ? _baseUrl[..^"/api/person".Length]
            : _baseUrl;

        var normalizedRoot = rootBase.TrimEnd('/');
        var normalizedPath = relativePathFromRoot.StartsWith("/")
            ? relativePathFromRoot
            : $"/{relativePathFromRoot}";

        return string.IsNullOrWhiteSpace(query)
            ? $"{normalizedRoot}{normalizedPath}"
            : $"{normalizedRoot}{normalizedPath}?{query}";
    }

    /// <summary>Device address sent to middleware; <paramref name="deviceIpOverride"/> wins, else appsettings <c>DeviceIp</c>.</summary>
    private string? ResolveDeviceIpForApi(string? deviceIpOverride)
    {
        var raw = !string.IsNullOrWhiteSpace(deviceIpOverride) ? deviceIpOverride.Trim() : _faceDeviceIp.Trim();
        raw = raw.TrimEnd('/');
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    public async Task CreatePersonAsync(RegisterFaceRequest request, CancellationToken ct, string? deviceIpOverride = null)
    {
        var personInfo = new MiddlewarePersonInfo
        {
            Id = request.PersonId,
            Name = request.FullName,
            IdCardNum = request.IdCardNumber ?? string.Empty,
            Phone = request.Phone ?? string.Empty,
            Tag = request.Department ?? string.Empty,
            FacePermission = _facePermission,
            PasswordPermission = _passwordPermission,
            FingerPermission = _fingerPermission,
            Role = 0
        };

        // Some device firmwares reject passwordPermission/password fields (LAN_EXP-9104).
        // Keep it configurable and disabled by default.
        if (_enablePersonPassword)
        {
            personInfo.Password = _defaultPersonPassword;
        }
        else
        {
            // Match your working flow: keep permission enabled but send empty person password.
            personInfo.Password = string.Empty;
        }

        var payload = new MiddlewarePersonCreateRequest
        {
            Pass = _personApiPass,
            // Use per-request device override when provided (multi-device enrollment),
            // otherwise fall back to configured default device IP.
            DeviceIp = ResolveDeviceIpForApi(deviceIpOverride) ?? string.Empty,
            Person = personInfo
        };

        // Support both styles:
        // - baseUrl = http://localhost:5184  -> /api/person/createperson
        // - baseUrl = http://localhost:5184/api/person -> /createperson
        var createPersonUrl = _baseUrl.EndsWith("/api/person", StringComparison.OrdinalIgnoreCase)
            ? $"{_baseUrl}/createperson"
            : BuildMiddlewareUrl("/api/person/createperson");

        var response = await httpClient.PostAsJsonAsync(createPersonUrl, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Device createperson failed: {(int)response.StatusCode} - {body}");
        }
    }

    /// <summary>
    /// Pushes person fields and permissions to the terminal via <c>/person/update</c>.
    /// Block uses 0 for face/password/finger permission. Allow uses
    /// <c>ExternalApis:FacePermissionWhenAllowed</c> (etc.) when set — many terminals need <c>facePermission=2</c> to
    /// fully restore access after it was cleared to 0, while new enroll may still use <c>FacePermission=1</c>.
    /// </summary>
    public async Task UpdatePersonDoorAccessOnDeviceAsync(Employee employee, bool doorAllowed, CancellationToken ct, string? deviceIpOverride = null)
    {
        ArgumentNullException.ThrowIfNull(employee);

        int faceP;
        int pwdP;
        int fingerP;
        if (!doorAllowed)
        {
            faceP = 0;
            pwdP = 0;
            fingerP = 0;
        }
        else
        {
            faceP = int.TryParse(_configuration["ExternalApis:FacePermissionWhenAllowed"], out var fa) ? fa : _facePermission;
            pwdP = int.TryParse(_configuration["ExternalApis:PasswordPermissionWhenAllowed"], out var pa) ? pa : _passwordPermission;
            fingerP = int.TryParse(_configuration["ExternalApis:FingerPermissionWhenAllowed"], out var fga) ? fga : _fingerPermission;
        }

        var personInfo = new MiddlewarePersonInfo
        {
            Id = employee.PersonId,
            Name = employee.FullName,
            IdCardNum = employee.IdCardNumber ?? string.Empty,
            Phone = employee.Phone ?? string.Empty,
            Tag = employee.Department ?? string.Empty,
            FacePermission = faceP,
            PasswordPermission = pwdP,
            FingerPermission = fingerP,
            Role = 0
        };

        if (_enablePersonPassword)
        {
            personInfo.Password = _defaultPersonPassword;
        }
        else
        {
            personInfo.Password = string.Empty;
        }

        var payload = new MiddlewarePersonCreateRequest
        {
            Pass = _personApiPass,
            DeviceIp = ResolveDeviceIpForApi(deviceIpOverride) ?? string.Empty,
            Person = personInfo
        };

        var updateUrl = _baseUrl.EndsWith("/api/person", StringComparison.OrdinalIgnoreCase)
            ? $"{_baseUrl}/{_personUpdatePath}"
            : BuildMiddlewareUrl($"/api/person/{_personUpdatePath}");

        var response = await httpClient.PostAsJsonAsync(updateUrl, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Device person update ({_personUpdatePath}) failed: {(int)response.StatusCode} - {body}");
        }
    }

    public async Task CreatePhotoAsync(RegisterFaceRequest request, CancellationToken ct, string? deviceIpOverride = null)
    {
        var payload = new MiddlewarePhotoCreateRequest
        {
            DeviceIp = ResolveDeviceIpForApi(deviceIpOverride) ?? string.Empty,
            DevicePassword = _devicePassword,
            PersonId = request.PersonId,
            Type = 0,
            FaceId = string.Empty,
            ImgBase64 = request.ImageBase64,
            IsEasyWay = true
        };

        await PostPhotoCreateAsync(payload, ct);
    }

    /// <summary>
    /// Starts on-terminal face capture (no image upload). The reader prompts the user at the device.
    /// Maps to middleware <c>photocreate</c> without <c>imgBase64</c> and with <c>type</c> set (1 = face photo).
    /// </summary>
    public async Task CreatePhotoOnDeviceCaptureAsync(string personId, CancellationToken ct, string? deviceIpOverride = null, int captureType = 1)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person ID is required.", nameof(personId));
        }

        var payload = new MiddlewarePhotoCreateRequest
        {
            DeviceIp = ResolveDeviceIpForApi(deviceIpOverride) ?? string.Empty,
            DevicePassword = _devicePassword,
            PersonId = personId.Trim(),
            Type = captureType,
            ImgBase64 = null,
            IsEasyWay = false
        };

        await PostPhotoCreateAsync(payload, ct);
    }

    /// <summary>Polls the terminal until a face template appears or attempts are exhausted.</summary>
    public async Task<string?> WaitForFaceIdOnDeviceAsync(
        string personId,
        CancellationToken ct,
        string? deviceIpOverride = null,
        int maxAttempts = 20,
        int delayMs = 2000)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var faceId = await TryFindFaceIdAsync(personId, ct, deviceIpOverride);
            if (!string.IsNullOrWhiteSpace(faceId))
            {
                return faceId;
            }

            if (attempt < maxAttempts - 1)
            {
                await Task.Delay(delayMs, ct);
            }
        }

        return null;
    }

    private async Task PostPhotoCreateAsync(MiddlewarePhotoCreateRequest payload, CancellationToken ct)
    {
        var createPhotoUrl = _baseUrl.EndsWith("/api/person", StringComparison.OrdinalIgnoreCase)
            ? $"{_baseUrl}/photocreate"
            : BuildMiddlewareUrl("/api/person/photocreate");

        var jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var response = await httpClient.PostAsJsonAsync(createPhotoUrl, payload, jsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Device photocreate failed: {(int)response.StatusCode} - {body}");
        }
    }

    /// <summary>Removes a person (and face templates) from the terminal via FaceReader_Middleware <c>POST /api/person/delete</c>.</summary>
    public async Task DeletePersonOnDeviceAsync(string personId, CancellationToken ct, string? deviceIpOverride = null)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person ID is required.", nameof(personId));
        }

        var payload = new MiddlewarePersonDeleteRequest
        {
            Pass = _personApiPass,
            Id = personId.Trim(),
            DeviceIp = ResolveDeviceIpForApi(deviceIpOverride)
        };

        var deleteUrl = _baseUrl.EndsWith("/api/person", StringComparison.OrdinalIgnoreCase)
            ? $"{_baseUrl}/delete"
            : BuildMiddlewareUrl("/api/person/delete");

        var response = await httpClient.PostAsJsonAsync(deleteUrl, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Device person delete failed: {(int)response.StatusCode} - {body}");
        }
    }

    /// <summary>
    /// Queries the device via middleware <c>POST /api/person/photo-find</c> and tries to read a <c>faceId</c> from the JSON payload.
    /// </summary>
    public async Task<string?> TryFindFaceIdAsync(string personId, CancellationToken ct, string? deviceIpOverride = null)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            return null;
        }

        var payload = new MiddlewarePhotoFindRequest
        {
            Pass = _personApiPass,
            PersonId = personId.Trim(),
            Base64Enable = 1,
            DeviceIp = ResolveDeviceIpForApi(deviceIpOverride)
        };

        var findUrl = _baseUrl.EndsWith("/api/person", StringComparison.OrdinalIgnoreCase)
            ? $"{_baseUrl}/photo-find"
            : BuildMiddlewareUrl("/api/person/photo-find");

        try
        {
            var response = await httpClient.PostAsJsonAsync(findUrl, payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var raw = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(raw);
            return TryExtractFaceIdFromJson(doc.RootElement);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Requests the enrolled face image from the terminal via <c>photo-find</c> with base64 enabled.
    /// Used when the web app has no <see cref="Models.Employee.PhotoBase64"/> but the person exists on the device.
    /// </summary>
    public async Task<string?> TryGetPhotoBase64FromDeviceAsync(string personId, CancellationToken ct, string? deviceIpOverride = null)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            return null;
        }

        var payload = new MiddlewarePhotoFindRequest
        {
            Pass = _personApiPass,
            PersonId = personId.Trim(),
            Base64Enable = 2,
            DeviceIp = ResolveDeviceIpForApi(deviceIpOverride)
        };

        var findUrl = _baseUrl.EndsWith("/api/person", StringComparison.OrdinalIgnoreCase)
            ? $"{_baseUrl}/photo-find"
            : BuildMiddlewareUrl("/api/person/photo-find");

        try
        {
            var response = await httpClient.PostAsJsonAsync(findUrl, payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var raw = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(raw);
            return TryExtractPhotoBase64FromJson(doc.RootElement);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "photo-find (base64) failed for {PersonId}", personId);
            return null;
        }
    }

    /// <summary>Updates the face template on the device via middleware <c>POST /api/person/photo-update-base64</c>.</summary>
    public async Task UpdateDevicePhotoBase64Async(string personId, string faceId, string imageBase64Plain, CancellationToken ct, string? deviceIpOverride = null)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person ID is required.", nameof(personId));
        }

        if (string.IsNullOrWhiteSpace(faceId))
        {
            throw new ArgumentException("Face ID is required.", nameof(faceId));
        }

        var payload = new MiddlewarePhotoUpdateBase64Request
        {
            Pass = _personApiPass,
            PersonId = personId.Trim(),
            FaceId = faceId.Trim(),
            ImgBase64 = imageBase64Plain,
            IsEasyWay = true,
            DeviceIp = ResolveDeviceIpForApi(deviceIpOverride)
        };

        var updateUrl = _baseUrl.EndsWith("/api/person", StringComparison.OrdinalIgnoreCase)
            ? $"{_baseUrl}/photo-update-base64"
            : BuildMiddlewareUrl("/api/person/photo-update-base64");

        var response = await httpClient.PostAsJsonAsync(updateUrl, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Device photo update failed: {(int)response.StatusCode} - {body}");
        }
    }

    public async Task<List<MiddlewareRecordItem>> FetchLatestRecordsAsync(CancellationToken ct)
    {
        var query =
            "PersonId=-1&StartTime=0&EndTime=0&Length=200&Index=0&Model=-1" +
            $"&DeviceIp={Uri.EscapeDataString(_faceDeviceIp)}&DevicePassword={Uri.EscapeDataString(_devicePassword)}";
        var url = BuildMiddlewareUrl("/api/recognition-records/GetRecord", query);
        var response = await httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Device fetch latest records failed: {(int)response.StatusCode} - {body}");
        }

        var raw = await response.Content.ReadAsStringAsync(ct);
        var parsed = JsonSerializer.Deserialize<MiddlewareRecordResponse>(raw, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return parsed?.Data?.Records ?? [];
    }

    public async Task<bool> PersonExistsOnDeviceAsync(string personId, CancellationToken ct, string? deviceIpOverride = null)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            return false;
        }

        var deviceIp = ResolveDeviceIpForApi(deviceIpOverride) ?? string.Empty;
        var query =
            $"pass={Uri.EscapeDataString(_devicePassword)}&id={Uri.EscapeDataString(personId)}&deviceIp={Uri.EscapeDataString(deviceIp)}";

        var url = _baseUrl.EndsWith("/api/person", StringComparison.OrdinalIgnoreCase)
            ? $"{_baseUrl}/find?{query}"
            : BuildMiddlewareUrl("/api/person/find", query);

        try
        {
            var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var raw = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            // Fast fallback for non-standard payloads.
            if (raw.Contains(personId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            using var doc = JsonDocument.Parse(raw);
            return JsonContainsPersonId(doc.RootElement, personId);
        }
        catch
        {
            // If device query fails, do not block registration here.
            return false;
        }
    }

    private static bool JsonContainsPersonId(JsonElement element, string personId)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if ((property.NameEquals("id") || property.NameEquals("personId")) &&
                        property.Value.ValueKind == JsonValueKind.String &&
                        string.Equals(property.Value.GetString(), personId, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (JsonContainsPersonId(property.Value, personId))
                    {
                        return true;
                    }
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (JsonContainsPersonId(item, personId))
                    {
                        return true;
                    }
                }
                break;
        }

        return false;
    }

    private static string? TryExtractFaceIdFromJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("faceId") || property.NameEquals("FaceId") || property.NameEquals("face_id"))
                    {
                        if (property.Value.ValueKind == JsonValueKind.String)
                        {
                            var s = property.Value.GetString();
                            if (!string.IsNullOrWhiteSpace(s))
                            {
                                return s.Trim();
                            }
                        }

                        if (property.Value.ValueKind == JsonValueKind.Number)
                        {
                            return property.Value.GetRawText();
                        }
                    }

                    var nested = TryExtractFaceIdFromJson(property.Value);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested;
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var nested = TryExtractFaceIdFromJson(item);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested;
                    }
                }

                break;
        }

        return null;
    }

    private static string? TryExtractPhotoBase64FromJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
            {
                var s = element.GetString();
                return NormalizePhotoBase64(s);
            }

            case JsonValueKind.Object:
            {
                string? best = null;
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        var cleaned = NormalizePhotoBase64(property.Value.GetString());
                        if (!string.IsNullOrWhiteSpace(cleaned) && (best == null || cleaned.Length > best.Length))
                        {
                            best = cleaned;
                        }
                    }
                    else
                    {
                        var nested = TryExtractPhotoBase64FromJson(property.Value);
                        if (!string.IsNullOrWhiteSpace(nested) && (best == null || nested.Length > best.Length))
                        {
                            best = nested;
                        }
                    }
                }

                return best;
            }

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var nested = TryExtractPhotoBase64FromJson(item);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested;
                    }
                }

                return null;

            default:
                return null;
        }
    }

    private static string StripDataUrlPrefix(string s)
    {
        var t = s.Trim();
        var idx = t.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            return t[(idx + "base64,".Length)..].Trim();
        }

        return t;
    }

    /// <summary>Strips data-URL prefix if present and returns plain base64 when it looks like a real image payload.</summary>
    private static string? NormalizePhotoBase64(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return null;
        }

        var t = StripDataUrlPrefix(s.Trim());
        if (t.Length < 120)
        {
            return null;
        }

        foreach (var c in t.AsSpan())
        {
            if (char.IsLetterOrDigit(c) || c is '+' or '/' or '=' or '\r' or '\n' or ' ')
            {
                continue;
            }

            return null;
        }

        return t;
    }
}
