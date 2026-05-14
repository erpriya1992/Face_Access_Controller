using FaceReader_Middleware.Models;

namespace FaceReader_Middleware.Services
{
    public class DeleteFaceRecordsService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public DeleteFaceRecordsService(HttpClient httpClient, IConfiguration config)
        {
            this._httpClient = httpClient;
            this._config = config;
        }

        public async Task<string> DeleteBeforeAsync(DeleteFaceRecordsRequest request)
        {
            string str1 = this._config["DeviceSettings:Devices:MainGate"];
            string str2 = this._config["DeviceSettings:Devices:Password"];
            FormUrlEncodedContent content = new FormUrlEncodedContent((IEnumerable<KeyValuePair<string, string>>)new Dictionary<string, string>()
            {
                ["pass"] = str2,
                ["time"] = request.Time
            });
            return await (await this._httpClient.PostAsync(str1 + "/deleteRecords", (HttpContent)content)).Content.ReadAsStringAsync();
        }
    }
}
