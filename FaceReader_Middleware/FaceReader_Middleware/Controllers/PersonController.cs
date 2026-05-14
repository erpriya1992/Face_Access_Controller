using FaceReader_Middleware.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace FaceReader_Middleware.Controllers
{
    [Route("api/person")]
    [ApiController]
    public class PersonController : ControllerBase
    {

        private readonly HttpClient _httpClient;
        private readonly DeviceSettings _deviceSettings;
        public PersonController(
    IHttpClientFactory httpClientFactory,
    IOptions<DeviceSettings> deviceSettings)
        {
            this._httpClient = httpClientFactory.CreateClient();
            this._deviceSettings = deviceSettings.Value;
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
        [HttpPost("createperson")]
        public async Task<IActionResult> CreatePerson([FromBody] PersonCreateRequest request)
        {
            PersonController personController = this;
            if (string.IsNullOrWhiteSpace(request.Pass))
                return (IActionResult)personController.BadRequest((object)"Device password is required.");
            if (request.Person == null || string.IsNullOrWhiteSpace(request.Person.Name))
                return (IActionResult)personController.BadRequest((object)"Person name is required.");
            // Prefer deviceIp coming from the caller; fallback to MainGate config.
            var deviceBaseUrl = !string.IsNullOrWhiteSpace(request.DeviceIp)
                ? NormalizeDeviceBaseUrl(request.DeviceIp)
                : NormalizeDeviceBaseUrl(personController._deviceSettings.Devices["MainGate"]);
            string requestUri = $"{deviceBaseUrl}/person/create";
            HttpResponseMessage httpResponseMessage = await personController._httpClient.PostAsJsonAsync<PersonCreateRequest>(requestUri, request);
            if (!httpResponseMessage.IsSuccessStatusCode)
                return (IActionResult)personController.StatusCode((int)httpResponseMessage.StatusCode, (object)"Personnel registration failed.");
            string str = await httpResponseMessage.Content.ReadAsStringAsync();
            return (IActionResult)personController.Ok((object)str);
        }

        [HttpPost("photocreate")]
        public async Task<IActionResult> CreateFace([FromBody] FaceCreateRequest request)
        {
            PersonController personController = this;
            if (string.IsNullOrEmpty(request.DeviceIp) || string.IsNullOrEmpty(request.DevicePassword) || string.IsNullOrEmpty(request.PersonId))
                return (IActionResult)personController.BadRequest((object)"Device IP, password, and personId are required.");
            // Prefer deviceIp coming from the caller.
            var deviceBaseUrl = NormalizeDeviceBaseUrl(request.DeviceIp);
            string requestUri = $"{deviceBaseUrl}/face/create";
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.FaceId = Guid.NewGuid().ToString();
                FormUrlEncodedContent content1;
                if (!string.IsNullOrEmpty(request.ImgBase64))
                    content1 = new FormUrlEncodedContent((IEnumerable<KeyValuePair<string, string>>)new KeyValuePair<string, string>[5]
                    {
          new KeyValuePair<string, string>("pass", request.DevicePassword),
          new KeyValuePair<string, string>("personId", request.PersonId),
          new KeyValuePair<string, string>("faceId", ""),
          new KeyValuePair<string, string>("imgBase64", request.ImgBase64),
          new KeyValuePair<string, string>("isEasyWay", request.IsEasyWay.ToString().ToLower())
                    });
                else
                    content1 = new FormUrlEncodedContent((IEnumerable<KeyValuePair<string, string>>)new KeyValuePair<string, string>[3]
                    {
          new KeyValuePair<string, string>("pass", request.DevicePassword),
          new KeyValuePair<string, string>("personId", request.PersonId),
          new KeyValuePair<string, string>("type", request.Type.ToString())
                    });
                try
                {
                    string content2 = await (await client.PostAsync(requestUri, (HttpContent)content1)).Content.ReadAsStringAsync();
                    return (IActionResult)personController.Content(content2, "application/json");
                }
                catch (HttpRequestException ex)
                {
                    return (IActionResult)personController.StatusCode(500, (object)new
                    {
                        success = false,
                        msg = "Error connecting to device",
                        error = ex.Message
                    });
                }
            }
        }

        // Card Number Registration
        // POST api/person/card-register
        [HttpPost("card-register")]
        public async Task<IActionResult> CardRegister([FromBody] CardNumberRegisterRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (string.IsNullOrWhiteSpace(request.PersonId))
            {
                return BadRequest("PersonId is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/face/icCardRegist";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                new KeyValuePair<string, string>("personId", request.PersonId)
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
        // Personnel Update
        // POST api/person/update
        [HttpPost("update")]
        public async Task<IActionResult> UpdatePerson([FromBody] PersonCreateRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (request.Person == null || string.IsNullOrWhiteSpace(request.Person.Id))
            {
                return BadRequest("Person id is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Person.Name))
            {
                return BadRequest("Person name is required.");
            }
            if (request.Person.FacePermission == 0)
            {
                request.Person.FacePermission = 1;
                request.Person.PasswordPermission = 1;
                request.Person.FingerPermission = 1;
            }
            else
            {
                request.Person.FacePermission = 2;
                request.Person.PasswordPermission = 1;
                request.Person.FingerPermission = 1;
            }
            // Match CreatePerson: honor caller deviceIp so the access app and middleware config stay aligned.
            string? mainGate = null;
            if (_deviceSettings.Devices != null && _deviceSettings.Devices.TryGetValue("MainGate", out var mg))
            {
                mainGate = mg;
            }

            var deviceBaseUrl = !string.IsNullOrWhiteSpace(request.DeviceIp)
                ? NormalizeDeviceBaseUrl(request.DeviceIp)
                : NormalizeDeviceBaseUrl(mainGate);

            if (string.IsNullOrWhiteSpace(deviceBaseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "Device IP is required on the request, or configure 'MainGate' in DeviceSettings.");
            }

            var requestUri = $"{deviceBaseUrl}/person/update";

            var httpResponseMessage = await _httpClient.PostAsJsonAsync(requestUri, request);

            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                return StatusCode((int)httpResponseMessage.StatusCode, "Personnel update failed.");
            }

            var responseBody = await httpResponseMessage.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Personnel Deletion (batch or single)
        // POST api/person/delete
        [HttpPost("delete")]
        public async Task<IActionResult> DeletePerson([FromBody] PersonDeleteRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Id))
            {
                return BadRequest("Person ID is required. Use \"-1\" to delete all personnel or comma-separated IDs for batch deletion.");
            }

            string? mainGate = null;
            if (_deviceSettings.Devices != null && _deviceSettings.Devices.TryGetValue("MainGate", out var mg))
            {
                mainGate = mg;
            }

            var deviceBaseUrl = !string.IsNullOrWhiteSpace(request.DeviceIp)
                ? NormalizeDeviceBaseUrl(request.DeviceIp)
                : NormalizeDeviceBaseUrl(mainGate);

            if (string.IsNullOrWhiteSpace(deviceBaseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "Device IP is required on the request, or configure 'MainGate' in DeviceSettings.");
            }

            var url = $"{deviceBaseUrl}/person/delete";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                new KeyValuePair<string, string>("id", request.Id)
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

        // Personnel Query
        // GET api/person/find
        [HttpGet("find")]
        public async Task<IActionResult> FindPerson([FromQuery] string pass, [FromQuery] string id, [FromQuery] string? deviceIp)
        {
            if (string.IsNullOrWhiteSpace(pass))
            {
                return BadRequest("Device password is required.");
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest("Person ID is required. Use \"-1\" to query all personnel.");
            }

            var baseUrl = deviceIp;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                if (!_deviceSettings.Devices.TryGetValue("MainGate", out var fallbackUrl) || string.IsNullOrWhiteSpace(fallbackUrl))
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
                }
                baseUrl = fallbackUrl;
            }

            baseUrl = NormalizeDeviceBaseUrl(baseUrl);

            var url = $"{baseUrl}/person/find?pass={Uri.EscapeDataString(pass)}&id={Uri.EscapeDataString(id)}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Device communication failed");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return Ok(responseBody);
        }

        // Personnel Fingerprint Registration
        // POST api/person/finger-register
        [HttpPost("finger-register")]
        public async Task<IActionResult> FingerRegister([FromBody] FingerprintRegisterRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (string.IsNullOrWhiteSpace(request.PersonId))
            {
                return BadRequest("PersonId is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/face/fingerRegist";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                new KeyValuePair<string, string>("personId", request.PersonId)
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

        // Photo Update (base64)
        // POST api/person/photo-update-base64
        [HttpPost("photo-update-base64")]
        public async Task<IActionResult> UpdatePhotoBase64([FromBody] FaceUpdateBase64Request request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (string.IsNullOrWhiteSpace(request.PersonId))
            {
                return BadRequest("personId is required.");
            }

            if (string.IsNullOrWhiteSpace(request.FaceId))
            {
                return BadRequest("faceId is required.");
            }

            if (string.IsNullOrWhiteSpace(request.ImgBase64))
            {
                return BadRequest("imgBase64 is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/face/update";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                new KeyValuePair<string, string>("personId", request.PersonId),
                new KeyValuePair<string, string>("faceId", request.FaceId),
                new KeyValuePair<string, string>("imgBase64", request.ImgBase64),
                new KeyValuePair<string, string>("isEasyWay", request.IsEasyWay.ToString().ToLower())
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

        // Photo Query
        // POST api/person/photo-find
        [HttpPost("photo-find")]
        public async Task<IActionResult> FindPhoto([FromBody] FaceFindRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (string.IsNullOrWhiteSpace(request.PersonId))
            {
                return BadRequest("personId is required.");
            }

            string? mainGate = null;
            if (_deviceSettings.Devices != null && _deviceSettings.Devices.TryGetValue("MainGate", out var mg))
            {
                mainGate = mg;
            }

            var deviceBaseUrl = !string.IsNullOrWhiteSpace(request.DeviceIp)
                ? NormalizeDeviceBaseUrl(request.DeviceIp)
                : NormalizeDeviceBaseUrl(mainGate);

            if (string.IsNullOrWhiteSpace(deviceBaseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "Device IP is required on the request, or configure 'MainGate' in DeviceSettings.");
            }

            var url = $"{deviceBaseUrl}/face/find";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                new KeyValuePair<string, string>("personId", request.PersonId)
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

        // Photo / Feature Values Deletion
        // POST api/person/delete-photo
        [HttpPost("delete-photo")]
        public async Task<IActionResult> DeletePhoto([FromBody] FaceDeleteRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Pass))
            {
                return BadRequest("Device password is required.");
            }

            if (string.IsNullOrWhiteSpace(request.FaceId))
            {
                return BadRequest("faceId is required.");
            }

            if (!_deviceSettings.Devices.TryGetValue("MainGate", out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Device configuration for 'MainGate' is missing.");
            }

            var url = $"{baseUrl}/face/delete";

            var formPairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("pass", request.Pass),
                new KeyValuePair<string, string>("faceId", request.FaceId)
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
    }
}
