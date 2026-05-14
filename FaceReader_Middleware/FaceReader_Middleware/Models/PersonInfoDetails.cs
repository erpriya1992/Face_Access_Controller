using System.Text.Json.Serialization;

namespace FaceReader_Middleware.Models
{
    public class PersonInfoDetails
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("idcardNum")]
        public string IdCardNum { get; set; }

        [JsonPropertyName("phone")]
        public string Phone { get; set; }

        [JsonPropertyName("tag")]
        public string Tag { get; set; }

        [JsonPropertyName("facePermission")]
        public int FacePermission { get; set; }

        [JsonPropertyName("passwordPermission")]
        public int PasswordPermission { get; set; }

        [JsonPropertyName("fingerPermission")]
        public int FingerPermission { get; set; }

        [JsonPropertyName("password")]
        public string Password { get; set; }

        [JsonPropertyName("role")]
        public int Role { get; set; }
    }
}
