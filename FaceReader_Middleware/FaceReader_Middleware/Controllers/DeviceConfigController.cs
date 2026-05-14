using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FaceReader_Middleware.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FaceReader_Middleware.Controllers
{
    [Route("api/device")]
    [ApiController]
    public class DeviceConfigController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly DeviceSettings _deviceSettings;

        public DeviceConfigController(
            IHttpClientFactory httpClientFactory,
            IOptions<DeviceSettings> deviceSettings)
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

        [HttpPost("set-config")]
        public async Task<IActionResult> SetConfig([FromBody] SetConfigRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            var baseUrl = ResolveDeviceBaseUrl(request.DeviceIp);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "Device IP is required on the request, or configure 'MainGate' in DeviceSettings.");
            }

            var url = $"{baseUrl}/setConfig";

            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var configJson = request.Config == null
                ? "{}"
                : JsonSerializer.Serialize(request.Config, jsonOptions);

            var formData = new[]
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                new KeyValuePair<string, string>("config", configJson)
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

        [HttpGet("config")]
        public async Task<IActionResult> GetConfig([FromQuery] string pass, [FromQuery] string? deviceIp)
        {
            if (string.IsNullOrWhiteSpace(pass))
            {
                return BadRequest("Device password is required.");
            }

            var baseUrl = ResolveDeviceBaseUrl(deviceIp);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "Device IP is required on the query string, or configure 'MainGate' in DeviceSettings.");
            }

            var url = $"{baseUrl}/device/config?pass={Uri.EscapeDataString(pass)}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }
    }
}
