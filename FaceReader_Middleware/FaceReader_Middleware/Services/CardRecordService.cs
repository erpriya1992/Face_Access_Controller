using FaceReader_Middleware.Models;

namespace FaceReader_Middleware.Services
{
    public class CardRecordService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public CardRecordService(HttpClient httpClient, IConfiguration config)
        {
            this._httpClient = httpClient;
            this._config = config;
        }

        public async Task<string> QueryAsync(FindCardRecordQueryRequest req)
        {
            string str1 = this._config["Device:BaseUrl"];
            string str2 = this._config["Device:Password"];
            string str3 = string.Join("&", new Dictionary<string, string>()
            {
                ["pass"] = str2,
                ["personId"] = req.PersonId,
                ["startTime"] = req.StartTime,
                ["endTime"] = req.EndTime
            }.Select<KeyValuePair<string, string>, string>((Func<KeyValuePair<string, string>, string>)(x => $"{x.Key}={Uri.EscapeDataString(x.Value)}")));
            return await this._httpClient.GetStringAsync($"{str1}/findICRecords?{str3}");
        }
    }
}
