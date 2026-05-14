namespace FaceReader_Middleware.Models
{
    public class FaceCreateRequest
    {
        public string DeviceIp { get; set; }

        public string DevicePassword { get; set; }

        public string PersonId { get; set; }

        public int Type { get; set; } = 1;

        public string? FaceId { get; set; }

        public string? ImgBase64 { get; set; }

        public bool IsEasyWay { get; set; }
    }
}
