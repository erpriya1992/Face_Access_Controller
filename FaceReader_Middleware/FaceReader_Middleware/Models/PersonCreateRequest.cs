using System.Text.Json.Serialization;

namespace FaceReader_Middleware.Models
{
    public class PersonCreateRequest
    {
        [JsonPropertyName("pass")]
        public string Pass { get; set; }

        [JsonPropertyName("deviceIp")]
        public string DeviceIp { get; set; }

        [JsonPropertyName("person")]
        public PersonInfoDetails Person { get; set; }
    }
}
