using FaceReader_Middleware.Models;

namespace FaceReader_Middleware.Services
{
    public class RecognitionRecordService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public RecognitionRecordService(HttpClient httpClient, IConfiguration config)
        {
            this._httpClient = httpClient;
            this._config = config;
        }

        public async Task<string> GetRecordsAsync(RecognitionRecordQueryRequest req)
        {
            // Prefer device settings provided by the caller; fallback to appsettings.
            string str1 = !string.IsNullOrWhiteSpace(req.DeviceIp)
                ? NormalizeDeviceBaseUrl(req.DeviceIp)
                : NormalizeDeviceBaseUrl(this._config["DeviceSettings:Devices:MainGate"]);

            string str2 = !string.IsNullOrWhiteSpace(req.DevicePassword)
                ? req.DevicePassword
                : this._config["DeviceSettings:Devices:Password"];
            Dictionary<string, string> source = new Dictionary<string, string>()
            {
                ["pass"] = str2,
                ["personId"] = req.PersonId,
                ["startTime"] = req.StartTime,
                ["endTime"] = req.EndTime,
                ["length"] = req.Length.ToString(),
                ["index"] = req.Index.ToString(),
                ["model"] = req.Model.ToString()
            };
            if (!string.IsNullOrWhiteSpace(req.Order))
                source.Add("order", req.Order);
            string str3 = string.Join("&", source.Select<KeyValuePair<string, string>, string>((Func<KeyValuePair<string, string>, string>)(x => $"{x.Key}={Uri.EscapeDataString(x.Value)}")));
            return await this._httpClient.GetStringAsync($"{str1}/newFindRecords?{str3}");
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
    }
}
