using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FaceReader_Middleware.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FaceReader_Middleware.Controllers
{
    [Route("api/device")]
    [ApiController]
    public class DeviceController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly DeviceSettings _deviceSettings;

        public DeviceController(IHttpClientFactory httpClientFactory, IOptions<DeviceSettings> deviceSettings)
        {
            _httpClient = httpClientFactory.CreateClient();
            _deviceSettings = deviceSettings.Value;
        }

        private static string NormalizeDeviceBaseUrl(string? rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                return string.Empty;
            }

            var url = rawUrl.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = $"http://{url}";
            }

            return url.TrimEnd('/');
        }

        private string? ResolveDeviceBaseUrl(string? deviceIp)
        {
            if (!string.IsNullOrWhiteSpace(deviceIp))
            {
                return NormalizeDeviceBaseUrl(deviceIp);
            }

            if (_deviceSettings.Devices.TryGetValue("MainGate", out var mainGate) && !string.IsNullOrWhiteSpace(mainGate))
            {
                return NormalizeDeviceBaseUrl(mainGate);
            }

            return null;
        }

        [HttpPost("set-password")]
        public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.OldPass) || string.IsNullOrWhiteSpace(request.NewPass))
            {
                return BadRequest("Old password and new password are required and cannot be empty or whitespace.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/setPassWord";

            var formData = new[]
            {
                new KeyValuePair<string, string>("oldPass", request.OldPass),
                new KeyValuePair<string, string>("newPass", request.NewPass)
            };

            using var content = new FormUrlEncodedContent(formData);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Device Serial Number Query - supports both GET and POST with optional deviceIp
        [HttpGet("device-key")]
        [HttpPost("device-key")]
        public async Task<IActionResult> GetDeviceKey([FromQuery] string? deviceIp)
        {
            var baseUrl = ResolveDeviceBaseUrl(deviceIp);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "Device IP is required on the query string, or configure 'MainGate' in DeviceSettings.");
            }

            var url = $"{baseUrl}/getDeviceKey";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Logo Change
        // POST api/device/change-logo
        [HttpPost("change-logo")]
        public async Task<IActionResult> ChangeLogo([FromBody] ChangeLogoRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (string.IsNullOrWhiteSpace(request.ImgBase64))
            {
                return BadRequest("imgBase64 (logo image base64 string) is required. Use \"-1\" to clear to default image.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/changeLogo";

            var formData = new[]
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                new KeyValuePair<string, string>("imgBase64", request.ImgBase64)
            };

            using var content = new FormUrlEncodedContent(formData);

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Img1 Change
        // PUT api/device/img1
        [HttpPut("img1")]
        public async Task<IActionResult> ChangeImg1([FromBody] Img1ChangeRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Base64))
            {
                return BadRequest("base64 (img1 image base64 string) is required. Use \"-1\" to clear to default image.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/device/img1";

            var formData = new[]
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                new KeyValuePair<string, string>("base64", request.Base64)
            };

            using var content = new FormUrlEncodedContent(formData);

            var response = await _httpClient.PutAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Wired Network Configuration
        // POST api/device/set-net-info
        [HttpPost("set-net-info")]
        public async Task<IActionResult> SetNetInfo([FromBody] WiredNetworkConfigRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (request.IsDHCPMod != 1 && request.IsDHCPMod != 2)
            {
                return BadRequest("isDHCPMod must be 1 (DHCP) or 2 (Static IP).");
            }

            // For static IP mode, ip, gateway and DNS are mandatory (per spec)
            if (request.IsDHCPMod == 2)
            {
                if (string.IsNullOrWhiteSpace(request.Ip) ||
                    string.IsNullOrWhiteSpace(request.Gateway) ||
                    string.IsNullOrWhiteSpace(request.Dns))
                {
                    return BadRequest("For static IP (isDHCPMod=2), ip, gateway and DNS are required and cannot be empty.");
                }
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/setNetInfo";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                new KeyValuePair<string, string>("isDHCPMod", request.IsDHCPMod.ToString())
            };

            if (request.IsDHCPMod == 2)
            {
                formPairs.Add(new KeyValuePair<string, string>("ip", request.Ip));
                formPairs.Add(new KeyValuePair<string, string>("gateway", request.Gateway));

                if (!string.IsNullOrWhiteSpace(request.SubnetMask))
                {
                    formPairs.Add(new KeyValuePair<string, string>("subnetMask", request.SubnetMask));
                }

                formPairs.Add(new KeyValuePair<string, string>("DNS", request.Dns));
            }

            using var content = new FormUrlEncodedContent(formPairs);

            // Device supports POST/GET; we use POST here
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Wireless Network Configuration
        // POST api/device/set-wifi
        [HttpPost("set-wifi")]
        public async Task<IActionResult> SetWifi([FromBody] WirelessNetworkConfigRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (string.IsNullOrWhiteSpace(request.SsId))
            {
                return BadRequest("Wi-Fi SSID (ssId) is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pwd))
            {
                return BadRequest("Wi-Fi password (pwd) is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/setWifi";

            // Build wifiMsg JSON according to spec
            var wifiObj = new Dictionary<string, object>
            {
                { "ssId", request.SsId },
                { "pwd", request.Pwd },
                { "isDHCPMod", request.IsDHCPMod }
            };

            if (!request.IsDHCPMod)
            {
                // Static IP mode: ip, gateway and dns required, subnetMask optional
                if (string.IsNullOrWhiteSpace(request.Ip) ||
                    string.IsNullOrWhiteSpace(request.Gateway) ||
                    string.IsNullOrWhiteSpace(request.Dns))
                {
                    return BadRequest("For static Wi-Fi IP (isDHCPMod=false), ip, gateway and dns are required and cannot be empty.");
                }

                wifiObj["ip"] = request.Ip;
                wifiObj["gateway"] = request.Gateway;
                wifiObj["dns"] = request.Dns;

                if (!string.IsNullOrWhiteSpace(request.SubnetMask))
                {
                    wifiObj["subnetMask"] = request.SubnetMask;
                }
            }

            var wifiJson = System.Text.Json.JsonSerializer.Serialize(wifiObj);

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                new KeyValuePair<string, string>("wifiMsg", wifiJson)
            };

            using var content = new FormUrlEncodedContent(formPairs);

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Device Time Setting
        // POST api/device/set-time
        [HttpPost("set-time")]
        public async Task<IActionResult> SetTime([FromBody] DeviceTimeSettingRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Timestamp))
            {
                return BadRequest("Timestamp (Unix millisecond timestamp) is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/setTime";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                new KeyValuePair<string, string>("timestamp", request.Timestamp)
            };

            using var content = new FormUrlEncodedContent(formPairs);

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Device Restarting
        // POST api/device/restart
        [HttpPost("restart")]
        public async Task<IActionResult> RestartDevice([FromBody] DeviceRestartRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/restartDevice";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass)
            };

            using var content = new FormUrlEncodedContent(formPairs);

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Registration Photo Callback configuration
        // POST api/device/set-img-reg-callback
        [HttpPost("set-img-reg-callback")]
        public async Task<IActionResult> SetImgRegCallback([FromBody] ImgRegCallbackConfigRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/setImgRegCallBack";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                // url: empty string or null will clear callback address, as per spec
                new KeyValuePair<string, string>("url", request.Url ?? string.Empty)
            };

            if (request.Base64.HasValue)
            {
                formPairs.Add(new KeyValuePair<string, string>("base64", request.Base64.Value.ToString()));
            }

            using var content = new FormUrlEncodedContent(formPairs);

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Heartbeat Callback configuration
        // POST api/device/set-heartbeat-callback
        [HttpPost("set-heartbeat-callback")]
        public async Task<IActionResult> SetHeartbeatCallback([FromBody] HeartbeatCallbackConfigRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/setDeviceHeartBeat";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                // url: empty string or null will clear callback address, as per spec
                new KeyValuePair<string, string>("url", request.Url ?? string.Empty)
            };

            if (request.Interval.HasValue)
            {
                formPairs.Add(new KeyValuePair<string, string>("Interval", request.Interval.Value.ToString()));
            }

            using var content = new FormUrlEncodedContent(formPairs);

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Remote Control Output
        // POST api/device/open-door-control
        [HttpPost("open-door-control")]
        public async Task<IActionResult> OpenDoorControl([FromBody] RemoteControlOutputRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/device/openDoorControl";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass)
            };

            if (request.Type.HasValue)
            {
                formPairs.Add(new KeyValuePair<string, string>("type", request.Type.Value.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(request.Content))
            {
                formPairs.Add(new KeyValuePair<string, string>("content", request.Content));
            }

            using var content = new FormUrlEncodedContent(formPairs);

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Card Number Registration Callback configuration
        // POST api/device/set-card-reg-callback
        [HttpPost("set-card-reg-callback")]
        public async Task<IActionResult> SetCardRegCallback([FromBody] CardRegCallbackConfigRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/setCardRegCallBack";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                // url: empty or null will clear callback address
                new KeyValuePair<string, string>("url", request.Url ?? string.Empty)
            };

            using var content = new FormUrlEncodedContent(formPairs);

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Algorithm Version Number Query
        // GET api/device/sdk-version
        [HttpGet("sdk-version")]
        public async Task<IActionResult> GetSdkVersion([FromQuery] string pass, [FromQuery] int? type)
        {
            if (string.IsNullOrWhiteSpace(pass))
            {
                return BadRequest("Device password is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            // Default algorithm type to 1 (Face SDK) if not provided
            var algoType = type ?? 1;

            var url = $"{baseUrl}/getSDKVersion?pass={Uri.EscapeDataString(pass)}&type={algoType}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Alarm Cancellation
        // POST api/device/alarm-cancel
        [HttpPost("alarm-cancel")]
        public async Task<IActionResult> AlarmCancel([FromBody] AlarmCancelRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/device/alarmCancel";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass)
            };

            using var content = new FormUrlEncodedContent(formPairs);

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // QR Code Callback configuration
        // POST api/device/set-qrcode-callback
        [HttpPost("set-qrcode-callback")]
        public async Task<IActionResult> SetQrCodeCallback([FromBody] QrCodeCallbackConfigRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/device/setQRCodeCallback";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                // url: empty or null will clear callback address
                new KeyValuePair<string, string>("url", request.Url ?? string.Empty)
            };

            using var content = new FormUrlEncodedContent(formPairs);

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Registration Information Callback Address
        // POST api/device/set-regist-callback
        [HttpPost("set-regist-callback")]
        public async Task<IActionResult> SetRegistCallback([FromBody] RegistrationCallbackConfigRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (request.Type < 1 || request.Type > 5)
            {
                return BadRequest("Type must be between 1 and 5 (1: photo, 2: card number, 3: fingerprint, 4: QR code, 5: person info).");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/device/setRegistCallback";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                // url: empty or null will clear callback address
                new KeyValuePair<string, string>("url", request.Url ?? string.Empty),
                new KeyValuePair<string, string>("type", request.Type.ToString())
            };

            if (request.Base64Enable.HasValue)
            {
                formPairs.Add(new KeyValuePair<string, string>("base64Enable", request.Base64Enable.Value.ToString()));
            }

            using var content = new FormUrlEncodedContent(formPairs);

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Write Secret Key
        // POST api/device/write-secret-key
        [HttpPost("write-secret-key")]
        public async Task<IActionResult> WriteSecretKey([FromBody] WriteSecretKeyRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (string.IsNullOrWhiteSpace(request.WriteSecretKey))
            {
                return BadRequest("Secret key (writeSecretKey) is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/device/writeSecretKey";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                new KeyValuePair<string, string>("writeSecretKey", request.WriteSecretKey)
            };

            using var content = new FormUrlEncodedContent(formPairs);

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Quick registration of add or delete cards
        // POST api/device/quick-card-register
        [HttpPost("quick-card-register")]
        public async Task<IActionResult> QuickCardRegister([FromBody] QuickCardRegisterRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/device/quickCardRegister";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass)
            };

            using var content = new FormUrlEncodedContent(formPairs);

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Set dynamic screen saver image (GIF)
        // POST api/device/set-gif
        [HttpPost("set-gif")]
        public async Task<IActionResult> SetGif([FromBody] DynamicScreenSaverGifRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Base64))
            {
                return BadRequest("GIF base64 content is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/device/setGif";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                new KeyValuePair<string, string>("base64", request.Base64)
            };

            using var content = new FormUrlEncodedContent(formPairs);

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Add static rotating image
        // POST api/device/add-static-pic
        [HttpPost("add-static-pic")]
        public async Task<IActionResult> AddStaticPic([FromBody] AddStaticPicRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (request.Operator != 1 && request.Operator != 2)
            {
                return BadRequest("Operator must be 1 (replace dynamic screen saver) or 2 (restore default screen saver).");
            }

            if (request.Operator == 1 && string.IsNullOrWhiteSpace(request.Base64))
            {
                return BadRequest("Base64 content is required when operator is 1.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/device/addStaticPic";

            // Build gif JSON manually to avoid using C# keyword 'operator' as an identifier
            var gifDict = new Dictionary<string, object>();
            gifDict["operator"] = request.Operator;
            gifDict["base64"] = request.Base64;

            var gifJson = System.Text.Json.JsonSerializer.Serialize(gifDict);

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                new KeyValuePair<string, string>("gif", gifJson)
            };

            using var content = new FormUrlEncodedContent(formPairs);

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Delete static rotating image
        // DELETE api/device/delete-static-pic
        [HttpDelete("delete-static-pic")]
        public async Task<IActionResult> DeleteStaticPic([FromBody] DeleteStaticPicRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Filename))
            {
                return BadRequest("Filename is required. Use \"-1\" to delete all static images, or comma-separated file names.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/device/delStaticPic";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                new KeyValuePair<string, string>("filename", request.Filename)
            };

            using var content = new FormUrlEncodedContent(formPairs);

            var httpRequest = new HttpRequestMessage(HttpMethod.Delete, url)
            {
                Content = content
            };

            var response = await _httpClient.SendAsync(httpRequest);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Get custom screensaver configuration
        // GET api/device/screen-saver
        [HttpGet("screen-saver")]
        public async Task<IActionResult> GetScreenSaver([FromQuery] string pass)
        {
            if (string.IsNullOrWhiteSpace(pass))
            {
                return BadRequest("Device password is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/device/getScreenSaver?pass={Uri.EscapeDataString(pass)}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Set custom screensaver configuration
        // POST api/device/set-screen-saver
        [HttpPost("set-screen-saver")]
        public async Task<IActionResult> SetScreenSaver([FromBody] SetScreenSaverRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (request.ScreenSaver == null)
            {
                return BadRequest("screenSaver configuration is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/device/setScreenSaver";

            var options = new System.Text.Json.JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var screenSaverJson = System.Text.Json.JsonSerializer.Serialize(request.ScreenSaver, options);

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                new KeyValuePair<string, string>("screenSaver", screenSaverJson)
            };

            using var content = new FormUrlEncodedContent(formPairs);

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Photo Download (internal interface)
        // GET api/device/download-image
        [HttpGet("download-image")]
        public async Task<IActionResult> DownloadImage([FromQuery] string pass, [FromQuery] string filename)
        {
            if (string.IsNullOrWhiteSpace(pass))
            {
                return BadRequest("Device password is required.");
            }

            if (string.IsNullOrWhiteSpace(filename))
            {
                return BadRequest("filename (picture absolute path) is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/download/image?pass={Uri.EscapeDataString(pass)}&filename={Uri.EscapeDataString(filename)}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var bytes = await response.Content.ReadAsByteArrayAsync();

            return File(bytes, contentType);
        }
    }
}

