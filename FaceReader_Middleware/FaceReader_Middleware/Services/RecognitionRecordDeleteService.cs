using FaceReader_Middleware.Models;

namespace FaceReader_Middleware.Services
{
    public class RecognitionRecordDeleteService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public RecognitionRecordDeleteService(HttpClient httpClient, IConfiguration config)
        {
            this._httpClient = httpClient;
            this._config = config;
        }

        public async Task<string> DeleteAsync(RecognitionRecordDeleteRequest req)
        {
            string str1 = this._config["DeviceSettings:Devices:MainGate"];
            string str2 = this._config["DeviceSettings:Devices:Password"];
            FormUrlEncodedContent content = new FormUrlEncodedContent((IEnumerable<KeyValuePair<string, string>>)new Dictionary<string, string>()
            {
                ["pass"] = str2,
                ["personId"] = req.PersonId,
                ["startTime"] = req.StartTime,
                ["endTime"] = req.EndTime,
                ["model"] = req.Model.ToString()
            });
            return await (await this._httpClient.PostAsync(str1 + "/newDeleteRecords", (HttpContent)content)).Content.ReadAsStringAsync();
        }
    }
}
